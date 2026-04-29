using Microsoft.Maui.Controls.Shapes;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
        UpdateGreeting();
        UpdateSavingsBar();
        BuildCategoryBars();
        BuildRecentList();
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        var g = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        GreetingLabel.Text = $"{g}! 👋";
        ProfileInitialsLabel.Text = "AU";
    }

    private void UpdateSavingsBar()
    {
        var rate = Math.Min(_vm.SavingsRate / 100.0, 1.0);
        var w = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - 80;
        SavingsProgressBar.WidthRequest = w * rate;
    }

    private void BuildCategoryBars()
    {
        CategoryBarsStack.Children.Clear();
        var summaries = _vm.CategorySummaries;
        NoCategoriesLabel.IsVisible = summaries.Count == 0;

        var categoryColors = new Dictionary<string, (string, string, string)>
        {
            {"Food & Dining",     ("#E53935","#FFEBEE","🍜")},
            {"Transport",         ("#1565C0","#E3F2FD","🚌")},
            {"Bills & Utilities", ("#F57F17","#FFF8E1","⚡")},
            {"Shopping",          ("#6A1B9A","#F3E5F5","🛍️")},
            {"Health",            ("#AD1457","#FCE4EC","❤️")},
            {"Entertainment",     ("#00838F","#E0F7FA","🎬")},
            {"Savings",           ("#2E7D32","#E8F5E9","💰")},
            {"Other",             ("#4E342E","#EFEBE9","📦")},
        };

        foreach (var cat in summaries)
        {
            var info = categoryColors.TryGetValue(cat.Category ?? "", out var ci)
                           ? ci : ("#00897B", "#E0F2F1", "📦");
            var catColor = Color.FromArgb(info.Item1);

            var barPct = cat.HasBudget
                ? Math.Min(cat.Total / cat.Budget, 1.0)
                : (_vm.TotalExpenses > 0 ? cat.Total / _vm.TotalExpenses : 0);

            var row = new VerticalStackLayout { Spacing = 5 };

            var labelRow = new Grid { ColumnSpacing = 8 };
            labelRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            labelRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var iconBubble = new Border
            {
                WidthRequest = 26,
                HeightRequest = 26,
                StrokeShape = new RoundRectangle { CornerRadius = 7 },
                BackgroundColor = Color.FromArgb(info.Item2),
                Stroke = Colors.Transparent,
                Content = new Label
                {
                    Text = info.Item3,
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var nameStack = new HorizontalStackLayout
            { Spacing = 7, VerticalOptions = LayoutOptions.Center };
            nameStack.Add(iconBubble);
            nameStack.Add(new Label
            {
                Text = cat.Category,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#37474F"),
                VerticalOptions = LayoutOptions.Center
            });
            labelRow.Add(nameStack, 0, 0);

            var amtStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.End };
            amtStack.Add(new Label
            {
                Text = cat.TotalDisplay,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = cat.IsOverBudget
                                 ? Color.FromArgb("#C62828")
                                 : Color.FromArgb("#1C2B2B"),
                HorizontalOptions = LayoutOptions.End
            });
            if (cat.HasBudget)
                amtStack.Add(new Label
                {
                    Text = $"of {cat.BudgetDisplay}",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#90A4A4"),
                    HorizontalOptions = LayoutOptions.End
                });
            labelRow.Add(amtStack, 1, 0);
            row.Add(labelRow);

            var track = new Border
            {
                BackgroundColor = Color.FromArgb("#F1F5F5"),
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                HeightRequest = 6
            };
            var fill = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                HeightRequest = 6,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = 0,
                BackgroundColor = cat.IsOverBudget
                                  ? Color.FromArgb("#C62828")
                                  : catColor
            };
            track.Content = fill;
            track.SizeChanged += (s, e) => fill.WidthRequest = track.Width * barPct;
            row.Add(track);

            if (cat.IsOverBudget)
                row.Add(new Label
                {
                    Text = $"🚨 Over by ₱{cat.Total - cat.Budget:N2}",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#C62828"),
                    FontAttributes = FontAttributes.Bold
                });
            else if (cat.HasBudget)
                row.Add(new Label
                {
                    Text = $"₱{cat.Budget - cat.Total:N2} remaining",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#90A4A4")
                });

            CategoryBarsStack.Children.Add(row);
        }
    }

    private void BuildRecentList()
    {
        RecentStack.Children.Clear();

        var categoryIcons = new Dictionary<string, (string, string)>
        {
            {"Food & Dining",     ("🍜","#FFEBEE")},
            {"Transport",         ("🚌","#E3F2FD")},
            {"Bills & Utilities", ("⚡","#FFF8E1")},
            {"Shopping",          ("🛍️","#F3E5F5")},
            {"Health",            ("❤️","#FCE4EC")},
            {"Entertainment",     ("🎬","#E0F7FA")},
            {"Savings",           ("💰","#E8F5E9")},
            {"Other",             ("📦","#EFEBE9")},
        };

        foreach (var t in _vm.Transactions.Take(5))
        {
            var info = categoryIcons.TryGetValue(t.Category, out var ci)
                       ? ci : ("📦", "#EFEBE9");

            var row = new Grid { ColumnSpacing = 11 };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            row.Add(new Border
            {
                WidthRequest = 42,
                HeightRequest = 42,
                StrokeShape = new RoundRectangle { CornerRadius = 11 },
                BackgroundColor = Color.FromArgb(info.Item2),
                Stroke = Colors.Transparent,
                Content = new Label
                {
                    Text = info.Item1,
                    FontSize = 21,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }, 0, 0);

            var textStack = new VerticalStackLayout
            { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Add(new Label
            {
                Text = t.Description,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1C2B2B"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            textStack.Add(new Label
            {
                Text = t.Date.ToString("MMM dd"),
                FontSize = 10,
                TextColor = Color.FromArgb("#90A4A4")
            });
            row.Add(textStack, 1, 0);

            row.Add(new Label
            {
                Text = t.AmountDisplay,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = t.IsIncome
                                 ? Color.FromArgb("#2E7D32")
                                 : Color.FromArgb("#C62828"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            RecentStack.Children.Add(row);
        }
    }

    private void OnAddEntryClicked(object sender, EventArgs e) { _vm.ResetForm(); Shell.Current.GoToAsync("AddEntryPage"); }
    private void OnViewAllClicked(object sender, EventArgs e) => Shell.Current.GoToAsync("//HistoryPage");
    private void OnBudgetsClicked(object sender, EventArgs e) => Shell.Current.GoToAsync("BudgetPage");
    private void OnProfileTapped(object sender, EventArgs e) => Shell.Current.GoToAsync("//ProfilePage");
}