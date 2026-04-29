using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TipidMateMauiApp.Models;
using TipidMateMauiApp.Services;

namespace TipidMateMauiApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db;
        private readonly FirebaseService _firebase;

        public MainViewModel(DatabaseService db, FirebaseService firebase)
        {
            _db = db;
            _firebase = firebase;

            Transactions = new ObservableCollection<Transaction>();
            Budgets = new ObservableCollection<Budget>();
            Categories = new List<string>
            {
                "Food & Dining", "Transport", "Bills & Utilities",
                "Shopping", "Health", "Entertainment", "Savings", "Other"
            };

            LoadDataCommand = new Command(async () => await LoadDataAsync());
            SaveTransactionCommand = new Command(async () => await SaveTransactionAsync());
            DeleteTransactionCommand = new Command<Transaction>(async t => await DeleteTransactionAsync(t));
            SaveBudgetCommand = new Command(async () => await SaveBudgetsAsync());
            ExportCsvCommand = new Command(async () => await ExportCsvAsync());
        }

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<Transaction> Transactions { get; }
        public ObservableCollection<Budget> Budgets { get; }
        public List<string> Categories { get; }

        // ── User info ─────────────────────────────────────────────
        public string UserDisplayName => _firebase.DisplayName ?? _firebase.UserEmail ?? "User";
        public string UserEmail => _firebase.UserEmail ?? string.Empty;

        // ── Computed totals (month-scoped) ────────────────────────
        public double TotalIncome =>
            Transactions
                .Where(t => t.IsIncome
                         && t.Date.Year == DateTime.Now.Year
                         && t.Date.Month == DateTime.Now.Month)
                .Sum(t => t.Amount);

        public double TotalExpenses =>
            Transactions
                .Where(t => !t.IsIncome
                         && t.Date.Year == DateTime.Now.Year
                         && t.Date.Month == DateTime.Now.Month)
                .Sum(t => t.Amount);

        public double Balance => TotalIncome - TotalExpenses;
        public double SavingsRate => TotalIncome > 0 ? (Balance / TotalIncome) * 100 : 0;

        public string CurrentMonthDisplay => DateTime.Now.ToString("MMMM yyyy");
        public string BalanceDisplay => $"₱{Balance:N2}";
        public string TotalIncomeDisplay => $"₱{TotalIncome:N2}";
        public string TotalExpenseDisplay => $"₱{TotalExpenses:N2}";
        public string SavingsRateDisplay => $"{SavingsRate:F0}%";
        public string SavingsMessage => SavingsRate >= 20 ? "Great job saving! 🎉"
                                      : SavingsRate >= 10 ? "Keep it up! 💪"
                                      : "Try to save more 📈";
        public Color BalanceColor => Balance >= 0
                                   ? Color.FromArgb("#4ECDC4")
                                   : Color.FromArgb("#FF6B6B");

        // ── Form fields ───────────────────────────────────────────
        private Transaction? _editingTransaction;
        public Transaction? EditingTransaction
        {
            get => _editingTransaction;
            set { _editingTransaction = value; OnPropertyChanged(); }
        }

        private string _formDescription = string.Empty;
        public string FormDescription
        {
            get => _formDescription;
            set { _formDescription = value; OnPropertyChanged(); }
        }

        private string _formCategory = "Food & Dining";
        public string FormCategory
        {
            get => _formCategory;
            set { _formCategory = value; OnPropertyChanged(); }
        }

        private string _formType = "expense";
        public string FormType
        {
            get => _formType;
            set { _formType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsExpense)); }
        }
        public bool IsExpense => FormType == "expense";

        private string _formAmount = string.Empty;
        public string FormAmount
        {
            get => _formAmount;
            set { _formAmount = value; OnPropertyChanged(); }
        }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate
        {
            get => _formDate;
            set { _formDate = value; OnPropertyChanged(); }
        }

        // ── Filter / Search (TransactionsPage) ───────────────────
        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(); RefreshFilteredTransactions(); }
        }

        private string _filterCategory = "All";
        public string FilterCategory
        {
            get => _filterCategory;
            set { _filterCategory = value; OnPropertyChanged(); RefreshFilteredTransactions(); }
        }

        private string _filterType = "All";
        public string FilterType
        {
            get => _filterType;
            set { _filterType = value; OnPropertyChanged(); RefreshFilteredTransactions(); }
        }

        private DateTime? _filterDateFrom;
        public DateTime? FilterDateFrom
        {
            get => _filterDateFrom;
            set { _filterDateFrom = value; OnPropertyChanged(); RefreshFilteredTransactions(); }
        }

        private DateTime? _filterDateTo;
        public DateTime? FilterDateTo
        {
            get => _filterDateTo;
            set { _filterDateTo = value; OnPropertyChanged(); RefreshFilteredTransactions(); }
        }

        private ObservableCollection<Transaction> _filteredTransactions = new();
        public ObservableCollection<Transaction> FilteredTransactions
        {
            get => _filteredTransactions;
            set { _filteredTransactions = value; OnPropertyChanged(); }
        }

        public void RefreshFilteredTransactions()
        {
            var filtered = Transactions.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
                filtered = filtered.Where(t =>
                    t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

            if (FilterCategory != "All")
                filtered = filtered.Where(t => t.Category == FilterCategory);

            if (FilterType != "All")
                filtered = filtered.Where(t => t.Type == FilterType);

            if (FilterDateFrom.HasValue)
                filtered = filtered.Where(t => t.Date.Date >= FilterDateFrom.Value.Date);

            if (FilterDateTo.HasValue)
                filtered = filtered.Where(t => t.Date.Date <= FilterDateTo.Value.Date);

            FilteredTransactions = new ObservableCollection<Transaction>(filtered);
            OnPropertyChanged(nameof(FilteredTotalIn));
            OnPropertyChanged(nameof(FilteredTotalOut));
            OnPropertyChanged(nameof(FilteredCount));
        }

        public double FilteredTotalIn => FilteredTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        public double FilteredTotalOut => FilteredTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
        public int FilteredCount => FilteredTransactions.Count;

        // ── Chart tap filter (SummaryPage only) ──────────────────
        // Separate from FilteredTransactions so the two pages
        // never interfere with each other's filter state.

        private string? _chartFilterCategory;
        public string? ChartFilterCategory
        {
            get => _chartFilterCategory;
            set
            {
                _chartFilterCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChartFilterHeader));
                OnPropertyChanged(nameof(ChartFilteredTransactions));
            }
        }

        /// <summary>
        /// Header text shown above the filtered list on SummaryPage.
        /// </summary>
        public string ChartFilterHeader => ChartFilterCategory == null
            ? "All Expenses This Month"
            : $"📂 {ChartFilterCategory}";

        /// <summary>
        /// Expenses for the current month, optionally filtered to one category.
        /// Used by SummaryPage's transaction list beneath the charts.
        /// </summary>
        public IEnumerable<Transaction> ChartFilteredTransactions
        {
            get
            {
                var now = DateTime.Now;
                var source = Transactions.Where(t =>
                    !t.IsIncome &&
                    t.Date.Month == now.Month &&
                    t.Date.Year == now.Year);

                return ChartFilterCategory == null
                    ? source.OrderByDescending(t => t.Date)
                    : source.Where(t => t.Category == ChartFilterCategory)
                            .OrderByDescending(t => t.Date);
            }
        }

        /// <summary>
        /// Set or clear the chart category filter.
        /// Pass null to show all expenses.
        /// </summary>
        public void SetChartFilter(string? category)
        {
            ChartFilterCategory = category;
        }

        // ── Budget helpers ────────────────────────────────────────
        public double GetSpentForCategory(string category) =>
            GetSpentForCategoryInMonth(category, DateTime.Now.Year, DateTime.Now.Month);

        public double GetSpentForCategoryInMonth(string category, int year, int month) =>
            Transactions
                .Where(t => t.Category == category && !t.IsIncome
                         && t.Date.Year == year && t.Date.Month == month)
                .Sum(t => t.Amount);

        public Budget? GetBudgetForCategory(string category) =>
            Budgets.FirstOrDefault(b => b.Category == category);

        public List<CategorySummary> CategorySummaries =>
            Categories.Select(cat =>
            {
                var spent = GetSpentForCategoryInMonth(cat, DateTime.Now.Year, DateTime.Now.Month);
                return new CategorySummary
                {
                    Category = cat,
                    Icon = new Transaction { Category = cat }.CategoryIcon,
                    Total = spent,
                    Count = Transactions.Count(t =>
                                t.Category == cat && !t.IsIncome
                             && t.Date.Year == DateTime.Now.Year
                             && t.Date.Month == DateTime.Now.Month),
                    Percentage = TotalExpenses > 0 ? (spent / TotalExpenses) * 100 : 0,
                    Budget = GetBudgetForCategory(cat)?.LimitAmount ?? 0,
                    Color = GetCategoryColor(cat)
                };
            }).Where(c => c.Total > 0).OrderByDescending(c => c.Total).ToList();

        public static string GetCategoryColor(string cat) => cat switch
        {
            "Food & Dining" => "#FF6B6B",
            "Transport" => "#4ECDC4",
            "Bills & Utilities" => "#FFD166",
            "Shopping" => "#06D6A0",
            "Health" => "#FF8B94",
            "Entertainment" => "#B4A7E5",
            "Savings" => "#74B9FF",
            _ => "#D4A574",
        };

        // ── Commands ──────────────────────────────────────────────
        public ICommand LoadDataCommand { get; }
        public ICommand SaveTransactionCommand { get; }
        public ICommand DeleteTransactionCommand { get; }
        public ICommand SaveBudgetCommand { get; }
        public ICommand ExportCsvCommand { get; }

        // ─────────────────────────────────────────────────────────
        //  LOAD DATA  –  Firebase first, SQLite fallback
        // ─────────────────────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            List<Transaction> transactions;
            List<Budget> budgets;

            if (_firebase.IsLoggedIn)
            {
                transactions = await _firebase.GetTransactionsAsync();
                budgets = await _firebase.GetBudgetsAsync();

                // Fallback to SQLite if Firebase returned nothing (offline)
                if (transactions.Count == 0 && budgets.Count == 0)
                {
                    transactions = await _db.GetTransactionsAsync();
                    budgets = await _db.GetBudgetsAsync();
                }
                else
                {
                    // Keep SQLite in sync as local cache
                    foreach (var t in transactions) await _db.SaveTransactionAsync(t);
                    foreach (var b in budgets) await _db.SaveBudgetAsync(b);
                }
            }
            else
            {
                // Not logged in → use local SQLite
                transactions = await _db.GetTransactionsAsync();
                budgets = await _db.GetBudgetsAsync();
            }

            Transactions.Clear();
            foreach (var t in transactions) Transactions.Add(t);

            Budgets.Clear();
            foreach (var b in budgets) Budgets.Add(b);

            RefreshFilteredTransactions();
            RefreshTotals();
        }

        // ─────────────────────────────────────────────────────────
        //  SAVE TRANSACTION  –  Firebase + SQLite
        // ─────────────────────────────────────────────────────────
        public async Task SaveTransactionAsync()
        {
            if (string.IsNullOrWhiteSpace(FormDescription)) return;
            if (!double.TryParse(FormAmount, out double amount) || amount <= 0) return;

            var t = EditingTransaction ?? new Transaction();
            t.Description = FormDescription.Trim();
            t.Category = FormCategory;
            t.Type = FormType;
            t.Amount = amount;
            t.Date = FormDate;

            // Save locally first (always works offline)
            await _db.SaveTransactionAsync(t);

            // Then push to Firebase if online
            if (_firebase.IsLoggedIn)
            {
                var fbId = await _firebase.SaveTransactionAsync(t);
                if (fbId != null)
                {
                    t.FirebaseId = fbId;
                    t.IsSynced = true;
                    await _db.SaveTransactionAsync(t);
                }
            }

            await LoadDataAsync();
            ResetForm();
        }

        public async Task DeleteTransactionAsync(Transaction t)
        {
            await _db.DeleteTransactionAsync(t);
            if (_firebase.IsLoggedIn && !string.IsNullOrEmpty(t.FirebaseId))
                await _firebase.DeleteTransactionAsync(t.FirebaseId);
            await LoadDataAsync();
        }

        public async Task SaveBudgetsAsync()
        {
            foreach (var b in Budgets)
            {
                await _db.SaveBudgetAsync(b);
                if (_firebase.IsLoggedIn)
                    await _firebase.SaveBudgetAsync(b);
            }
            await LoadDataAsync();
        }

        // ─────────────────────────────────────────────────────────
        //  MIGRATE SQLITE → FIREBASE (called once after first login)
        // ─────────────────────────────────────────────────────────
        public async Task MigrateSqliteToFirebaseAsync()
        {
            if (!_firebase.IsLoggedIn) return;

            var transactions = await _db.GetTransactionsAsync();
            var budgets = await _db.GetBudgetsAsync();

            var unsynced = transactions.Where(t => !t.IsSynced).ToList();
            foreach (var t in unsynced)
            {
                var fbId = await _firebase.SaveTransactionAsync(t);
                if (fbId != null)
                    await _db.MarkTransactionSyncedAsync(t.Id, fbId);
            }

            foreach (var b in budgets)
                await _firebase.SaveBudgetAsync(b);
        }

        // ─────────────────────────────────────────────────────────
        //  EXPORT CSV
        // ─────────────────────────────────────────────────────────
        public async Task ExportCsvAsync()
        {
            var lines = new List<string> { "Date,Description,Category,Type,Amount" };
            lines.AddRange(FilteredTransactions.Select(t =>
                $"{t.Date:yyyy-MM-dd},\"{t.Description}\",{t.Category},{t.Type},{t.Amount:F2}"));

            var csv = string.Join("\n", lines);
            var path = Path.Combine(FileSystem.CacheDirectory, "TipidMate_Export.csv");
            await File.WriteAllTextAsync(path, csv);
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "TipidMate Export",
                File = new ShareFile(path)
            });
        }

        // ── Form helpers ──────────────────────────────────────────
        public void PrepareEditForm(Transaction t)
        {
            EditingTransaction = t;
            FormDescription = t.Description;
            FormCategory = t.Category;
            FormType = t.Type;
            FormAmount = t.Amount.ToString("F2");
            FormDate = t.Date;
        }

        public void ResetForm()
        {
            EditingTransaction = null;
            FormDescription = string.Empty;
            FormCategory = "Food & Dining";
            FormType = "expense";
            FormAmount = string.Empty;
            FormDate = DateTime.Today;
        }

        // ─────────────────────────────────────────────────────────
        //  REFRESH TOTALS
        // ─────────────────────────────────────────────────────────
        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(TotalIncome));
            OnPropertyChanged(nameof(TotalExpenses));
            OnPropertyChanged(nameof(Balance));
            OnPropertyChanged(nameof(SavingsRate));
            OnPropertyChanged(nameof(BalanceDisplay));
            OnPropertyChanged(nameof(TotalIncomeDisplay));
            OnPropertyChanged(nameof(TotalExpenseDisplay));
            OnPropertyChanged(nameof(SavingsRateDisplay));
            OnPropertyChanged(nameof(SavingsMessage));
            OnPropertyChanged(nameof(BalanceColor));
            OnPropertyChanged(nameof(CategorySummaries));
            OnPropertyChanged(nameof(CurrentMonthDisplay));
            OnPropertyChanged(nameof(UserDisplayName));
            OnPropertyChanged(nameof(UserEmail));

            // Keep SummaryPage list in sync after a data reload
            OnPropertyChanged(nameof(ChartFilteredTransactions));
            OnPropertyChanged(nameof(ChartFilterHeader));
        }

        // ── INotifyPropertyChanged ────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Helper class for SummaryPage ─────────────────────────────
    public class CategorySummary
    {
        public string? Category { get; set; }
        public string? Icon { get; set; }
        public double Total { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public double Budget { get; set; }
        public string? Color { get; set; }

        public string TotalDisplay => $"₱{Total:N2}";
        public string PercentageDisplay => $"{Percentage:F1}%";
        public string BudgetDisplay => Budget > 0 ? $"₱{Budget:N2}" : "—";
        public bool HasBudget => Budget > 0;
        public bool IsOverBudget => Budget > 0 && Total > Budget;
        public double ProgressValue => Budget > 0 ? Math.Min(Total / Budget, 1.0) : 0;
    }
}