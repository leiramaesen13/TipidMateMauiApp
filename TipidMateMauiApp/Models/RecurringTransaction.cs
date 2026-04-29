using SQLite;

namespace TipidMateMauiApp.Models
{
    [Table("RecurringTransactions")]
    public class RecurringTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string? FirebaseId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Type { get; set; } = "expense";
        public double Amount { get; set; }
        public string Frequency { get; set; } = "monthly"; // daily/weekly/monthly
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime NextRunDate { get; set; } = DateTime.Today;
        public bool IsActive { get; set; } = true;
        public int RunCount { get; set; } = 0;

        [Ignore] public bool IsIncome => Type == "income";
        [Ignore]
        public string AmountDisplay =>
            IsIncome ? $"+₱{Amount:N2}" : $"-₱{Amount:N2}";
        [Ignore]
        public string FrequencyIcon => Frequency switch
        {
            "daily" => "📅",
            "weekly" => "📆",
            "monthly" => "🗓️",
            _ => "🔁"
        };
        [Ignore]
        public string FrequencyLabel => Frequency switch
        {
            "daily" => "Every day",
            "weekly" => "Every week",
            "monthly" => "Every month",
            _ => Frequency
        };
        [Ignore]
        public string CategoryIcon => Category switch
        {
            "Food & Dining" => "🍜",
            "Transport" => "🚌",
            "Bills & Utilities" => "💡",
            "Shopping" => "🛒",
            "Health" => "💊",
            "Entertainment" => "🎮",
            "Savings" => "🏦",
            _ => "📦",
        };
        [Ignore]
        public string NextRunDisplay =>
            NextRunDate.Date == DateTime.Today
                ? "Today"
                : NextRunDate.Date == DateTime.Today.AddDays(1)
                    ? "Tomorrow"
                    : NextRunDate.ToString("MMM dd, yyyy");

        // Calculate next run date based on frequency
        public DateTime GetNextRunDate(DateTime from) => Frequency switch
        {
            "daily" => from.AddDays(1),
            "weekly" => from.AddDays(7),
            "monthly" => from.AddMonths(1),
            _ => from.AddMonths(1)
        };
    }
}