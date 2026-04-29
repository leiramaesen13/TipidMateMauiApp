using Plugin.LocalNotification;
using System.ComponentModel;
using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly NotificationService _notif;
    private readonly string _userName;

    // Preset borders for highlight tracking
    private Border? _activePreset;

    // Selected alert time (default 8PM)
    private TimeSpan _selectedTime = new TimeSpan(20, 0, 0);

    // Preference keys
    private const string KEY_NOTIF_ON = "notif_enabled";
    private const string KEY_NOTIF_HOUR = "notif_hour";
    private const string KEY_NOTIF_MIN = "notif_minute";
    private const string KEY_OVER_BUDGET = "notif_over_budget";
    private const string KEY_EIGHTY_PCT = "notif_eighty_pct";
    private const string KEY_DAILY_SUMMARY = "notif_daily_summary";

    public ProfilePage(MainViewModel vm, NotificationService notif,
                       string userName = "TipidMate User")
    {
        InitializeComponent();
        _vm = vm;
        _notif = notif;
        _userName = userName;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
        SetupProfile();
        BuildStatsRows();
        LoadNotifSettings();
    }

    // ─────────────────────────────────────────────────────────────
    //  PROFILE SETUP
    // ─────────────────────────────────────────────────────────────
    private void SetupProfile()
    {
        var initials = string.Join("",
            _userName.Split(" ").Take(2)
                     .Select(w => w[0].ToString().ToUpper()));
        AvatarLabel.Text = initials;
        UserNameLabel.Text = _userName;
        UserHandleLabel.Text = $"@{_userName.Split(" ")[0].ToLower()}";
        StatEntriesLabel.Text = _vm.Transactions.Count.ToString();
        StatIncomeLabel.Text = $"₱{_vm.TotalIncome / 1000:N0}k";
        StatSpentLabel.Text = $"₱{_vm.TotalExpenses / 1000:N0}k";
    }

    private void BuildStatsRows()
    {
        StatsStack.Children.Clear();
        var srColor = _vm.SavingsRate >= 20 ? "#2E7D32"
                    : _vm.SavingsRate >= 10 ? "#F57F17" : "#C62828";

        var stats = new[]
        {
            ("📊", "Savings Rate",    _vm.SavingsRateDisplay,                    srColor),
            ("💰", "Total Income",    $"₱{_vm.TotalIncome:N2}",                 "#2E7D32"),
            ("💸", "Total Expenses",  $"₱{_vm.TotalExpenses:N2}",               "#C62828"),
            ("🏦", "Net Balance",     $"₱{Math.Abs(_vm.Balance):N2}",
                                       _vm.Balance >= 0 ? "#00897B" : "#C62828"),
            ("🗓️","Transactions",    _vm.Transactions.Count.ToString(),         "#1C2B2B"),
            ("📂", "Categories Used", _vm.CategorySummaries.Count.ToString(),    "#1C2B2B"),
        };

        foreach (var (icon, label, value, color) in stats)
        {
            StatsStack.Children.Add(new BoxView
            { Color = Color.FromArgb("#F1F5F5"), HeightRequest = 1 });

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto)
                },
                ColumnSpacing = 11,
                Padding = new Thickness(0, 9)
            };
            row.Add(new Label
            {
                Text = icon,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            row.Add(new Label
            {
                Text = label,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#37474F"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            row.Add(new Label
            {
                Text = value,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(color),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);
            StatsStack.Children.Add(row);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  LOAD SAVED NOTIFICATION SETTINGS
    // ─────────────────────────────────────────────────────────────
    private void LoadNotifSettings()
    {
        var enabled = Preferences.Get(KEY_NOTIF_ON, false);
        var hour = Preferences.Get(KEY_NOTIF_HOUR, 20);
        var minute = Preferences.Get(KEY_NOTIF_MIN, 0);

        NotifMasterSwitch.IsToggled = enabled;
        OverBudgetSwitch.IsToggled = Preferences.Get(KEY_OVER_BUDGET, true);
        EightyPctSwitch.IsToggled = Preferences.Get(KEY_EIGHTY_PCT, true);
        DailySummarySwitch.IsToggled = Preferences.Get(KEY_DAILY_SUMMARY, true);

        _selectedTime = new TimeSpan(hour, minute, 0);
        CustomTimePicker.Time = _selectedTime;
        NotifOptionsPanel.IsVisible = enabled;

        UpdateNotifStatusLabel();
        UpdateSelectedTimeLabel();
        HighlightMatchingPreset(_selectedTime);
    }

    // ─────────────────────────────────────────────────────────────
    //  MASTER SWITCH TOGGLED
    // ─────────────────────────────────────────────────────────────
    private async void OnMasterSwitchToggled(object sender, ToggledEventArgs e)
    {
        NotifOptionsPanel.IsVisible = e.Value;
        Preferences.Set(KEY_NOTIF_ON, e.Value);
        UpdateNotifStatusLabel();

        if (e.Value)
        {
            var granted = await _notif.RequestPermissionAsync();
            if (!granted)
            {
                await DisplayAlert("Permission Required",
                    "Please enable notifications for TipidMate in your device settings.",
                    "OK");
                NotifMasterSwitch.IsToggled = false;
                return;
            }
            await _notif.ScheduleAllBudgetAlertsAsync();
        }
        else
        {
            _notif.CancelAll();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PRESET TIME TAPPED
    // ─────────────────────────────────────────────────────────────
    private async void OnPresetTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string timeStr) return;
        var parts = timeStr.Split(':');
        _selectedTime = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 0);
        CustomTimePicker.Time = _selectedTime;

        // Highlight selected preset
        if (sender is Element el)
        {
            var border = el as Border ?? el.Parent as Border;
            HighlightPreset(border);
        }

        UpdateSelectedTimeLabel();
        await ApplyAndSaveSettings();
    }

    // ─────────────────────────────────────────────────────────────
    //  CUSTOM TIME CHANGED
    // ─────────────────────────────────────────────────────────────
    private async void OnCustomTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != TimePicker.TimeProperty.PropertyName) return;
        _selectedTime = CustomTimePicker.Time;
        UpdateSelectedTimeLabel();
        HighlightMatchingPreset(_selectedTime);
        await ApplyAndSaveSettings();
    }

    // ─────────────────────────────────────────────────────────────
    //  SAVE BUTTON
    // ─────────────────────────────────────────────────────────────
    private async void OnSaveNotifSettingsClicked(object sender, EventArgs e)
    {
        await ApplyAndSaveSettings();
        await DisplayAlert("✅ Saved",
            $"Notifications set for {FormatTime(_selectedTime)} daily.", "OK");
    }

    // ─────────────────────────────────────────────────────────────
    //  APPLY AND SAVE
    // ─────────────────────────────────────────────────────────────
    private async Task ApplyAndSaveSettings()
    {
        // Save preferences
        Preferences.Set(KEY_NOTIF_HOUR, _selectedTime.Hours);
        Preferences.Set(KEY_NOTIF_MIN, _selectedTime.Minutes);
        Preferences.Set(KEY_OVER_BUDGET, OverBudgetSwitch.IsToggled);
        Preferences.Set(KEY_EIGHTY_PCT, EightyPctSwitch.IsToggled);
        Preferences.Set(KEY_DAILY_SUMMARY, DailySummarySwitch.IsToggled);

        if (!NotifMasterSwitch.IsToggled) return;

        // Cancel existing and reschedule with new time
        _notif.CancelAll();
        await _notif.ScheduleAllBudgetAlertsAsync(
            hour: _selectedTime.Hours,
            minute: _selectedTime.Minutes,
            sendOverBudget: OverBudgetSwitch.IsToggled,
            sendEightyPct: EightyPctSwitch.IsToggled,
            sendSummary: DailySummarySwitch.IsToggled
        );

        UpdateNotifStatusLabel();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI HELPERS
    // ─────────────────────────────────────────────────────────────
    private void UpdateNotifStatusLabel()
    {
        if (NotifMasterSwitch.IsToggled)
            NotifStatusLabel.Text = $"Daily alerts at {FormatTime(_selectedTime)} ✅";
        else
            NotifStatusLabel.Text = "Notifications are off";
    }

    private void UpdateSelectedTimeLabel()
    {
        SelectedTimeLabel.Text =
            $"Alerts scheduled daily at {FormatTime(_selectedTime)}";
    }

    private void HighlightPreset(Border? border)
    {
        // Reset previous
        if (_activePreset != null)
        {
            _activePreset.BackgroundColor = Color.FromArgb("#F8FAFB");
            _activePreset.Stroke = Color.FromArgb("#E2EAEA");
            if (_activePreset.Content is Label lbl)
                lbl.TextColor = Color.FromArgb("#9898A6");
        }

        // Highlight new
        if (border != null)
        {
            border.BackgroundColor = Color.FromArgb("#E8FBF8");
            border.Stroke = Color.FromArgb("#4ECDC4");
            if (border.Content is Label lbl)
                lbl.TextColor = Color.FromArgb("#4ECDC4");
            _activePreset = border;
        }
    }

    private void HighlightMatchingPreset(TimeSpan time)
    {
        var presets = new Dictionary<TimeSpan, Border>
        {
            { new TimeSpan(8,  0, 0), Preset8AM  },
            { new TimeSpan(12, 0, 0), Preset12PM },
            { new TimeSpan(18, 0, 0), Preset6PM  },
            { new TimeSpan(20, 0, 0), Preset8PM  },
            { new TimeSpan(22, 0, 0), Preset10PM },
        };

        // Reset all first
        foreach (var (_, b) in presets)
        {
            b.BackgroundColor = Color.FromArgb("#F8FAFB");
            b.Stroke = Color.FromArgb("#E2EAEA");
            if (b.Content is Label l) l.TextColor = Color.FromArgb("#9898A6");
        }

        // Highlight match
        if (presets.TryGetValue(time, out var match))
            HighlightPreset(match);
    }

    private static string FormatTime(TimeSpan t)
    {
        var dt = DateTime.Today.Add(t);
        return dt.ToString("h:mm tt");
    }

    // ─────────────────────────────────────────────────────────────
    //  NAVIGATION
    // ─────────────────────────────────────────────────────────────
    private void OnBudgetsClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("BudgetPage");

    private void OnExportClicked(object sender, EventArgs e)
        => _vm.ExportCsvCommand.Execute(null);

    private void OnSummaryClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("//SummaryPage");

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool ok = await DisplayAlert("Sign Out",
            $"See you next time, {_userName.Split(" ")[0]}!",
            "Sign Out", "Cancel");
        if (!ok) return;

        _notif.CancelAll();
        var loginPage = Handler?.MauiContext?.Services.GetService<LoginPage>();
        if (loginPage != null)
            Application.Current!.Windows[0].Page = loginPage;
    }
}