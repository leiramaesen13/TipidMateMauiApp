using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Services
{
    public class NotificationService
    {
        private readonly MainViewModel _vm;
        private readonly DatabaseService _db;

        // Notification IDs
        private const int ID_DAILY_SUMMARY = 1000;
        private const int ID_BUDGET_BASE = 2000; // +index per category
        private const int ID_OVER_BASE = 3000;

        public NotificationService(MainViewModel vm, DatabaseService db)
        {
            _vm = vm;
            _db = db;
        }

        // ─────────────────────────────────────────────────────────
        //  REQUEST PERMISSION (call once on first launch)
        // ─────────────────────────────────────────────────────────
        public async Task<bool> RequestPermissionAsync()
        {
            var result = await LocalNotificationCenter.Current
                .RequestNotificationPermission();
            return result;
        }

        // ─────────────────────────────────────────────────────────
        //  CHECK BUDGETS NOW and send immediate alerts if needed
        // ─────────────────────────────────────────────────────────
        public async Task CheckAndNotifyAsync()
        {
            await _vm.LoadDataAsync();

            var categories = new[]
            {
                "Food & Dining", "Transport", "Bills & Utilities",
                "Shopping", "Health", "Entertainment", "Savings", "Other"
            };

            int idx = 0;
            foreach (var cat in categories)
            {
                var budget = _vm.GetBudgetForCategory(cat);
                if (budget == null || budget.LimitAmount <= 0)
                {
                    idx++;
                    continue;
                }

                var spent = _vm.GetSpentForCategory(cat);
                var pct = spent / budget.LimitAmount;

                if (spent > budget.LimitAmount)
                {
                    // Over budget
                    await SendNotificationAsync(
                        id: ID_OVER_BASE + idx,
                        title: $"🚨 Over Budget: {cat}",
                        body: $"You've spent ₱{spent:N2} of your ₱{budget.LimitAmount:N2} limit. " +
                               $"That's ₱{spent - budget.LimitAmount:N2} over!",
                        scheduleIn: TimeSpan.FromSeconds(3)
                    );
                }
                else if (pct >= 0.8)
                {
                    // 80% warning
                    await SendNotificationAsync(
                        id: ID_BUDGET_BASE + idx,
                        title: $"⚠️ Budget Warning: {cat}",
                        body: $"You've used {pct * 100:F0}% of your budget. " +
                               $"Only ₱{budget.LimitAmount - spent:N2} left this month.",
                        scheduleIn: TimeSpan.FromSeconds(3)
                    );
                }

                idx++;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  SCHEDULE DAILY NOTIFICATION at 8:00 PM every day
        // ─────────────────────────────────────────────────────────
        public async Task ScheduleDailyBudgetReminderAsync()
        {
            // Cancel existing daily notifications first
            LocalNotificationCenter.Current.Cancel(ID_DAILY_SUMMARY);

            var now = DateTime.Now;
            var notify8PM = new DateTime(now.Year, now.Month, now.Day, 20, 0, 0);

            // If 8PM already passed today, schedule for tomorrow
            if (now > notify8PM)
                notify8PM = notify8PM.AddDays(1);

            var spent = _vm.TotalExpenses;
            var income = _vm.TotalIncome;
            var left = income - spent;

            string body;
            if (left < 0)
                body = $"🚨 You're ₱{Math.Abs(left):N2} over your income this month! Time to tipid!";
            else if (spent / Math.Max(income, 1) >= 0.8)
                body = $"⚠️ You've spent ₱{spent:N2} of ₱{income:N2} income. Only ₱{left:N2} left!";
            else
                body = $"💚 Today's spend: ₱{GetTodaySpend():N2}. Monthly balance: ₱{left:N2}. Keep it up!";

            await SendNotificationAsync(
                id: ID_DAILY_SUMMARY,
                title: "💰 TipidMate Daily Summary",
                body: body,
                scheduleIn: notify8PM - now,
                repeatsDaily: true
            );
        }

        // ─────────────────────────────────────────────────────────
        //  SCHEDULE BUDGET ALERTS for all categories at 8PM daily
        // ─────────────────────────────────────────────────────────
        public async Task ScheduleAllBudgetAlertsAsync(
            int hour = 20,
            int minute = 0,
            bool sendOverBudget = true,
            bool sendEightyPct = true,
            bool sendSummary = true)
        {
            // Cancel existing
            for (int i = 0; i < 10; i++)
            {
                LocalNotificationCenter.Current.Cancel(ID_BUDGET_BASE + i);
                LocalNotificationCenter.Current.Cancel(ID_OVER_BASE + i);
            }
            LocalNotificationCenter.Current.Cancel(ID_DAILY_SUMMARY);

            var now = DateTime.Now;
            var notifTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (now > notifTime) notifTime = notifTime.AddDays(1);
            var delay = notifTime - now;

            var categories = new[]
            {
                "Food & Dining", "Transport", "Bills & Utilities",
                "Shopping", "Health", "Entertainment", "Savings", "Other"
            };

            int idx = 0;
            foreach (var cat in categories)
            {
                var budget = _vm.GetBudgetForCategory(cat);
                if (budget == null || budget.LimitAmount <= 0)
                { idx++; continue; }

                var spent = _vm.GetSpentForCategory(cat);
                var pct = spent / budget.LimitAmount;

                if (sendOverBudget && spent > budget.LimitAmount)
                {
                    await SendNotificationAsync(
                        id: ID_OVER_BASE + idx,
                        title: $"🚨 Over Budget: {cat}",
                        body: $"Over by ₱{spent - budget.LimitAmount:N2}!",
                        scheduleIn: delay,
                        repeatsDaily: true);
                }
                else if (sendEightyPct && pct >= 0.8)
                {
                    await SendNotificationAsync(
                        id: ID_BUDGET_BASE + idx,
                        title: $"⚠️ Budget Warning: {cat}",
                        body: $"{pct * 100:F0}% used — ₱{budget.LimitAmount - spent:N2} left.",
                        scheduleIn: delay,
                        repeatsDaily: true);
                }
                idx++;
            }

            if (sendSummary)
            {
                var left = _vm.TotalIncome - _vm.TotalExpenses;
                await SendNotificationAsync(
                    id: ID_DAILY_SUMMARY,
                    title: "💰 TipidMate Daily Summary",
                    body: $"Today's spend: ₱{GetTodaySpend():N2} | Balance: ₱{left:N2}",
                    scheduleIn: delay,
                    repeatsDaily: true);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  CANCEL ALL
        // ─────────────────────────────────────────────────────────
        public void CancelAll()
        {
            LocalNotificationCenter.Current.CancelAll();
        }

        // ─────────────────────────────────────────────────────────
        //  CORE SEND HELPER
        // ─────────────────────────────────────────────────────────
        private async Task SendNotificationAsync(
            int id,
            string title,
            string body,
            TimeSpan scheduleIn,
            bool repeatsDaily = false)
        {
            var request = new NotificationRequest
            {
                NotificationId = id,
                Title = title,
                Description = body,
                BadgeNumber = 1,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = DateTime.Now.Add(scheduleIn),
                    RepeatType = repeatsDaily
                        ? NotificationRepeat.Daily
                        : NotificationRepeat.No
                },
                Android = new AndroidOptions
                {
                    ChannelId = "tipidmate_budget",
                    IsGroupSummary = false,
                    Color = new AndroidColor(unchecked((int)0xFF4ECDC4)) // ✅ Fix CS0221: unchecked cast
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }

        private double GetTodaySpend() =>
            _vm.Transactions
                .Where(t => !t.IsIncome && t.Date.Date == DateTime.Today)
                .Sum(t => t.Amount);
    }
}