using IndiWare.Models;
using SQLite;

namespace IndiWare.Service
{
    public class DatabaseService
    {
        // DECLARE SQLITE ASYNC CONNECTION
        readonly SQLiteAsyncConnection _db;

        public DatabaseService()
        {
            // DEFINE DATABASE PATH
            var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "index.db3");

            // CHECK IF DATABASE FILE EXISTS
            if (!File.Exists(dbPath))
            {
                // CREATE DATABASE FILE IF IT DOESN'T EXIST
                using (File.Create(dbPath)) { }
                // INITIALIZE DATABASE CONNECTION
                _db = new SQLiteAsyncConnection(dbPath);
                // CREATE TABLES
                CreateTables();
            }
            else
            {
                // INITIALIZE DATABASE CONNECTION
                _db = new SQLiteAsyncConnection(dbPath);
            }
        }

        public async Task<List<FileItem>> GetSearchedAsync(string searchQuery)
        {
            // QUERY FileItemFTS SEARCHING FOR MATCHES AND RETRIEVE Ids
            var foundIdQuery = await _db.QueryAsync<FileItem>(
                "SELECT Id FROM FileItemFTS WHERE FileItemFTS MATCH ?",
                searchQuery);

            // PREPARE LIST TO HOLD FULL RECORDS
            var rows = new List<FileItem>();

            foreach (var item in foundIdQuery)
            {
                // QUERY FileItem FOR FULL RECORD USING Id
                var file = await _db.QueryAsync<FileItem>(
               "SELECT * FROM FileItem WHERE Id=?",
               item.Id);

                // ADD THE FIRST MATCHING RECORD TO THE RESULTS LIST
                rows.Add(file.FirstOrDefault());

            }
            return rows;
        }

        public async Task<bool> IsDatabaseEmptyAsync()
        {
            // CHECK IF FileItem TABLE IS EMPTY
            var count = await _db.ExecuteScalarAsync<int>("SELECT EXISTS(SELECT 1 FROM FileItem LIMIT 1);");

            // RETURN TRUE IF EMPTY, FALSE OTHERWISE
            if (count == 0)
                return true;
            else
                return false;
        }

        public async Task SaveManyAsync(IEnumerable<FileItem> items)
        {
            // RECREATE TABLES TO ENSURE CLEAN STATE
            await RecreateTablesAsync();

            await _db.RunInTransactionAsync(tran =>
            {
                foreach (var item in items)
                {
                    // INSERT INTO FileItem
                    tran.Insert(item);

                    // INSERT FILE FilePath AND Id INTO FileItemFTS
                    tran.Execute(
                        "INSERT INTO FileItemFTS (FilePath, Id) VALUES (?, ?)",
                        item.FilePath, item.Id);

                }
            });
        }

        private async void CreateTables()
        {
            // CALL RecreateTablesAsync METHOD TO ENSURE ASYNC TABLE CREATION
            await RecreateTablesAsync();
        }

        public async Task RecreateTablesAsync()
        {
            await _db.RunInTransactionAsync(tran =>
            {
                // DROP TABLE IF EXISTS FileItem AND FileItemFTS
                tran.Execute("DROP TABLE IF EXISTS FileItem;");
                tran.Execute("DROP TABLE IF EXISTS FileItemFTS;");

                // RECREATE TABLE FileItem
                tran.CreateTable<FileItem>();

                // RECREATE VIRTUAL TABLE FileItemFTS AGAIN
                tran.Execute(@"CREATE VIRTUAL TABLE FileItemFTS
                       USING fts5(FilePath, Id UNINDEXED)");
            });
        }
    }
}