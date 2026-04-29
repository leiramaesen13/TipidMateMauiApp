using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TipidMateMauiApp.Models;

namespace TipidMateMauiApp.Services
{
    public class FirebaseService
    {
        public string ProjectIdPublic => ProjectId;

        // ── Firebase credentials ──────────────────────────────────
        private const string ApiKey = "AIzaSyBKkKZJotbmAyQY-oEFQ8FlkKtTkMYLUYQ";
        private const string ProjectId = "tipidmate-d63e4";

        // ── Google OAuth credentials ──────────────────────────────
        private const string GoogleClientId = "490364501530-37sonjlb0tnds67t5fo3v6ifksktj10f.apps.googleusercontent.com";
        private const string GoogleRedirectUri = "http://localhost";  // ← FIXED
        // ─────────────────────────────────────────────────────────

        private readonly HttpClient _http = new();

        // ── Auth state ────────────────────────────────────────────
        public string? IdToken { get; private set; }
        public string? UserId { get; private set; }
        public string? UserEmail { get; private set; }
        public string? DisplayName { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(IdToken);

        // ─────────────────────────────────────────────────────────
        //  EMAIL / PASSWORD AUTH
        // ─────────────────────────────────────────────────────────

        public async Task<(bool success, string error)> SignUpAsync(
            string email, string password, string displayName = "")
        {
            try
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
                var body = new { email, password, returnSecureToken = true };
                var res = await _http.PostAsJsonAsync(url, body);
                var json = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return (false, ParseFirebaseError(json));

                var doc = JsonDocument.Parse(json).RootElement;
                IdToken = doc.GetProperty("idToken").GetString();
                UserId = doc.GetProperty("localId").GetString();
                UserEmail = email;
                DisplayName = displayName;

                if (!string.IsNullOrEmpty(displayName))
                    await UpdateDisplayNameAsync(displayName);

                return (true, string.Empty);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool success, string error)> SignInWithEmailAsync(
            string email, string password)
        {
            try
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
                var body = new { email, password, returnSecureToken = true };
                var res = await _http.PostAsJsonAsync(url, body);
                var json = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return (false, ParseFirebaseError(json));

                var doc = JsonDocument.Parse(json).RootElement;
                IdToken = doc.GetProperty("idToken").GetString();
                UserId = doc.GetProperty("localId").GetString();
                UserEmail = email;
                DisplayName = doc.TryGetProperty("displayName", out var dn)
                              ? dn.GetString() : email.Split('@')[0];
                return (true, string.Empty);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ─────────────────────────────────────────────────────────
        //  GOOGLE SIGN-IN
        // ─────────────────────────────────────────────────────────

        public async Task<(bool success, string error)> SignInWithGoogleAsync()
        {
            try
            {
                var state = Guid.NewGuid().ToString("N");

                var authUrl = new Uri(
                    $"https://accounts.google.com/o/oauth2/v2/auth" +
                    $"?client_id={GoogleClientId}" +
                    $"&redirect_uri={Uri.EscapeDataString(GoogleRedirectUri)}" +
                    $"&response_type=token" +
                    $"&scope={Uri.EscapeDataString("email profile")}" +
                    $"&state={state}");

                var callbackUrl = new Uri(GoogleRedirectUri);  // ← now "http://localhost"

                var result = await WebAuthenticator.Default.AuthenticateAsync(
                    authUrl, callbackUrl);

                var accessToken = result.AccessToken;

                if (string.IsNullOrEmpty(accessToken))
                    return (false, "Google sign-in was cancelled.");

                return await SignInWithGoogleTokenAsync(accessToken);
            }
            catch (TaskCanceledException) { return (false, "Sign-in cancelled."); }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private async Task<(bool success, string error)> SignInWithGoogleTokenAsync(string googleAccessToken)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={ApiKey}";
            var body = new
            {
                postBody = $"access_token={googleAccessToken}&providerId=google.com",
                requestUri = "http://localhost",
                returnSecureToken = true,
                returnIdpCredential = true
            };
            var res = await _http.PostAsJsonAsync(url, body);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return (false, ParseFirebaseError(json));

            var doc = JsonDocument.Parse(json).RootElement;
            IdToken = doc.GetProperty("idToken").GetString();
            UserId = doc.GetProperty("localId").GetString();
            UserEmail = doc.TryGetProperty("email", out var em) ? em.GetString() : "";
            DisplayName = doc.TryGetProperty("displayName", out var dn) ? dn.GetString() : UserEmail?.Split('@')[0];
            return (true, string.Empty);
        }

        // ─────────────────────────────────────────────────────────
        //  PASSWORD RESET
        // ─────────────────────────────────────────────────────────

        public async Task<(bool success, string error)> SendPasswordResetAsync(string email)
        {
            try
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={ApiKey}";
                var body = new { requestType = "PASSWORD_RESET", email };
                var res = await _http.PostAsJsonAsync(url, body);
                return res.IsSuccessStatusCode
                    ? (true, string.Empty)
                    : (false, ParseFirebaseError(await res.Content.ReadAsStringAsync()));
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public void SignOut()
        {
            IdToken = null;
            UserId = null;
            UserEmail = null;
            DisplayName = null;
        }

        // ─────────────────────────────────────────────────────────
        //  FIRESTORE – TRANSACTIONS
        // ─────────────────────────────────────────────────────────

        private string FirestoreBase =>
            $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

        private string TransactionsPath => $"{FirestoreBase}/users/{UserId}/transactions";
        private string BudgetsPath => $"{FirestoreBase}/users/{UserId}/budgets";

        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            var list = new List<Transaction>();
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, TransactionsPath);
                req.Headers.Add("Authorization", $"Bearer {IdToken}");
                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode) return list;

                var doc = JsonDocument.Parse(json).RootElement;
                if (!doc.TryGetProperty("documents", out var docs)) return list;

                foreach (var d in docs.EnumerateArray())
                {
                    var t = FirestoreDocToTransaction(d);
                    if (t != null) list.Add(t);
                }
                list = list.OrderByDescending(t => t.Date).ToList();
            }
            catch { /* offline */ }
            return list;
        }

        public async Task<string?> SaveTransactionAsync(Transaction t)
        {
            try
            {
                var fields = TransactionToFirestoreFields(t);
                var body = JsonSerializer.Serialize(new { fields });

                HttpResponseMessage res;
                if (string.IsNullOrEmpty(t.FirebaseId))
                {
                    var req = new HttpRequestMessage(HttpMethod.Post,
                        $"{TransactionsPath}?documentId={Guid.NewGuid()}");
                    req.Headers.Add("Authorization", $"Bearer {IdToken}");
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    res = await _http.SendAsync(req);
                }
                else
                {
                    var req = new HttpRequestMessage(new HttpMethod("PATCH"),
                        $"{TransactionsPath}/{t.FirebaseId}");
                    req.Headers.Add("Authorization", $"Bearer {IdToken}");
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    res = await _http.SendAsync(req);
                }

                if (!res.IsSuccessStatusCode) return null;
                var json = await res.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;
                var name = doc.GetProperty("name").GetString()!;
                return name.Split('/').Last();
            }
            catch { return null; }
        }

        public async Task<bool> DeleteTransactionAsync(string firebaseId)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Delete,
                    $"{TransactionsPath}/{firebaseId}");
                req.Headers.Add("Authorization", $"Bearer {IdToken}");
                var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────
        //  FIRESTORE – BUDGETS
        // ─────────────────────────────────────────────────────────

        public async Task<List<Budget>> GetBudgetsAsync()
        {
            var list = new List<Budget>();
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, BudgetsPath);
                req.Headers.Add("Authorization", $"Bearer {IdToken}");
                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode) return list;

                var doc = JsonDocument.Parse(json).RootElement;
                if (!doc.TryGetProperty("documents", out var docs)) return list;

                foreach (var d in docs.EnumerateArray())
                {
                    var b = FirestoreDocToBudget(d);
                    if (b != null) list.Add(b);
                }
            }
            catch { }
            return list;
        }

        public async Task SaveBudgetAsync(Budget b)
        {
            try
            {
                var fields = new Dictionary<string, object>
                {
                    ["category"] = new { stringValue = b.Category },
                    ["limitAmount"] = new { doubleValue = b.LimitAmount }
                };
                var body = JsonSerializer.Serialize(new { fields });
                var docId = Uri.EscapeDataString(b.Category);
                var req = new HttpRequestMessage(new HttpMethod("PATCH"),
                    $"{BudgetsPath}/{docId}");
                req.Headers.Add("Authorization", $"Bearer {IdToken}");
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                await _http.SendAsync(req);
            }
            catch { }
        }

        public async Task DeleteBudgetAsync(string category)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Delete,
                    $"{BudgetsPath}/{Uri.EscapeDataString(category)}");
                req.Headers.Add("Authorization", $"Bearer {IdToken}");
                await _http.SendAsync(req);
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────
        //  MIGRATION
        // ─────────────────────────────────────────────────────────

        public async Task MigrateFromSqliteAsync(
            List<Transaction> transactions, List<Budget> budgets)
        {
            foreach (var t in transactions) await SaveTransactionAsync(t);
            foreach (var b in budgets) await SaveBudgetAsync(b);
        }

        // ─────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────

        private static Dictionary<string, object> TransactionToFirestoreFields(Transaction t) =>
            new()
            {
                ["description"] = new { stringValue = t.Description },
                ["category"] = new { stringValue = t.Category },
                ["type"] = new { stringValue = t.Type },
                ["amount"] = new { doubleValue = t.Amount },
                ["date"] = new { stringValue = t.Date.ToString("o") }
            };

        private static Transaction? FirestoreDocToTransaction(JsonElement doc)
        {
            try
            {
                var fields = doc.GetProperty("fields");
                var name = doc.GetProperty("name").GetString()!;
                var firebaseId = name.Split('/').Last();
                return new Transaction
                {
                    FirebaseId = firebaseId,
                    Description = fields.GetProperty("description").GetProperty("stringValue").GetString() ?? "",
                    Category = fields.GetProperty("category").GetProperty("stringValue").GetString() ?? "",
                    Type = fields.GetProperty("type").GetProperty("stringValue").GetString() ?? "expense",
                    Amount = fields.GetProperty("amount").GetProperty("doubleValue").GetDouble(),
                    Date = DateTime.Parse(
                        fields.GetProperty("date").GetProperty("stringValue").GetString()!)
                };
            }
            catch { return null; }
        }

        private static Budget? FirestoreDocToBudget(JsonElement doc)
        {
            try
            {
                var fields = doc.GetProperty("fields");
                return new Budget
                {
                    Category = fields.GetProperty("category").GetProperty("stringValue").GetString() ?? "",
                    LimitAmount = fields.GetProperty("limitAmount").GetProperty("doubleValue").GetDouble()
                };
            }
            catch { return null; }
        }

        private async Task UpdateDisplayNameAsync(string name)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={ApiKey}";
            var body = new { idToken = IdToken, displayName = name, returnSecureToken = false };
            await _http.PostAsJsonAsync(url, body);
        }

        private static string ParseFirebaseError(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json).RootElement;
                var msg = doc.GetProperty("error").GetProperty("message").GetString() ?? "Unknown error";
                return msg switch
                {
                    "EMAIL_EXISTS" => "This email is already registered.",
                    "INVALID_EMAIL" => "Invalid email address.",
                    "WEAK_PASSWORD : Password should be at least 6 characters"
                        => "Password must be at least 6 characters.",
                    "EMAIL_NOT_FOUND" => "No account found with this email.",
                    "INVALID_PASSWORD" => "Incorrect password.",
                    "USER_DISABLED" => "This account has been disabled.",
                    "INVALID_LOGIN_CREDENTIALS" => "Incorrect email or password.",
                    _ => msg
                };
            }
            catch { return "An error occurred. Please try again."; }
        }
    }
}