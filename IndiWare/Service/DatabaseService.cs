using IndiWare.Models;
using SQLite;

namespace IndiWare.Service
{
    public class DatabaseService
    {
        readonly SQLiteAsyncConnection _db;

        public DatabaseService()
        {
            var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "index.db3");

            if (!File.Exists(dbPath))
            {
                using (File.Create(dbPath)) { }
                _db = new SQLiteAsyncConnection(dbPath);
                CreateTables();
            }
            else
            {
                _db = new SQLiteAsyncConnection(dbPath);
            }
        }

        private async void CreateTables()
        {
            await RecreateTablesAsync();
        }

        public async Task<List<FileItem>> GetSearchedAsync(string searchQuery)
        {
            var foundIdQuery = await _db.QueryAsync<FileItem>(
                "SELECT Id FROM FileItemFTS WHERE FileItemFTS MATCH ?",
                searchQuery);

            var rows = new List<FileItem>();

            foreach (var item in foundIdQuery)
            {
                var file = await _db.QueryAsync<FileItem>(
               "SELECT * FROM FileItem WHERE Id=?",
               item.Id);

                rows.Add(file.FirstOrDefault());

            }
            return rows;
        }

        public async Task<bool> IsDatabaseEmptyAsync()
        {
            var count = await _db.ExecuteScalarAsync<int>("SELECT EXISTS(SELECT 1 FROM FileItem LIMIT 1);");

            if (count == 0)
                return true;
            else
                return false;
        }

        public async Task<int> SaveManyAsync(IEnumerable<FileItem> items)
        {
            await RecreateTablesAsync();

            int inserted = 0;
            await _db.RunInTransactionAsync(tran =>
            {
                foreach (var item in items)
                {
                    tran.Insert(item);

                    tran.Execute(
                        "INSERT INTO FileItemFTS (FilePath, Id) VALUES (?, ?)",
                        item.FilePath, item.Id);

                    inserted++;
                }
            });

            return inserted;
        }

        public async Task RecreateTablesAsync()
        {
            await _db.RunInTransactionAsync(tran =>
            {
                tran.Execute("DROP TABLE IF EXISTS FileItem;");
                tran.Execute("DROP TABLE IF EXISTS FileItemFTS;");

                tran.CreateTable<FileItem>();   // ricrea la principale

                // ricrea la FTS: non usare IF NOT EXISTS, altrimenti resta quella vecchia
                tran.Execute(@"CREATE VIRTUAL TABLE FileItemFTS
                       USING fts5(FilePath, Id UNINDEXED)");
            });
        }
    }
}