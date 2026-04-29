using TipidMateMauiApp.Models;
using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;
using Plugin.LocalNotification;

namespace TipidMateMauiApp.Services
{
    public class RecurringTransactionService
    {
        private readonly DatabaseService _db;
        private readonly FirebaseService _firebase;
        private readonly MainViewModel _vm;

        private const string LAST_CHECK_KEY = "recurring_last_check";
        private const int NOTIF_RECURRING = 5000;

        public RecurringTransactionService(
            DatabaseService db,
            FirebaseService firebase,
            MainViewModel vm)
        {
            _db = db;
            _firebase = firebase;
            _vm = vm;
        }

        // ─────────────────────────────────────────────────────────
        //  INIT — ensure table exists
        // ─────────────────────────────────────────────────────────
        public async Task InitAsync()
        {
            await _db.InitRecurringTableAsync();
        }

        // ─────────────────────────────────────────────────────────
        //  GET ALL RECURRING
        // ─────────────────────────────────────────────────────────
        public async Task<List<RecurringTransaction>> GetAllAsync()
            => await _db.GetRecurringTransactionsAsync();

        // ─────────────────────────────────────────────────────────
        //  SAVE RECURRING
        // ─────────────────────────────────────────────────────────
        public async Task SaveAsync(RecurringTransaction r)
        {
            // Set next run date on first save
            if (r.Id == 0)
                r.NextRunDate = r.StartDate;

            await _db.SaveRecurringAsync(r);

            // Sync to Firebase
            if (_firebase.IsLoggedIn)
                await SaveToFirebaseAsync(r);
        }

        // ─────────────────────────────────────────────────────────
        //  DELETE RECURRING
        // ─────────────────────────────────────────────────────────
        public async Task DeleteAsync(RecurringTransaction r)
        {
            await _db.DeleteRecurringAsync(r);
            if (_firebase.IsLoggedIn && !string.IsNullOrEmpty(r.FirebaseId))
                await DeleteFromFirebaseAsync(r.FirebaseId);
        }

        // ─────────────────────────────────────────────────────────
        //  CHECK AND PROCESS DUE TRANSACTIONS
        //  Call this on app startup
        // ─────────────────────────────────────────────────────────
        public async Task<int> ProcessDueTransactionsAsync()
        {
            var today = DateTime.Today;
            var lastCheck = Preferences.Get(LAST_CHECK_KEY, DateTime.MinValue.ToString());
            var lastDate = DateTime.Parse(lastCheck);

            // Already checked today
            if (lastDate.Date == today) return 0;

            var recurring = await _db.GetRecurringTransactionsAsync();
            var due = recurring.Where(r =>
                r.IsActive && r.NextRunDate.Date <= today).ToList();

            int count = 0;
            foreach (var r in due)
            {
                // Create actual transaction
                var t = new Transaction
                {
                    Description = r.Description,
                    Category = r.Category,
                    Type = r.Type,
                    Amount = r.Amount,
                    Date = r.NextRunDate.Date == today
                                  ? DateTime.Now
                                  : r.NextRunDate,
                    IsSynced = false
                };

                // Save to SQLite
                await _db.SaveTransactionAsync(t);

                // Sync to Firebase
                if (_firebase.IsLoggedIn)
                {
                    var fbId = await _firebase.SaveTransactionAsync(t);
                    if (fbId != null)
                    {
                        t.FirebaseId = fbId;
                        t.IsSynced = true;
                        await _db.SaveTransactionAsync(t);
                    }
                }

                // Update next run date
                r.RunCount++;
                r.NextRunDate = r.GetNextRunDate(r.NextRunDate.Date);
                await _db.SaveRecurringAsync(r);
                if (_firebase.IsLoggedIn)
                    await SaveToFirebaseAsync(r);

                count++;
            }

            // Save today as last check date
            Preferences.Set(LAST_CHECK_KEY, today.ToString());

            // Reload VM data
            if (count > 0)
                await _vm.LoadDataAsync();

            return count;
        }

        // ─────────────────────────────────────────────────────────
        //  SEND NOTIFICATION for processed recurring transactions
        // ─────────────────────────────────────────────────────────
        public async Task NotifyProcessedAsync(int count)
        {
            if (count == 0) return;

            var request = new NotificationRequest
            {
                NotificationId = NOTIF_RECURRING,
                Title = "🔁 Recurring Transactions Added",
                Description = $"{count} recurring transaction{(count > 1 ? "s" : "")} " +
                                 $"were automatically added today.",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = DateTime.Now.AddSeconds(2),
                    RepeatType = NotificationRepeat.No
                }
            };
            await LocalNotificationCenter.Current.Show(request);
        }

        // ─────────────────────────────────────────────────────────
        //  SCHEDULE DAILY REMINDER for upcoming recurring
        // ─────────────────────────────────────────────────────────
        public async Task ScheduleDailyReminderAsync(int hour = 8, int minute = 0)
        {
            LocalNotificationCenter.Current.Cancel(NOTIF_RECURRING + 1);

            var recurring = await _db.GetRecurringTransactionsAsync();
            var upcoming = recurring
                .Where(r => r.IsActive && r.NextRunDate.Date == DateTime.Today)
                .ToList();

            if (!upcoming.Any()) return;

            var names = string.Join(", ", upcoming.Take(3).Select(r => r.Description));
            var now = DateTime.Now;
            var notifTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (now > notifTime) notifTime = notifTime.AddDays(1);

            var request = new NotificationRequest
            {
                NotificationId = NOTIF_RECURRING + 1,
                Title = "🔁 Recurring Transactions Due Today",
                Description = $"Auto-processing: {names}" +
                                 (upcoming.Count > 3 ? $" +{upcoming.Count - 3} more" : ""),
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = notifTime,
                    RepeatType = NotificationRepeat.Daily
                }
            };
            await LocalNotificationCenter.Current.Show(request);
        }

        // ─────────────────────────────────────────────────────────
        //  FIREBASE HELPERS
        // ─────────────────────────────────────────────────────────
        private async Task SaveToFirebaseAsync(RecurringTransaction r)
        {
            try
            {
                var fields = new Dictionary<string, object>
                {
                    ["description"] = new { stringValue = r.Description },
                    ["category"] = new { stringValue = r.Category },
                    ["type"] = new { stringValue = r.Type },
                    ["amount"] = new { doubleValue = r.Amount },
                    ["frequency"] = new { stringValue = r.Frequency },
                    ["startDate"] = new { stringValue = r.StartDate.ToString("o") },
                    ["nextRunDate"] = new { stringValue = r.NextRunDate.ToString("o") },
                    ["isActive"] = new { booleanValue = r.IsActive },
                    ["runCount"] = new { integerValue = r.RunCount.ToString() }
                };

                var path = $"https://firestore.googleapis.com/v1/projects/" +
                           $"{_firebase.ProjectIdPublic}/databases/(default)/documents/" +
                           $"users/{_firebase.UserId}/recurring";

                using var http = new System.Net.Http.HttpClient();
                var body = System.Text.Json.JsonSerializer.Serialize(new { fields });

                System.Net.Http.HttpResponseMessage res;
                if (string.IsNullOrEmpty(r.FirebaseId))
                {
                    var req = new System.Net.Http.HttpRequestMessage(
                        System.Net.Http.HttpMethod.Post,
                        $"{path}?documentId={Guid.NewGuid()}");
                    req.Headers.Add("Authorization", $"Bearer {_firebase.IdToken}");
                    req.Content = new System.Net.Http.StringContent(
                        body, System.Text.Encoding.UTF8, "application/json");
                    res = await http.SendAsync(req);
                }
                else
                {
                    var req = new System.Net.Http.HttpRequestMessage(
                        new System.Net.Http.HttpMethod("PATCH"),
                        $"{path}/{r.FirebaseId}");
                    req.Headers.Add("Authorization", $"Bearer {_firebase.IdToken}");
                    req.Content = new System.Net.Http.StringContent(
                        body, System.Text.Encoding.UTF8, "application/json");
                    res = await http.SendAsync(req);
                }

                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;
                    var name = doc.GetProperty("name").GetString()!;
                    r.FirebaseId = name.Split('/').Last();
                    await _db.SaveRecurringAsync(r);
                }
            }
            catch { }
        }

        private async Task DeleteFromFirebaseAsync(string firebaseId)
        {
            try
            {
                var path = $"https://firestore.googleapis.com/v1/projects/" +
                           $"{_firebase.ProjectIdPublic}/databases/(default)/documents/" +
                           $"users/{_firebase.UserId}/recurring/{firebaseId}";
                using var http = new System.Net.Http.HttpClient();
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Delete, path);
                req.Headers.Add("Authorization", $"Bearer {_firebase.IdToken}");
                await http.SendAsync(req);
            }
            catch { }
        }
    }
}