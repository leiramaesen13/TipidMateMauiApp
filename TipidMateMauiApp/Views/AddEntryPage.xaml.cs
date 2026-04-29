using Microsoft.Maui.Controls.Shapes;
using TipidMateMauiApp.Models;
using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class AddEntryPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly RecurringTransactionService _recurring;
    private string _selectedCategory = "Food & Dining";
    private string _selectedFrequency = "weekly";

    private readonly (string Name, string Icon, string Color, string Bg)[] _categories =
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

    public AddEntryPage(MainViewModel vm, RecurringTransactionService recurring)
    {
        InitializeComponent();
        _vm = vm;
        _recurring = recurring;
        BindingContext = _vm;
        BuildCategoryGrid();
        SetTypeUI("expense");
        UpdateNextRunPreview();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.EditingTransaction != null)
        {
            _selectedCategory = _vm.FormCategory;
            SetTypeUI(_vm.FormType);
            CancelBtn.IsVisible = true;
            SaveBtn.Text = "✓  Update Entry";
            PageTitleLabel.Text = "Edit Entry";
            BuildCategoryGrid();
        }
        else
        {
            CancelBtn.IsVisible = false;
            SaveBtn.Text = "✓  Save Entry";
            PageTitleLabel.Text = "New Entry";
            RecurringSwitch.IsToggled = false;
            FrequencyPanel.IsVisible = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  CATEGORY GRID
    // ─────────────────────────────────────────────────────────────
    private void BuildCategoryGrid()
    {
        CategoryGrid.Children.Clear();
        for (int i = 0; i < _categories.Length; i++)
        {
            var cat = _categories[i];
            CategoryGrid.Add(
                MakeCategoryBtn(cat.Name, cat.Icon, cat.Color, cat.Bg),
                i % 4, i / 4);
        }
    }

    private Border MakeCategoryBtn(string name, string icon,
                                   string color, string bg)
    {
        var active = name == _selectedCategory;
        var catColor = Color.FromArgb(color);

        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            Stroke = active ? catColor : Color.FromArgb("#E2EAEA"),
            StrokeThickness = active ? 2 : 1,
            BackgroundColor = active ? Color.FromArgb(bg) : Colors.White,
            Padding = new Thickness(3, 9),
        };
        if (active)
            border.Shadow = new Shadow
            {
                Brush = catColor,
                Opacity = 0.25f,
                Radius = 6,
                Offset = new Point(0, 2)
            };

        var stack = new VerticalStackLayout
        { Spacing = 3, HorizontalOptions = LayoutOptions.Center };
        stack.Add(new Label
        { Text = icon, FontSize = 20, HorizontalOptions = LayoutOptions.Center });
        stack.Add(new Label
        {
            Text = name.Split(" ")[0],
            FontSize = 8,
            FontAttributes = FontAttributes.Bold,
            TextColor = active ? catColor : Color.FromArgb("#90A4A4"),
            HorizontalOptions = LayoutOptions.Center,
        });
        border.Content = stack;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            _selectedCategory = name;
            _vm.FormCategory = name;
            BuildCategoryGrid();
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }

    // ─────────────────────────────────────────────────────────────
    //  TYPE UI
    // ─────────────────────────────────────────────────────────────
    private void SetTypeUI(string type)
    {
        _vm.FormType = type;
        if (type == "expense")
        {
            ExpenseBtnInner.BackgroundColor = Color.FromArgb("#C62828");
            ExpenseBtnInner.TextColor = Colors.White;
            IncomeBtnInner.BackgroundColor = Color.FromArgb("#F1F5F5");
            IncomeBtnInner.TextColor = Color.FromArgb("#90A4A4");
            AmountEntry.TextColor = Color.FromArgb("#C62828");
            AmountUnderline.Color = Color.FromArgb("#C62828");
            SaveBtnBorder.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb("#C62828"), 0),
                    new(Color.FromArgb("#B71C1C"), 1)
                }, new Point(0, 0), new Point(1, 0));
        }
        else
        {
            IncomeBtnInner.BackgroundColor = Color.FromArgb("#2E7D32");
            IncomeBtnInner.TextColor = Colors.White;
            ExpenseBtnInner.BackgroundColor = Color.FromArgb("#F1F5F5");
            ExpenseBtnInner.TextColor = Color.FromArgb("#90A4A4");
            AmountEntry.TextColor = Color.FromArgb("#2E7D32");
            AmountUnderline.Color = Color.FromArgb("#2E7D32");
            SaveBtnBorder.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb("#2E7D32"), 0),
                    new(Color.FromArgb("#1B5E20"), 1)
                }, new Point(0, 0), new Point(1, 0));
        }
    }

    private void OnExpenseClicked(object sender, EventArgs e) => SetTypeUI("expense");
    private void OnIncomeClicked(object sender, EventArgs e) => SetTypeUI("income");

    // ─────────────────────────────────────────────────────────────
    //  RECURRING SWITCH
    // ─────────────────────────────────────────────────────────────
    private void OnRecurringSwitchToggled(object sender, ToggledEventArgs e)
    {
        FrequencyPanel.IsVisible = e.Value;
        if (e.Value) UpdateNextRunPreview();
    }

    // ─────────────────────────────────────────────────────────────
    //  FREQUENCY SELECTION
    // ─────────────────────────────────────────────────────────────
    private void OnFrequencyTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string freq) return;
        _selectedFrequency = freq;
        HighlightFrequency(freq);
        UpdateNextRunPreview();
    }

    private void HighlightFrequency(string freq)
    {
        // Reset all
        var buttons = new[]
        {
            (DailyBtn,   "daily"),
            (WeeklyBtn,  "weekly"),
            (MonthlyBtn, "monthly")
        };

        foreach (var (btn, f) in buttons)
        {
            var active = f == freq;
            btn.BackgroundColor = active
                ? Color.FromArgb("#E8FBF8")
                : Color.FromArgb("#F8FAFB");
            btn.Stroke = active
                ? Color.FromArgb("#4ECDC4")
                : Color.FromArgb("#E2EAEA");

            if (btn.Content is VerticalStackLayout stack)
            {
                foreach (var child in stack.Children)
                {
                    if (child is Label lbl && lbl.FontSize == 11)
                        lbl.TextColor = active
                            ? Color.FromArgb("#4ECDC4")
                            : Color.FromArgb("#9898A6");
                }
            }
        }
    }

    private void UpdateNextRunPreview()
    {
        var next = _selectedFrequency switch
        {
            "daily" => DateTime.Today.AddDays(1),
            "weekly" => DateTime.Today.AddDays(7),
            "monthly" => DateTime.Today.AddMonths(1),
            _ => DateTime.Today.AddMonths(1)
        };

        var freqLabel = _selectedFrequency switch
        {
            "daily" => "every day",
            "weekly" => "every week",
            "monthly" => "every month",
            _ => "monthly"
        };

        NextRunPreviewLabel.Text =
            $"🔁 Repeats {freqLabel} — next: {next:MMM dd, yyyy}";
    }

    // ─────────────────────────────────────────────────────────────
    //  SAVE
    // ─────────────────────────────────────────────────────────────
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.FormDescription))
        {
            await DisplayAlert("Missing Info",
                "Please enter a description.", "OK");
            return;
        }
        if (!double.TryParse(_vm.FormAmount, out double amt) || amt <= 0)
        {
            await DisplayAlert("Invalid Amount",
                "Please enter a valid amount.", "OK");
            return;
        }

        // Save the one-time transaction as usual
        await _vm.SaveTransactionAsync();

        // Also save as recurring if toggled
        if (RecurringSwitch.IsToggled)
        {
            var r = new RecurringTransaction
            {
                Description = _vm.FormDescription.Trim(),
                Category = _selectedCategory,
                Type = _vm.FormType,
                Amount = amt,
                Frequency = _selectedFrequency,
                StartDate = _vm.FormDate,
                NextRunDate = _selectedFrequency switch
                {
                    "daily" => _vm.FormDate.AddDays(1),
                    "weekly" => _vm.FormDate.AddDays(7),
                    "monthly" => _vm.FormDate.AddMonths(1),
                    _ => _vm.FormDate.AddMonths(1)
                },
                IsActive = true
            };
            await _recurring.SaveAsync(r);
            await DisplayAlert("✅ Recurring Set",
                $"This transaction will auto-add {r.FrequencyLabel.ToLower()}. " +
                $"Next: {r.NextRunDate:MMM dd, yyyy}", "OK");
        }

        await Shell.Current.GoToAsync("..");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        _vm.ResetForm();
        Shell.Current.GoToAsync("..");
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        _vm.ResetForm();
        Shell.Current.GoToAsync("..");
    }
}