using Microcharts;
using Microcharts.Maui;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class SummaryPage : ContentPage
{
    private readonly MainViewModel _vm;
    private string? _activeFilter = null;

    private readonly (string Name, string Icon,
                       string Color, string Bg)[] _catInfo =
    {
        ("Food & Dining",    "🍜","#E53935","#FFEBEE"),
        ("Transport",        "🚌","#1565C0","#E3F2FD"),
        ("Bills & Utilities","⚡","#F57F17","#FFF8E1"),
        ("Shopping",         "🛍️","#6A1B9A","#F3E5F5"),
        ("Health",           "❤️","#AD1457","#FCE4EC"),
        ("Entertainment",    "🎬","#00838F","#E0F7FA"),
        ("Savings",          "💰","#2E7D32","#E8F5E9"),
        ("Other",            "📦","#4E342E","#EFEBE9"),
    };

    public SummaryPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
        BuildPieChart();
        BuildBarChart();
        BuildLineChart();
        BuildMonthlyChart();
        BuildTableRows();
        BuildIncomeRows();
        UpdateNetBalance();
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB SWITCHING
    // ─────────────────────────────────────────────────────────────
    private void OnPieTabTapped(object sender, TappedEventArgs e)
        => SwitchTab("pie");

    private void OnBarTabTapped(object sender, TappedEventArgs e)
        => SwitchTab("bar");

    private void OnLineTabTapped(object sender, TappedEventArgs e)
        => SwitchTab("line");

    private void SwitchTab(string tab)
    {
        PieCard.IsVisible = tab == "pie";
        BarCard.IsVisible = tab == "bar";
        LineCard.IsVisible = tab == "line";

        SetTab(PieTabBorder, PieTabLabel, "🥧 Pie", tab == "pie");
        SetTab(BarTabBorder, BarTabLabel, "📊 Weekly", tab == "bar");
        SetTab(LineTabBorder, LineTabLabel, "📈 Trend", tab == "line");
    }

    private static void SetTab(Border border, Label label,
                                string text, bool active)
    {
        border.BackgroundColor = active
            ? Color.FromArgb("#4ECDC4") : Colors.Transparent;
        label.Text = text;
        label.TextColor = active ? Colors.White
                                     : Color.FromArgb("#9898A6");
        label.FontAttributes = active
            ? FontAttributes.Bold : FontAttributes.None;
    }

    // ─────────────────────────────────────────────────────────────
    //  PIE CHART (Microcharts DonutChart)
    // ─────────────────────────────────────────────────────────────
    private void BuildPieChart()
    {
        var now = DateTime.Now;
        PieMonthLabel.Text = now.ToString("MMMM yyyy");

        var expenses = _vm.Transactions
            .Where(t => !t.IsIncome
                     && t.Date.Year == now.Year
                     && t.Date.Month == now.Month)
            .ToList();

        var total = expenses.Sum(t => t.Amount);
        PieTotalLabel.Text = $"₱{total:N2}";

        if (!expenses.Any())
        {
            PieEmptyLabel.IsVisible = true;
            PieChartView.IsVisible = false;
            PieLegendStack.Children.Clear();
            return;
        }

        PieEmptyLabel.IsVisible = false;
        PieChartView.IsVisible = true;

        var entries = new List<ChartEntry>();
        PieLegendStack.Children.Clear();

        foreach (var cat in _catInfo)
        {
            var catTotal = expenses
                .Where(t => t.Category == cat.Name)
                .Sum(t => t.Amount);
            if (catTotal <= 0) continue;

            var skColor = SKColor.Parse(cat.Color);
            var pct = total > 0 ? (catTotal / total) * 100 : 0;

            entries.Add(new ChartEntry((float)catTotal)
            {
                Label = cat.Name,
                ValueLabel = $"₱{catTotal:N0}",
                Color = skColor,
                TextColor = skColor,
                ValueLabelColor = skColor
            });

            // Legend row
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto),
                    new(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Padding = new Thickness(0, 5)
            };

            row.Add(new BoxView
            {
                Color = Color.FromArgb(cat.Color),
                WidthRequest = 10,
                HeightRequest = 10,
                CornerRadius = 2,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            row.Add(new Label
            {
                Text = $"{cat.Icon} {cat.Name}",
                FontSize = 11,
                TextColor = Color.FromArgb("#37474F"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            row.Add(new Label
            {
                Text = $"₱{catTotal:N2}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(cat.Color),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            row.Add(new Label
            {
                Text = $"{pct:F1}%",
                FontSize = 10,
                TextColor = Color.FromArgb("#9898A6"),
                VerticalOptions = LayoutOptions.Center
            }, 3, 0);

            // Tap legend row to filter
            var catName = cat.Name;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => ApplyFilter(catName);
            row.GestureRecognizers.Add(tap);

            PieLegendStack.Children.Add(row);
            PieLegendStack.Children.Add(new BoxView
            {
                Color = Color.FromArgb("#F0F4F4"),
                HeightRequest = 1
            });
        }

        PieChartView.Chart = new DonutChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 28,
            HoleRadius = 0.5f,
            LabelMode = LabelMode.None,
            AnimationDuration = TimeSpan.FromMilliseconds(600)
        };
    }

    private void ApplyFilter(string category)
    {
        _activeFilter = category;
        _vm.FilterCategory = category;
        FilterBanner.IsVisible = true;
        FilterLabel.Text =
            $"🔍 {category} — {_vm.FilteredCount} transactions";
        BuildTableRows();
    }

    private void OnClearFilterClicked(object sender, EventArgs e)
    {
        _activeFilter = null;
        _vm.FilterCategory = "All";
        FilterBanner.IsVisible = false;
        BuildTableRows();
    }

    // ─────────────────────────────────────────────────────────────
    //  BAR CHART (Microcharts BarChart — daily this week)
    // ─────────────────────────────────────────────────────────────
    private void BuildBarChart()
    {
        var today = DateTime.Today;
        int dow = (int)today.DayOfWeek;
        var monday = today.AddDays(dow == 0 ? -6 : -(dow - 1));
        var sunday = monday.AddDays(6);

        BarWeekLabel.Text = $"{monday:MMM dd} – {sunday:MMM dd, yyyy}";

        var dayLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var colors = new[]
        {
            "#4ECDC4","#38b2aa","#4ECDC4","#38b2aa",
            "#4ECDC4","#FFD166","#FF6B6B"
        };

        var entries = new List<ChartEntry>();
        double weekTotal = 0;

        for (int i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var spend = _vm.Transactions
                .Where(t => !t.IsIncome && t.Date.Date == day)
                .Sum(t => t.Amount);
            weekTotal += spend;

            entries.Add(new ChartEntry((float)spend)
            {
                Label = dayLabels[i],
                ValueLabel = spend > 0 ? $"₱{spend:N0}" : "",
                Color = SKColor.Parse(colors[i]),
                TextColor = SKColor.Parse("#9898A6"),
                ValueLabelColor = SKColor.Parse(colors[i])
            });
        }

        BarTotalLabel.Text = $"₱{weekTotal:N2}";

        if (weekTotal == 0)
        {
            BarEmptyLabel.IsVisible = true;
            BarChartView.IsVisible = false;
            return;
        }

        BarEmptyLabel.IsVisible = false;
        BarChartView.IsVisible = true;

        BarChartView.Chart = new BarChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 28,
            ValueLabelTextSize = 26,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            AnimationDuration = TimeSpan.FromMilliseconds(500),
            BarAreaAlpha = 210
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  LINE CHART (Microcharts LineChart — 30 day balance)
    // ─────────────────────────────────────────────────────────────
    private void BuildLineChart()
    {
        var today = DateTime.Today;
        var start = today.AddDays(-29);
        LineDateLabel.Text = $"{start:MMM dd} – {today:MMM dd, yyyy}";

        // Prior balance before window
        double running = _vm.Transactions
            .Where(t => t.Date.Date < start)
            .Sum(t => t.IsIncome ? t.Amount : -t.Amount);

        var entries = new List<ChartEntry>();
        var hasData = false;

        for (int i = 0; i < 30; i++)
        {
            var day = start.AddDays(i);
            var net = _vm.Transactions
                .Where(t => t.Date.Date == day)
                .Sum(t => t.IsIncome ? t.Amount : -t.Amount);
            running += net;

            if (net != 0) hasData = true;

            var color = running >= 0
                ? SKColor.Parse("#4ECDC4")
                : SKColor.Parse("#FF6B6B");

            entries.Add(new ChartEntry((float)running)
            {
                Label = i % 7 == 0 ? day.ToString("MMM dd") : "",
                ValueLabel = i == 29 ? $"₱{running:N0}" : "",
                Color = color,
                TextColor = SKColor.Parse("#9898A6"),
                ValueLabelColor = color
            });
        }

        if (!hasData)
        {
            LineEmptyLabel.IsVisible = true;
            LineChartView.IsVisible = false;
            return;
        }

        LineEmptyLabel.IsVisible = false;
        LineChartView.IsVisible = true;

        var first = entries.First().Value;
        var last = entries.Last().Value;
        var trend = last - first;
        LineTrendLabel.Text = trend >= 0
            ? $"↑ ₱{trend:N0}"
            : $"↓ ₱{Math.Abs((decimal)trend):N0}";

        LineChartView.Chart = new LineChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 26,
            ValueLabelTextSize = 28,
            LineMode = LineMode.Spline,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 8,
            AnimationDuration = TimeSpan.FromMilliseconds(700),
            EnableYFadeOutGradient = true
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  ORIGINAL MONTHLY BARS (unchanged from your code)
    // ─────────────────────────────────────────────────────────────
    private void BuildMonthlyChart()
    {
        ChartBarsStack.Children.Clear();
        var now = DateTime.Now;
        ChartMonthLabel.Text = $"{now:MMMM yyyy}";

        var monthlyExpenses = _vm.Transactions
            .Where(t => !t.IsIncome
                     && t.Date.Month == now.Month
                     && t.Date.Year == now.Year)
            .ToList();

        var monthlyTotal = monthlyExpenses.Sum(t => t.Amount);
        ChartTotalLabel.Text = $"₱{monthlyTotal:N2} total";

        if (!monthlyExpenses.Any())
        {
            ChartEmptyLabel.IsVisible = true;
            ChartLegend.IsVisible = false;
            return;
        }

        ChartEmptyLabel.IsVisible = false;
        ChartLegend.IsVisible = true;

        var grouped = _catInfo
            .Select(ci => new
            {
                ci.Name,
                ci.Icon,
                ci.Color,
                ci.Bg,
                Total = monthlyExpenses
                    .Where(t => t.Category == ci.Name)
                    .Sum(t => t.Amount),
                Budget = _vm.GetBudgetForCategory(ci.Name)
                             ?.LimitAmount ?? 0
            })
            .Where(c => c.Total > 0)
            .OrderByDescending(c => c.Total)
            .ToList();

        double maxValue = grouped.Max(c => Math.Max(c.Total, c.Budget));
        if (maxValue <= 0) maxValue = 1;

        foreach (var cat in grouped)
        {
            var isOver = cat.Budget > 0 && cat.Total > cat.Budget;
            var actualPct = cat.Total / maxValue;
            var budgetPct = cat.Budget > 0
                ? Math.Min(cat.Budget / maxValue, 1.0) : 0;
            var barColor = isOver ? "#FF6B6B" : cat.Color;

            var row = new VerticalStackLayout { Spacing = 4 };

            var labelRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            labelRow.Add(new Border
            {
                WidthRequest = 26,
                HeightRequest = 26,
                StrokeShape = new RoundRectangle { CornerRadius = 7 },
                BackgroundColor = Color.FromArgb(cat.Bg),
                Stroke = Colors.Transparent,
                Content = new Label
                {
                    Text = cat.Icon,
                    FontSize = 13,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }, 0, 0);

            labelRow.Add(new Label
            {
                Text = cat.Name,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#37474F"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            var amtStack = new VerticalStackLayout
            { HorizontalOptions = LayoutOptions.End, Spacing = 1 };
            amtStack.Add(new Label
            {
                Text = $"₱{cat.Total:N2}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = isOver
                    ? Color.FromArgb("#C62828")
                    : Color.FromArgb("#1C2B2B"),
                HorizontalOptions = LayoutOptions.End
            });
            if (cat.Budget > 0)
                amtStack.Add(new Label
                {
                    Text = isOver
                        ? $"🚨 +₱{cat.Total - cat.Budget:N0}"
                        : $"₱{cat.Budget - cat.Total:N0} left",
                    FontSize = 9,
                    TextColor = isOver
                        ? Color.FromArgb("#C62828")
                        : Color.FromArgb("#90A4A4"),
                    HorizontalOptions = LayoutOptions.End
                });
            labelRow.Add(amtStack, 2, 0);
            row.Add(labelRow);

            var track = new Grid { HeightRequest = 10 };
            track.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#F1F5F5"),
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                HeightRequest = 10,
                HorizontalOptions = LayoutOptions.Fill
            });

            if (cat.Budget > 0)
            {
                var budgetBar = new Border
                {
                    BackgroundColor = Color.FromArgb("#FFD16666"),
                    StrokeShape = new RoundRectangle { CornerRadius = 5 },
                    HeightRequest = 10,
                    HorizontalOptions = LayoutOptions.Start,
                    WidthRequest = 0
                };
                track.Add(budgetBar);
                track.SizeChanged += (s, e) =>
                    budgetBar.WidthRequest = track.Width * budgetPct;
            }

            var actualBar = new Border
            {
                BackgroundColor = Color.FromArgb(barColor),
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                HeightRequest = 10,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = 0
            };
            track.Add(actualBar);
            track.SizeChanged += (s, e) =>
                actualBar.WidthRequest =
                    track.Width * Math.Min(actualPct, 1.0);

            row.Add(track);
            ChartBarsStack.Children.Add(row);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  TABLE ROWS
    // ─────────────────────────────────────────────────────────────
    private void BuildTableRows()
    {
        TableRowsStack.Children.Clear();

        var cats = _activeFilter != null
            ? _catInfo.Where(c => c.Name == _activeFilter).ToArray()
            : _catInfo;

        TotalCountLabel.Text =
            _vm.Transactions.Count(t => !t.IsIncome).ToString();
        bool alt = false;

        foreach (var ci in cats)
        {
            var catExp = _vm.Transactions
                .Where(e => e.Category == ci.Name && !e.IsIncome)
                .ToList();
            var total = catExp.Sum(e => e.Amount);
            var budget = _vm.GetBudgetForCategory(ci.Name)?.LimitAmount ?? 0;
            var over = budget > 0 && total > budget;
            var pctVal = _vm.TotalExpenses > 0
                ? (total / _vm.TotalExpenses) * 100 : 0;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(new GridLength(2, GridUnitType.Star)),
                    new(new GridLength(1, GridUnitType.Star)),
                    new(new GridLength(1.2, GridUnitType.Star)),
                    new(new GridLength(1, GridUnitType.Star)),
                },
                BackgroundColor = alt
                    ? Color.FromArgb("#F8FAFB") : Colors.White,
                Padding = new Thickness(14, 10)
            };

            var catStack = new HorizontalStackLayout
            { Spacing = 7, VerticalOptions = LayoutOptions.Center };
            catStack.Add(new Border
            {
                WidthRequest = 28,
                HeightRequest = 28,
                StrokeShape = new RoundRectangle { CornerRadius = 7 },
                BackgroundColor = Color.FromArgb(ci.Bg),
                Stroke = Colors.Transparent,
                Content = new Label
                {
                    Text = ci.Icon,
                    FontSize = 15,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            });
            var nameStack = new VerticalStackLayout
            { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            nameStack.Add(new Label
            {
                Text = ci.Name,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#37474F")
            });
            if (budget > 0)
                nameStack.Add(new Label
                {
                    Text = $"{(over ? "🚨 Over" : "Budget")}: ₱{budget:N0}",
                    FontSize = 8,
                    TextColor = over
                        ? Color.FromArgb("#C62828")
                        : Color.FromArgb("#90A4A4")
                });
            catStack.Add(nameStack);
            row.Add(catStack, 0, 0);

            row.Add(new Label
            {
                Text = catExp.Count.ToString(),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#90A4A4"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            row.Add(new Label
            {
                Text = $"₱{total:N2}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = total > 0
                    ? Color.FromArgb(ci.Color)
                    : Color.FromArgb("#E2EAEA"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            var pctStack = new VerticalStackLayout
            { HorizontalOptions = LayoutOptions.End };
            pctStack.Add(new Label
            {
                Text = $"{pctVal:F1}%",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = total > 0
                    ? Color.FromArgb("#1C2B2B")
                    : Color.FromArgb("#E2EAEA"),
                HorizontalOptions = LayoutOptions.End
            });
            if (total > 0)
            {
                var track = new Border
                {
                    BackgroundColor = Color.FromArgb(ci.Color + "22"),
                    StrokeShape = new RoundRectangle { CornerRadius = 2 },
                    HeightRequest = 2,
                    WidthRequest = 48
                };
                var fill = new Border
                {
                    BackgroundColor = Color.FromArgb(ci.Color),
                    StrokeShape = new RoundRectangle { CornerRadius = 2 },
                    HeightRequest = 2,
                    HorizontalOptions = LayoutOptions.Start,
                    WidthRequest = 0
                };
                track.Content = fill;
                track.SizeChanged += (s, e) =>
                    fill.WidthRequest = track.Width * (pctVal / 100.0);
                pctStack.Add(track);
            }
            row.Add(pctStack, 3, 0);

            TableRowsStack.Children.Add(new BoxView
            { Color = Color.FromArgb("#F1F5F5"), HeightRequest = 1 });
            TableRowsStack.Children.Add(row);
            alt = !alt;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  INCOME ROWS (unchanged)
    // ─────────────────────────────────────────────────────────────
    private void BuildIncomeRows()
    {
        IncomeRowsStack.Children.Clear();
        var list = _vm.Transactions.Where(t => t.IsIncome).ToList();

        if (!list.Any())
        {
            IncomeRowsStack.Children.Add(new Label
            {
                Text = "No income recorded yet.",
                FontSize = 12,
                TextColor = Color.FromArgb("#90A4A4")
            });
            return;
        }

        foreach (var t in list)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Star), new(GridLength.Auto)
                },
                Padding = new Thickness(0, 7)
            };
            var info = new VerticalStackLayout { Spacing = 2 };
            info.Add(new Label
            {
                Text = t.Description,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#37474F")
            });
            info.Add(new Label
            {
                Text = t.Date.ToString("MMM dd, yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#90A4A4")
            });
            row.Add(info, 0, 0);
            row.Add(new Label
            {
                Text = $"₱{t.Amount:N2}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2E7D32"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            IncomeRowsStack.Children.Add(new BoxView
            { Color = Color.FromArgb("#2E7D3222"), HeightRequest = 1 });
            IncomeRowsStack.Children.Add(row);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  NET BALANCE (unchanged)
    // ─────────────────────────────────────────────────────────────
    private void UpdateNetBalance()
    {
        var bal = _vm.Balance;
        NetBalanceLabel.Text = bal < 0
            ? $"-₱{Math.Abs(bal):N2}" : $"₱{bal:N2}";
        NetBalanceLabel.TextColor = bal >= 0
            ? Color.FromArgb("#00897B") : Color.FromArgb("#C62828");
        NetBalanceCard.BackgroundColor = bal >= 0
            ? Color.FromArgb("#E0F2F1") : Color.FromArgb("#FFEBEE");
        NetBalanceCard.Stroke = bal >= 0
            ? Color.FromArgb("#00897B") : Color.FromArgb("#C62828");
        NetBalanceMessage.Text = bal >= 0
            ? "Great savings! 🎉" : "Time to cut back 💪";
    }

    private void OnExportClicked(object sender, EventArgs e)
        => _vm.ExportCsvCommand.Execute(null);
}