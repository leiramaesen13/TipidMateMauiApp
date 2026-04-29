using SQLite;
using TipidMateMauiApp.Models;

namespace TipidMateMauiApp.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;

        public async Task InitAsync()
        {
            if (_db != null) return;
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tipidmate.db3");
            _db = new SQLiteAsyncConnection(dbPath);
            await _db.CreateTableAsync<Transaction>();
            await _db.CreateTableAsync<Budget>();

            // Add new columns if upgrading from old version (migration safety)
            try { await _db.ExecuteAsync("ALTER TABLE Transactions ADD COLUMN FirebaseId TEXT"); } catch { }
            try { await _db.ExecuteAsync("ALTER TABLE Transactions ADD COLUMN IsSynced INTEGER DEFAULT 0"); } catch { }
        }

        // ── TRANSACTIONS ─────────────────────────────────────────

        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            await InitAsync();
            return await _db!.Table<Transaction>()
                             .OrderByDescending(t => t.Date)
                             .ToListAsync();
        }

        public async Task<List<Transaction>> GetUnsyncedTransactionsAsync()
        {
            await InitAsync();
            return await _db!.Table<Transaction>()
                             .Where(t => !t.IsSynced)
                             .ToListAsync();
        }

        public async Task<int> SaveTransactionAsync(Transaction t)
        {
            await InitAsync();
            return t.Id == 0 ? await _db!.InsertAsync(t) : await _db!.UpdateAsync(t);
        }

        public async Task<int> DeleteTransactionAsync(Transaction t)
        {
            await InitAsync();
            return await _db!.DeleteAsync(t);
        }

        public async Task DeleteAllTransactionsAsync()
        {
            await InitAsync();
            await _db!.DeleteAllAsync<Transaction>();
        }

        public async Task MarkTransactionSyncedAsync(int localId, string firebaseId)
        {
            await InitAsync();
            var t = await _db!.Table<Transaction>().Where(x => x.Id == localId).FirstOrDefaultAsync();
            if (t != null)
            {
                t.FirebaseId = firebaseId;
                t.IsSynced = true;
                await _db.UpdateAsync(t);
            }
        }

        // ── BUDGETS ──────────────────────────────────────────────

        public async Task<List<Budget>> GetBudgetsAsync()
        {
            await InitAsync();
            return await _db!.Table<Budget>().ToListAsync();
        }

        public async Task SaveBudgetAsync(Budget b)
        {
            await InitAsync();
            var existing = await _db!.Table<Budget>()
                .Where(x => x.Category == b.Category)
                .FirstOrDefaultAsync();
            if (existing == null) await _db.InsertAsync(b);
            else { b.Id = existing.Id; await _db.UpdateAsync(b); }
        }

        public async Task DeleteBudgetAsync(string category)
        {
            await InitAsync();
            var existing = await _db!.Table<Budget>()
                .Where(x => x.Category == category)
                .FirstOrDefaultAsync();
            if (existing != null) await _db.DeleteAsync(existing);
        }

        // ── MIGRATION FLAG ───────────────────────────────────────

        /// <summary>Returns true if SQLite data has already been migrated to Firebase.</summary>
        public async Task<bool> HasMigratedToFirebaseAsync()
        {
            await InitAsync();
            var count = await _db!.Table<Transaction>().Where(t => t.IsSynced).CountAsync();
            return count > 0;
        }

        // ── RECURRING TRANSACTIONS ────────────────────────────────────

        public async Task InitRecurringTableAsync()
        {
            await InitAsync();
            await _db!.CreateTableAsync<RecurringTransaction>();
            try
            {
                await _db.ExecuteAsync(
                "ALTER TABLE RecurringTransactions ADD COLUMN FirebaseId TEXT");
            }
            catch { }
        }

        public async Task<List<RecurringTransaction>> GetRecurringTransactionsAsync()
        {
            await InitRecurringTableAsync();
            return await _db!.Table<RecurringTransaction>()
                             .OrderBy(r => r.NextRunDate)
                             .ToListAsync();
        }

        public async Task SaveRecurringAsync(RecurringTransaction r)
        {
            await InitRecurringTableAsync();
            if (r.Id == 0) await _db!.InsertAsync(r);
            else await _db!.UpdateAsync(r);
        }

        public async Task DeleteRecurringAsync(RecurringTransaction r)
        {
            await InitRecurringTableAsync();
            await _db!.DeleteAsync(r);
        }
    }
}