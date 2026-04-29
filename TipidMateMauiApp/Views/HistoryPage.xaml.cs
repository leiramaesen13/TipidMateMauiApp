using Microsoft.Maui.Controls.Shapes;
using TipidMateMauiApp.Models;
using TipidMateMauiApp.ViewModels;

namespace TipidMateMauiApp.Views;

public partial class HistoryPage : ContentPage
{
    private readonly MainViewModel _vm;

    private readonly (string Name, string Icon, string Color)[] _categories =
    {
        ("Food & Dining",    "🍜","#E53935"),
        ("Transport",        "🚌","#1565C0"),
        ("Bills & Utilities","⚡","#F57F17"),
        ("Shopping",         "🛍️","#6A1B9A"),
        ("Health",           "❤️","#AD1457"),
        ("Entertainment",    "🎬","#00838F"),
        ("Savings",          "💰","#2E7D32"),
        ("Other",            "📦","#4E342E"),
    };

    public HistoryPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
        BuildCategoryPills();
        UpdateStatBar();
    }

    private void BuildCategoryPills()
    {
        CategoryPillsStack.Children.Clear();
        CategoryPillsStack.Children.Add(MakePill("All", "All", "#00897B", _vm.FilterCategory == "All"));
        foreach (var cat in _categories)
            CategoryPillsStack.Children.Add(MakePill(cat.Name, $"{cat.Icon} {cat.Name.Split(" ")[0]}", cat.Color, _vm.FilterCategory == cat.Name));
    }

    private Border MakePill(string category, string label, string color, bool active)
    {
        var pill = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Stroke = Colors.Transparent,
            BackgroundColor = active ? Color.FromArgb(color) : Color.FromArgb("#F1F5F5"),
            Padding = new Thickness(13, 5)
        };
        if (active) pill.Shadow = new Shadow { Brush = Color.FromArgb(color), Opacity = 0.3f, Radius = 5, Offset = new Point(0, 2) };

        var lbl = new Label { Text = label, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = active ? Colors.White : Color.FromArgb("#90A4A4") };
        pill.Content = lbl;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => { _vm.FilterCategory = category; BuildCategoryPills(); UpdateStatBar(); };
        pill.GestureRecognizers.Add(tap);
        return pill;
    }

    private void SetTypePill(string type)
    {
        _vm.FilterType = type;
        PillAll.BackgroundColor = Color.FromArgb("#F1F5F5");
        PillExpense.BackgroundColor = Color.FromArgb("#F1F5F5");
        PillIncome.BackgroundColor = Color.FromArgb("#F1F5F5");
        ((Label)PillAll.Content).TextColor = Color.FromArgb("#90A4A4");
        ((Label)PillExpense.Content).TextColor = Color.FromArgb("#90A4A4");
        ((Label)PillIncome.Content).TextColor = Color.FromArgb("#90A4A4");

        switch (type)
        {
            case "All":
                PillAll.BackgroundColor = Color.FromArgb("#00897B");
                ((Label)PillAll.Content).TextColor = Colors.White; break;
            case "expense":
                PillExpense.BackgroundColor = Color.FromArgb("#C62828");
                ((Label)PillExpense.Content).TextColor = Colors.White; break;
            case "income":
                PillIncome.BackgroundColor = Color.FromArgb("#2E7D32");
                ((Label)PillIncome.Content).TextColor = Colors.White; break;
        }
        UpdateStatBar();
    }

    private void OnFilterAll(object s, EventArgs e) => SetTypePill("All");
    private void OnFilterExpense(object s, EventArgs e) => SetTypePill("expense");
    private void OnFilterIncome(object s, EventArgs e) => SetTypePill("income");

    private void UpdateStatBar()
    {
        ShownCountLabel.Text = _vm.FilteredCount.ToString();
        TotalInLabel.Text = $"₱{_vm.FilteredTotalIn:N0}";
        TotalOutLabel.Text = $"₱{_vm.FilteredTotalOut:N0}";
    }

    private void OnDateFromSelected(object sender, DateChangedEventArgs e)
    { _vm.FilterDateFrom = e.NewDate; ClearFiltersBtn.IsVisible = true; UpdateStatBar(); }

    private void OnDateToSelected(object sender, DateChangedEventArgs e)
    { _vm.FilterDateTo = e.NewDate; ClearFiltersBtn.IsVisible = true; UpdateStatBar(); }

    private void OnClearFiltersClicked(object sender, EventArgs e)
    {
        _vm.SearchQuery = ""; _vm.FilterCategory = "All"; _vm.FilterType = "All";
        _vm.FilterDateFrom = null; _vm.FilterDateTo = null;
        DateFromPicker.Date = DateTime.Today; DateToPicker.Date = DateTime.Today;
        ClearFiltersBtn.IsVisible = false;
        BuildCategoryPills(); SetTypePill("All"); UpdateStatBar();
    }

    private void OnEditTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Transaction t) { _vm.PrepareEditForm(t); Shell.Current.GoToAsync("AddEntryPage"); }
    }

    private async void OnDeleteTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Transaction t)
        {
            bool ok = await DisplayAlert("Delete", $"Delete \"{t.Description}\"?", "Delete", "Cancel");
            if (ok) { await _vm.DeleteTransactionAsync(t); UpdateStatBar(); }
        }
    }

    private void OnExportClicked(object sender, EventArgs e) => _vm.ExportCsvCommand.Execute(null);
}