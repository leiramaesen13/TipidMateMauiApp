using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class LoginPage : ContentPage
{
    private readonly FirebaseService _firebase;
    private readonly DatabaseService _db;
    private readonly MainViewModel _vm;
    private bool _showPassword = false;
    private bool _isLoginTab = true;

    public LoginPage(FirebaseService firebase, DatabaseService db, MainViewModel vm)
    {
        InitializeComponent();
        _firebase = firebase;
        _db = db;
        _vm = vm;
    }

    // ── Tab switching ─────────────────────────────────────────────
    private void OnLoginTabTapped(object sender, EventArgs e)
    {
        if (_isLoginTab) return;
        _isLoginTab = true;
        LoginPanel.IsVisible = true;
        SignUpPanel.IsVisible = false;
        HideMessages();
    }

    private void OnSignUpTabTapped(object sender, EventArgs e)
    {
        if (!_isLoginTab) return;
        _isLoginTab = false;
        LoginPanel.IsVisible = false;
        SignUpPanel.IsVisible = true;
        HideMessages();
    }

    // ── Password toggle ───────────────────────────────────────────
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _showPassword = !_showPassword;
        PasswordEntry.IsPassword = !_showPassword;
        ShowPassBtn.Text = _showPassword ? "🙈" : "👁️";
    }

    // ── SIGN IN ───────────────────────────────────────────────────
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { ShowError("Please enter your email and password."); return; }

        SetLoading(true);

        var (success, error) = await _firebase.SignInWithEmailAsync(email, password);

        if (success)
        {
            await OnLoginSuccess();
        }
        else
        {
            SetLoading(false);
            ShowError(error);
            PasswordEntry.Text = string.Empty;
        }
    }

    // ── SIGN UP ───────────────────────────────────────────────────
    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        var name = SignUpNameEntry.Text?.Trim() ?? string.Empty;
        var email = SignUpEmailEntry.Text?.Trim() ?? string.Empty;
        var password = SignUpPasswordEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { ShowError("Please enter your email and password."); return; }

        if (password.Length < 6)
        { ShowError("Password must be at least 6 characters."); return; }

        SetLoading(true);

        var (success, error) = await _firebase.SignUpAsync(email, password, name);

        if (success)
        {
            await OnLoginSuccess();
        }
        else
        {
            SetLoading(false);
            ShowError(error);
        }
    }

    // ── GOOGLE SIGN-IN ────────────────────────────────────────────
    private async void OnGoogleSignInClicked(object sender, EventArgs e)
    {
        SetLoading(true);

        var (success, error) = await _firebase.SignInWithGoogleAsync();

        if (success)
        {
            await OnLoginSuccess();
        }
        else
        {
            SetLoading(false);
            if (!error.Contains("cancel", StringComparison.OrdinalIgnoreCase))
                ShowError(error);
        }
    }

    // ── FORGOT PASSWORD ───────────────────────────────────────────
    private async void OnForgotPasswordTapped(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Enter your email address first, then tap Forgot password.");
            return;
        }

        SetLoading(true);
        var (success, error) = await _firebase.SendPasswordResetAsync(email);
        SetLoading(false);

        if (success)
            ShowSuccess($"Password reset email sent to {email}");
        else
            ShowError(error);
    }

    // ── POST-LOGIN: migrate SQLite data, then navigate ────────────
    private async Task OnLoginSuccess()
    {
        try
        {
            // Check if this device has unsynced SQLite data → migrate once
            var hasMigrated = await _db.HasMigratedToFirebaseAsync();
            if (!hasMigrated)
            {
                ShowSuccess("Syncing your existing data to the cloud… ☁️");
                await _vm.MigrateSqliteToFirebaseAsync();
            }

            await _vm.LoadDataAsync();

            // ── ADD THESE TWO LINES ──────────────────────────────
            var notif = IPlatformApplication.Current?.Services
                            .GetService<NotificationService>();
            if (notif != null)
            {
                await notif.RequestPermissionAsync();
                await notif.ScheduleAllBudgetAlertsAsync();
            }
            // ────────────────────────────────────────────────────

        }
        catch { /* non-fatal – proceed anyway */ }
        finally
        {
            SetLoading(false);
        }

        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = new AppShell();
    }

    // ── UI helpers ────────────────────────────────────────────────
    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        SignInBtn.IsEnabled = !loading;
        SignUpBtn.IsEnabled = !loading;
        GoogleBtn.IsEnabled = !loading;
    }

    private void ShowError(string msg)
    {
        SuccessBorder.IsVisible = false;
        ErrorLabel.Text = msg;
        ErrorBorder.IsVisible = true;
    }

    private void ShowSuccess(string msg)
    {
        ErrorBorder.IsVisible = false;
        SuccessLabel.Text = msg;
        SuccessBorder.IsVisible = true;
    }

    private void HideMessages()
    {
        ErrorBorder.IsVisible = false;
        SuccessBorder.IsVisible = false;
    }
}