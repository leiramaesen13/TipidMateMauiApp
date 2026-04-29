using TipidMateMauiApp.Services;
using TipidMateMauiApp.Views;

namespace TipidMateMauiApp;

public partial class App : Application
{
    public App(LoginPage loginPage)
    {
        InitializeComponent();
        MainPage = loginPage;
    }

    protected override async void OnStart()
    {
        base.OnStart();
        try
        {
            var recurring = IPlatformApplication.Current?.Services
                                .GetService<RecurringTransactionService>();
            if (recurring != null)
            {
                await recurring.InitAsync();
                var count = await recurring.ProcessDueTransactionsAsync();
                await recurring.NotifyProcessedAsync(count);
                await recurring.ScheduleDailyReminderAsync(hour: 8, minute: 0);
            }
        }
        catch { }
    }
}