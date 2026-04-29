using Microsoft.Maui.Controls.Shapes;
using TipidMateMauiApp.Models;
using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class BudgetPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly DatabaseService _db;

    // ── Period state ──────────────────────────────────────────────
    private bool _isMonthly = true;
    private DateTime _currentMonth;
    private DateTime _currentWeekStart;

    private readonly (string Name, string Icon, string Color)[] _categories =
    {
        ("Food & Dining",    "🍜", "#FF6B6B"),
        ("Transport",        "🚌", "#4ECDC4"),
        ("Bills & Utilities","💡", "#FFD166"),
        ("Shopping",         "🛒", "#06D6A0"),
        ("Health",           "💊", "#FF8B94"),
        ("Entertainment",    "🎮", "#B4A7E5"),
    };

    private readonly Dictionary<string, Entry> _entryMap = new();

    public BudgetPage(MainViewModel vm, DatabaseService db)
    {
        InitializeComponent();
        _vm = vm;
        _db = db;
        BindingContext = _vm;

        // Start on current month and week
        _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        _currentWeekStart = GetMonday(DateTime.Today);

        UpdatePeriodLabels();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
        UpdatePeriodLabels();
        BuildBudgetRows();
        UpdateSummaryCards();
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB SWITCHING
    // ─────────────────────────────────────────────────────────────
    private void OnMonthlyTabTapped(object sender, EventArgs e)
    {
        if (_isMonthly) return;
        _isMonthly = true;
        MonthlyTabBorder.BackgroundColor = Color.FromArgb("#4ECDC4");
        MonthlyTabLabel.TextColor = Colors.White;
        MonthlyPeriodLabel.TextColor = Color.FromArgb("#E0F7F5");
        WeeklyTabBorder.BackgroundColor = Color.FromArgb("#F0F4F4");
        WeeklyTabLabel.TextColor = Color.FromArgb("#9898A6");
        WeeklyPeriodLabel.TextColor = Color.FromArgb("#9898A6");
        UpdatePeriodLabels();
        BuildBudgetRows();
        UpdateSummaryCards();
    }

    private void OnWeeklyTabTapped(object sender, EventArgs e)
    {
        if (!_isMonthly) return;
        _isMonthly = false;
        WeeklyTabBorder.BackgroundColor = Color.FromArgb("#4ECDC4");
        WeeklyTabLabel.TextColor = Colors.White;
        WeeklyPeriodLabel.TextColor = Color.FromArgb("#E0F7F5");
        MonthlyTabBorder.BackgroundColor = Color.FromArgb("#F0F4F4");
        MonthlyTabLabel.TextColor = Color.FromArgb("#9898A6");
        MonthlyPeriodLabel.TextColor = Color.FromArgb("#9898A6");
        UpdatePeriodLabels();
        BuildBudgetRows();
        UpdateSummaryCards();
    }

    // ─────────────────────────────────────────────────────────────
    //  PREV / NEXT NAVIGATION
    // ─────────────────────────────────────────────────────────────
    private void OnPrevPeriod(object sender, EventArgs e)
    {
        if (_isMonthly)
            _currentMonth = _currentMonth.AddMonths(-1);
        else
            _currentWeekStart = _currentWeekStart.AddDays(-7);

        UpdatePeriodLabels();
        BuildBudgetRows();
        UpdateSummaryCards();
    }

    private void OnNextPeriod(object sender, EventArgs e)
    {
        if (_isMonthly)
        {
            var next = _currentMonth.AddMonths(1);
            if (next <= DateTime.Today) _currentMonth = next;
        }
        else
        {
            var next = _currentWeekStart.AddDays(7);
            if (next <= DateTime.Today) _currentWeekStart = next;
        }

        UpdatePeriodLabels();
        BuildBudgetRows();
        UpdateSummaryCards();
    }

    // ─────────────────────────────────────────────────────────────
    //  LABEL UPDATES
    // ─────────────────────────────────────────────────────────────
    private void UpdatePeriodLabels()
    {
        if (_isMonthly)
        {
            var label = _currentMonth.ToString("MMMM yyyy");
            PeriodRangeLabel.Text = label;
            MonthlyPeriodLabel.Text = label;
            PeriodSubtitle.Text = "Monthly budget limits per category";
        }
        else
        {
            var weekEnd = _currentWeekStart.AddDays(6);
            var label = $"{_currentWeekStart:MMM dd} – {weekEnd:MMM dd, yyyy}";
            PeriodRangeLabel.Text = label;
            WeeklyPeriodLabel.Text = $"{_currentWeekStart:MMM dd} – {weekEnd:MMM dd}";
            PeriodSubtitle.Text = "Weekly budget limits per category";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SUMMARY CARDS
    // ─────────────────────────────────────────────────────────────
    private void UpdateSummaryCards()
    {
        double income, spent;

        if (_isMonthly)
        {
            income = _vm.Transactions
                .Where(t => t.IsIncome
                         && t.Date.Year == _currentMonth.Year
                         && t.Date.Month == _currentMonth.Month)
                .Sum(t => t.Amount);

            spent = _vm.Transactions
                .Where(t => !t.IsIncome
                         && t.Date.Year == _currentMonth.Year
                         && t.Date.Month == _currentMonth.Month)
                .Sum(t => t.Amount);
        }
        else
        {
            var weekEnd = _currentWeekStart.AddDays(7);
            income = _vm.Transactions
                .Where(t => t.IsIncome
                         && t.Date.Date >= _currentWeekStart
                         && t.Date.Date < weekEnd)
                .Sum(t => t.Amount);

            spent = _vm.Transactions
                .Where(t => !t.IsIncome
                         && t.Date.Date >= _currentWeekStart
                         && t.Date.Date < weekEnd)
                .Sum(t => t.Amount);
        }

        var left = income - spent;
        SummaryIncome.Text = $"₱{income:N0}";
        SummarySpent.Text = $"₱{spent:N0}";
        SummaryLeft.Text = $"₱{Math.Abs(left):N0}";
        SummaryLeft.TextColor = left >= 0
            ? Color.FromArgb("#4ECDC4")
            : Color.FromArgb("#FF6B6B");
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD BUDGET ROWS
    // ─────────────────────────────────────────────────────────────
    private void BuildBudgetRows()
    {
        BudgetRowsStack.Children.Clear();
        _entryMap.Clear();

        foreach (var cat in _categories)
        {
            var existingBudget = _vm.GetBudgetForCategory(cat.Name);
            var catColor = Color.FromArgb(cat.Color);

            // Get spent for the currently viewed period
            double spent;
            if (_isMonthly)
            {
                spent = _vm.GetSpentForCategoryInMonth(
                    cat.Name, _currentMonth.Year, _currentMonth.Month);
            }
            else
            {
                var weekEnd = _currentWeekStart.AddDays(7);
                spent = _vm.Transactions
                    .Where(t => t.Category == cat.Name
                             && !t.IsIncome
                             && t.Date.Date >= _currentWeekStart
                             && t.Date.Date < weekEnd)
                    .Sum(t => t.Amount);
            }

            // Weekly budget = monthly / 4.33
            double limitAmount = existingBudget?.LimitAmount ?? 0;
            double displayLimit = _isMonthly
                ? limitAmount
                : Math.Round(limitAmount / 4.33, 0);

            var row = new VerticalStackLayout { Spacing = 8 };

            // ── Top row: icon + name + entry ──────────────────────
            var topRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(new GridLength(120))
                },
                ColumnSpacing = 10
            };

            // Icon bubble
            var iconBubble = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                BackgroundColor = Color.FromArgb(cat.Color + "15"),
                Stroke = Color.FromArgb(cat.Color + "33"),
                StrokeThickness = 1.5,
                Content = new Label
                {
                    Text = cat.Icon,
                    FontSize = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            topRow.Add(iconBubble, 0, 0);

            // Name + spent label
            var nameStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            nameStack.Add(new Label
            {
                Text = cat.Name,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3D3D4E")
            });
            nameStack.Add(new Label
            {
                Text = $"Spent: ₱{spent:N2}",
                FontSize = 11,
                TextColor = Color.FromArgb("#9898A6")
            });
            topRow.Add(nameStack, 1, 0);

            // Amount entry (monthly budget — weekly shown as info)
            var entryBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#FAF9F6"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Stroke = Color.FromArgb("#EFEFEF"),
                StrokeThickness = 1.5,
                VerticalOptions = LayoutOptions.Center
            };
            var entryGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto), new(GridLength.Star)
                }
            };
            entryGrid.Add(new Label
            {
                Text = "₱",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#9898A6"),
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(10, 0, 0, 0)
            }, 0, 0);

            var amtEntry = new Entry
            {
                Placeholder = "0",
                PlaceholderColor = Color.FromArgb("#CCCCCC"),
                Keyboard = Keyboard.Numeric,
                FontSize = 13,
                TextColor = Color.FromArgb("#2D2D35"),
                BackgroundColor = Colors.Transparent,
                // Always show monthly limit in entry — weekly is read-only display
                Text = limitAmount > 0 ? limitAmount.ToString("F0") : string.Empty
            };
            entryGrid.Add(amtEntry, 1, 0);
            entryBorder.Content = entryGrid;
            topRow.Add(entryBorder, 2, 0);

            _entryMap[cat.Name] = amtEntry;
            row.Add(topRow);

            // Weekly breakdown label (only shown in weekly mode)
            if (!_isMonthly && limitAmount > 0)
            {
                row.Add(new Label
                {
                    Text = $"Weekly limit ≈ ₱{displayLimit:N0}  (monthly ₱{limitAmount:N0} ÷ 4.33)",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#4ECDC4"),
                    FontAttributes = FontAttributes.Bold
                });
            }

            // Progress bar
            double progressLimit = _isMonthly ? limitAmount : displayLimit;
            if (progressLimit > 0)
            {
                var pct = Math.Min(spent / progressLimit, 1.0);
                var isOver = spent > progressLimit;
                var is80 = pct >= 0.8 && !isOver;

                var track = new Border
                {
                    BackgroundColor = Color.FromArgb("#F0F0F5"),
                    StrokeShape = new RoundRectangle { CornerRadius = 5 },
                    HeightRequest = 8
                };
                var fill = new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 5 },
                    HeightRequest = 8,
                    HorizontalOptions = LayoutOptions.Start,
                    WidthRequest = 0
                };
                fill.BackgroundColor = isOver ? Color.FromArgb("#FF6B6B")
                                     : is80 ? Color.FromArgb("#FFD166")
                                              : catColor;
                track.Content = fill;
                track.SizeChanged += (s, e) => { fill.WidthRequest = track.Width * pct; };
                row.Add(track);

                // Status label
                if (isOver)
                    row.Add(new Label
                    {
                        Text = $"🚨 Over budget by ₱{spent - progressLimit:N2}",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#FF6B6B"),
                        FontAttributes = FontAttributes.Bold
                    });
                else if (is80)
                    row.Add(new Label
                    {
                        Text = $"⚠️ {pct * 100:F0}% used — ₱{progressLimit - spent:N2} left",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#E6A817"),
                        FontAttributes = FontAttributes.Bold
                    });
                else
                    row.Add(new Label
                    {
                        Text = $"✅ ₱{progressLimit - spent:N2} remaining ({pct * 100:F0}% used)",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#9898A6")
                    });
            }

            // Separator
            if (cat.Name != _categories.Last().Name)
                row.Add(new BoxView
                {
                    Color = Color.FromArgb("#F0F0F5"),
                    HeightRequest = 1,
                    Margin = new Thickness(0, 4, 0, 0)
                });

            BudgetRowsStack.Children.Add(row);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SAVE
    // ─────────────────────────────────────────────────────────────
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        int saved = 0;
        foreach (var (catName, entry) in _entryMap)
        {
            if (double.TryParse(entry.Text, out double amount) && amount > 0)
            {
                await _db.SaveBudgetAsync(new Budget { Category = catName, LimitAmount = amount });
                saved++;
            }
            else if (string.IsNullOrWhiteSpace(entry.Text))
            {
                await _db.DeleteBudgetAsync(catName);
            }
        }

        await _vm.LoadDataAsync();

        // ── ADD THIS: reschedule alerts with new budget values ───
        var notif = IPlatformApplication.Current?.Services
                        .GetService<NotificationService>();
        if (notif != null)
            await notif.ScheduleAllBudgetAlertsAsync();
        // ────────────────────────────────────────────────────────

        await DisplayAlert("✅ Saved",
            $"{saved} budget limit{(saved != 1 ? "s" : "")} saved successfully!", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private void OnBackClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("..");

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────
    private static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}