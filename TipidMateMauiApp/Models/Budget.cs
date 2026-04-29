using SQLite;

namespace TipidMateMauiApp.Models
{
    [Table("Budgets")]
    public class Budget
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public double LimitAmount { get; set; }

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