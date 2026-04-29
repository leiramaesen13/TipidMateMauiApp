namespace TipidMateMauiApp;

public partial class AppShell : Shell
{
    public AppShell(string userName = "User")
    {
        InitializeComponent();
        Routing.RegisterRoute("AddEntryPage", typeof(Views.AddEntryPage));
        Routing.RegisterRoute("BudgetPage", typeof(Views.BudgetPage));
    }
}