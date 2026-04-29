using SQLite;

namespace TipidMateMauiApp.Models
{
    [Table("Transactions")]
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Firebase Firestore document ID (null for local-only records)
        public string? FirebaseId { get; set; }

        // Track whether this local record has been synced to Firebase
        public bool IsSynced { get; set; } = false;

        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Type { get; set; } = "expense";
        public double Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;

        [Ignore] public bool IsIncome => Type == "income";
        [Ignore] public string AmountDisplay => IsIncome ? $"+₱{Amount:N2}" : $"-₱{Amount:N2}";
        [Ignore] public string DateDisplay => Date.ToString("MMM dd, yyyy");

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
    }
}