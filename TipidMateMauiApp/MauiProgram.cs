using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using TipidMateMauiApp.Services;
using TipidMateMauiApp.ViewModels;
using TipidMateMauiApp.Views;

namespace TipidMateMauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMicrocharts()              // ← ADD THIS
            .UseLocalNotification()        // ← ADD THIS
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Services ──────────────────────────────────────────────
        builder.Services.AddSingleton<DatabaseService>();   // SQLite (local cache)
        builder.Services.AddSingleton<FirebaseService>();   // Firebase Auth + Firestore
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<NotificationService>(); // ← ADD THIS
        builder.Services.AddSingleton<RecurringTransactionService>();

        // ── Pages ─────────────────────────────────────────────────
        builder.Services.AddSingleton<App>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AddEntryPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SummaryPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<BudgetPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
