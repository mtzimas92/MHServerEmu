using System.Data.SQLite;
using Dapper;
using MHServerEmu.Core.Logging;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.DatabaseAccess.SQLite;
using MHServerEmu.DatabaseAccess.MongoDB;

namespace MHServerEmu.DatabaseAccess.Migration
{
    public static class DatabaseMigrationTool
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        public static void MigrateSQLiteToMongoDB()
        {
            Logger.Info("Starting migration from SQLite to MongoDB...");

            SQLiteDBManager sqliteManager = SQLiteDBManager.Instance;
            MongoDBManager mongoManager = MongoDBManager.Instance;

            // Ensure both database managers are initialized
            sqliteManager.Initialize();
            mongoManager.Initialize();

            // Fetch all accounts from SQLite
            var accounts = FetchAllAccountsFromSQLite(sqliteManager);

            // Migrate each account to MongoDB
            foreach (var account in accounts)
            {
                MigrateAccount(account, sqliteManager, mongoManager);
            }

            Logger.Info("Migration completed successfully.");
        }

        private static List<DBAccount> FetchAllAccountsFromSQLite(SQLiteDBManager sqliteManager)
        {
            using (var connection = sqliteManager.GetConnection())
            {
                return connection.Query<DBAccount>("SELECT * FROM Account").ToList();
            }
        }

        private static void MigrateAccount(DBAccount account, SQLiteDBManager sqliteManager, MongoDBManager mongoManager)
        {
            // Load full account data from SQLite
            sqliteManager.LoadPlayerData(account);

            // Insert or update the account in MongoDB
            if (mongoManager.TryQueryAccountByEmail(account.Email, out _))
            {
                mongoManager.UpdateAccount(account);
            }
            else
            {
                mongoManager.InsertAccount(account);
            }

            // Save player data to MongoDB
            mongoManager.SavePlayerData(account);

            Logger.Info($"Migrated account: {account.Email}");
        }
    }
}
