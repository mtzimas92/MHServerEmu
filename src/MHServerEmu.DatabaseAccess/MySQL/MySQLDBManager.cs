using Dapper;
using MHServerEmu.Core.Config;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.System.Time;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.DatabaseAccess.MySqlDB;
using MHServerEmu.DatabaseAccess.MySqlA;
using MySql.Data.MySqlClient;
using MHServerEmu.Core.Helpers;
using MHServerEmu.DatabaseAccess.SQLite;
using System.Data.SQLite;
using System.Text;


namespace MHServerEmu.DatabaseAccess.MySQL
{
    /// <summary>
    /// Provides functionality for storing <see cref="DBAccount"/> instances in a MySql database using the <see cref="IDBManager"/> interface.
    /// </summary>
    public class MySQLDBManager : IDBManager
    {
        private const int CurrentSchemaVersion = 2;         // Increment this when making changes to the database schema
        private const int NumTestAccounts = 5;              // Number of test accounts to create for new databases
        private const int NumPlayerDataWriteAttempts = 3;   // Number of write attempts to do when saving player data

        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly object _writeLock = new();

        private string _dbFilePath;
        private string _connectionString;

        public static MySQLDBManager Instance { get; } = new();
        private MySQLDBManager() { }

        public bool Initialize()
        {
            var config = ConfigManager.Instance.GetConfig<MySQLDBManagerConfig>();
            _connectionString = $"Data Source={_dbFilePath}";
            _dbFilePath = Path.Combine(FileHelper.DataDirectory, ConfigManager.Instance.GetConfig<SQLiteDBManagerConfig>().FileName);
            try
            {
                using MySqlConnection schemaCheck = GetConnection();
                var isSchemaExist = schemaCheck.Query("SHOW DATABASES LIKE '" + config.MySqlDBName + "';");
                schemaCheck.Close();
                if (!isSchemaExist.Any()) InitializeDatabaseFile();
                if (File.Exists(_dbFilePath) == true)
                {
                    if (MigrateDatabaseFileToCurrentSchema() == false)
                        return false;
                    return true;
                }
                    //_lastBackupTime = Clock.GameTime;
                return true;
            }
            catch
            {
                if (File.Exists(_dbFilePath) == true)
                {
                    // Migrate existing database if needed
                    if (MigrateDatabaseFileToCurrentSchema() == false)
                        return false;
                }
                else
                {
                    if (InitializeDatabaseFile() == false)
                        return false;
                }

                return true;
            }
        }


            public bool TryQueryAccountByEmail(string email, out DBAccount account)
        {
            using MySqlConnection connection = GetConnection();

            var accounts = connection.Query<DBAccount>("SELECT * FROM Account WHERE Email = @Email", new { Email = email });

            // Associated player data is loaded separately
            account = accounts.FirstOrDefault();
            return account != null;
        }

        public bool QueryIsPlayerNameTaken(string playerName)
        {
            using MySqlConnection connection = GetConnection();

            // This check is innately case insensitive
            var results = connection.Query<string>("SELECT PlayerName FROM Account WHERE PlayerName = @PlayerName", new { PlayerName = playerName });
            return results.Any();
        }

        public bool InsertAccount(DBAccount account)
        {

            {
                using MySqlConnection connection = GetConnection();

                try
                {
                    connection.Execute(@"INSERT INTO Account (Id, Email, PlayerName, PasswordHash, Salt, UserLevel, Flags)
                        VALUES (@Id, @Email, @PlayerName, @PasswordHash, @Salt, @UserLevel, @Flags)", account);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.ErrorException(e, nameof(InsertAccount));
                    return false;
                }
            }
        }
        public bool UpdateAccount(DBAccount account)
        {

            {
                using MySqlConnection connection = GetConnection();

                try
                {
                    connection.Execute(@"UPDATE Account SET Email=@Email, PlayerName=@PlayerName, PasswordHash=@PasswordHash, Salt=@Salt,
                        UserLevel=@UserLevel, Flags=@Flags WHERE Id=@Id", account);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.ErrorException(e, nameof(UpdateAccount));
                    return false;
                }
            }
        }

        public bool LoadPlayerData(DBAccount account)
        {
            // Clear existing data
            account.Player = null;
            account.ClearEntities();

            // Load fresh data
            using MySqlConnection connection = GetConnection();

            var @params = new { DbGuid = account.Id };

            var players = connection.Query<DBPlayer>("SELECT * FROM Player WHERE DbGuid = @DbGuid", @params);
            account.Player = players.FirstOrDefault();

            if (account.Player == null)
            {
                account.Player = new(account.Id);
                Logger.Info($"Initialized player data for account 0x{account.Id:X}");
            }

            // Load inventory entities
            account.Avatars.AddRange(LoadEntitiesFromTable(connection, "Avatar", account.Id));
            account.TeamUps.AddRange(LoadEntitiesFromTable(connection, "TeamUp", account.Id));
            account.Items.AddRange(LoadEntitiesFromTable(connection, "Item", account.Id));

            foreach (DBEntity avatar in account.Avatars)
            {
                account.Items.AddRange(LoadEntitiesFromTable(connection, "Item", avatar.DbGuid));
                account.ControlledEntities.AddRange(LoadEntitiesFromTable(connection, "ControlledEntity", avatar.DbGuid));
            }

            foreach (DBEntity teamUp in account.TeamUps)
            {
                account.Items.AddRange(LoadEntitiesFromTable(connection, "Item", teamUp.DbGuid));
            }

            return true;
        }

        public bool SavePlayerData(DBAccount account)
        {
            for (int i = 0; i < NumPlayerDataWriteAttempts; i++)
            {
                if (DoSavePlayerData(account))
                    return Logger.InfoReturn(true, $"Successfully written player data for account [{account}]");

                // Maybe we should add a delay here
            }

            return Logger.WarnReturn(false, $"SavePlayerData(): Failed to write player data for account [{account}]");
        }

        /// <summary>
        /// Creates and opens a new <see cref="MySQLConnection"/>.
        /// </summary>
        private static MySqlConnection GetConnection()
        {
            var config = ConfigManager.Instance.GetConfig<MySQLDBManagerConfig>();
            var connectionStringVars = string.Join(";", "server="+config.MySqlIP, "Database="+config.MySqlDBName, "Uid="+config.MySqlUsername, "Pwd="+config.MySqlPw);
            string connectionString = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionStringVars).ToString();
            MySqlConnection connection = new(connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Initializes a new empty database file using the current schema.
        /// </summary>
        private bool InitializeDatabaseFile()
        {
            string MySqlInitializationScript = MySqlA.MySQLScripts.GetInitializationScript();
            var config = ConfigManager.Instance.GetConfig<MySQLDBManagerConfig>();
            if (MySqlInitializationScript == string.Empty)
                return Logger.ErrorReturn(false, "InitializeDatabaseFile(): Failed to get database initialization script");

            var connectionStringVars = string.Join(";", "server=" + config.MySqlIP, "Uid=" + config.MySqlUsername, "Pwd=" + config.MySqlPw);
            string connectionString = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionStringVars).ToString();
            MySqlConnection connectionInit = new(connectionString);
            connectionInit.Open();
            connectionInit.Execute("CREATE SCHEMA IF NOT EXISTS " + config.MySqlDBName);
            connectionInit.Execute("USE " + config.MySqlDBName);
            connectionInit.Execute(MySqlInitializationScript);

            Logger.Info($"Initialized a new MySql database using schema version {CurrentSchemaVersion}");
            connectionInit.Close();
            CreateTestAccounts(NumTestAccounts);

            return true;
        }

        /// <summary>
        /// Creates the specified number of test accounts.
        /// </summary>
        private void CreateTestAccounts(int numAccounts)
        {
            for (int i = 0; i < numAccounts; i++)
            {
                string email = $"test{i + 1}@test.com";
                string playerName = $"Player{i + 1}";
                string password = "123";

                DBAccount account = new(email, playerName, password);
                InsertAccount(account);
                Logger.Info($"Created test account {account}");
            }
        }

        /// <summary>
        /// Migrates an existing database to the current schema if needed.
        /// </summary>
        private bool MigrateDatabaseFileToCurrentSchema()
        {

            if (File.Exists(_dbFilePath) == true)
            {
                var config = ConfigManager.Instance.GetConfig<MySQLDBManagerConfig>();
                try
                {
                    using MySqlConnection connection = GetConnection();
                    File.WriteAllText($"{_dbFilePath}.WMigrate.sql", DumpSQLiteDatabase(_dbFilePath, true));
                    string filePath = $"{_dbFilePath}.WMigrate.sql";
                    Logger.Info("Importing SQLite Database into MySQL... This may take a while.");
                    MySqlScript importScript = new(connection, File.ReadAllText(filePath));
                    importScript.Execute();
                    connection.Close();
                    File.Move($"{_dbFilePath}", $"{_dbFilePath}.WMigrated");
                    Logger.Info($"Old SQLite Database saved to {_dbFilePath}.Migrated");
                    File.Delete($"{_dbFilePath}.WMigrate.sql");
                    return true;
                }
                catch (Exception e)
                {
                    if (e.Message.Any())
                    {
                        Logger.Error(e.Message); return false;
                    }
                    File.WriteAllText($"{_dbFilePath}.Migrate.sql", DumpSQLiteDatabase(_dbFilePath, false));
                    var connectionStringVars = string.Join(";", "server=" + config.MySqlIP, "Uid=" + config.MySqlUsername, "Pwd=" + config.MySqlPw);
                    string connectionString = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionStringVars).ToString();
                    MySqlConnection connectionInit = new(connectionString);
                    connectionInit.Open();
                    connectionInit.Execute("CREATE SCHEMA IF NOT EXISTS " + config.MySqlDBName);
                    connectionInit.Close();
                    using MySqlConnection connection = GetConnection();
                    Logger.Info("Importing SQLite Database into MySQL... This may take a while.");
                    string filePath = $"{_dbFilePath}.Migrate.sql";
                    MySqlScript importScript = new(connection, File.ReadAllText(filePath));
                    importScript.Execute();
                    File.Move($"{_dbFilePath}", $"{_dbFilePath}.Migrated");
                    Logger.Info($"SQL to MySQL Migration success!");
                    Logger.Info($"Old SQLite Database saved to {_dbFilePath}.Migrated");
                    connection.Close();
                    File.Delete($"{_dbFilePath}.WMigrate.sql");
                    return true;
                }

            }
            else
            {
                using MySqlConnection connection = GetConnection();
                int schemaVersion = GetSchemaVersion(connection);
                if (schemaVersion > CurrentSchemaVersion)
                    return Logger.ErrorReturn(false, $"Initialize(): Existing database uses unsupported schema version {schemaVersion} (current = {CurrentSchemaVersion})");

                Logger.Info($"Found existing database with schema version {schemaVersion} (current = {CurrentSchemaVersion})");

                if (schemaVersion == CurrentSchemaVersion)
                    return true;

                // Create a backup to fall back to if something goes wrong


                bool success = true;

                while (schemaVersion < CurrentSchemaVersion)
                {
                    Logger.Info($"Migrating version {schemaVersion} => {schemaVersion + 1}...");

                    string migrationScript = MySQLScripts.GetMigrationScript(schemaVersion);
                    if (migrationScript == string.Empty)
                    {
                        Logger.Error($"MigrateDatabaseFileToCurrentSchema(): Failed to get database migration script for version {schemaVersion}");
                        success = false;
                        break;
                    }

                    connection.Execute(migrationScript);
                    SetSchemaVersion(connection, ++schemaVersion);
                }

                success &= GetSchemaVersion(connection) == CurrentSchemaVersion;

                if (success == false)
                {
                    // Restore backup

                    return Logger.ErrorReturn(false, "MigrateDatabaseFileToCurrentSchema(): Migration failed, backup restored");
                }
                else
                {
                    // Clean up backup

                }

                Logger.Info($"Successfully migrated to schema version {CurrentSchemaVersion}");
            }
            return true;
        }

        private static bool DoSavePlayerData(DBAccount account)
        {

            {
                using MySqlConnection connection = GetConnection();

                // Use a transaction to make sure all data is saved
                using MySqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    // Update player entity
                    if (account.Player != null)
                    {
                        connection.Execute(@$"INSERT IGNORE INTO Player (DbGuid) VALUES (@DbGuid)", account.Player, transaction);
                        connection.Execute(@$"UPDATE Player SET ArchiveData=@ArchiveData, StartTarget=@StartTarget,
                                            StartTargetRegionOverride=@StartTargetRegionOverride, AOIVolume=@AOIVolume WHERE DbGuid = @DbGuid",
                                            account.Player, transaction);
                    }
                    else
                    {
                        Logger.Warn($"DoSavePlayerData(): Attempted to save null player entity data for account {account}");
                    }

                    // Update inventory entities
                    UpdateEntityTable(connection, transaction, "Avatar", account.Id, account.Avatars);
                    UpdateEntityTable(connection, transaction, "TeamUp", account.Id, account.TeamUps);
                    UpdateEntityTable(connection, transaction, "Item", account.Id, account.Items);

                    foreach (DBEntity avatar in account.Avatars)
                    {
                        UpdateEntityTable(connection, transaction, "Item", avatar.DbGuid, account.Items);
                        UpdateEntityTable(connection, transaction, "ControlledEntity", avatar.DbGuid, account.ControlledEntities);
                    }

                    foreach (DBEntity teamUp in account.TeamUps)
                    {
                        UpdateEntityTable(connection, transaction, "Item", teamUp.DbGuid, account.Items);
                    }

                    transaction.Commit();


                    return true;
                }
                catch (Exception e)
                {
                    Logger.Warn($"DoSavePlayerData(): MySql error for account [{account}]: {e.Message}");
                    transaction.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns the user_version value of the current database.
        /// </summary>
        private static int GetSchemaVersion(MySqlConnection connection)
        {
            var queryResult = connection.Query<int>("PRAGMA user_version");
            if (queryResult.Any())
                return queryResult.First();

            return Logger.WarnReturn(-1, "GetSchemaVersion(): Failed to query user_version from the DB");
        }

        /// <summary>
        /// Sets the user_version value of the current database.
        /// </summary>
        private static void SetSchemaVersion(MySqlConnection connection, int version)
        {
            connection.Execute($"UPDATE PRAGMA SET user_version={version} LIMIT 1");
        }

        /// <summary>
        /// Loads <see cref="DBEntity"/> instances belonging to the specified container from the specified table.
        /// </summary>
        private static IEnumerable<DBEntity> LoadEntitiesFromTable(MySqlConnection connection, string tableName, long containerDbGuid)
        {
            var @params = new { ContainerDbGuid = containerDbGuid };
            return connection.Query<DBEntity>($"SELECT * FROM {tableName} WHERE ContainerDbGuid = @ContainerDbGuid", @params);
        }

        /// <summary>
        /// Updates <see cref="DBEntity"/> instances belonging to the specified container in the specified table using the provided <see cref="DBEntityCollection"/>.
        /// </summary>
        private static void UpdateEntityTable(MySqlConnection connection, MySqlTransaction transaction, string tableName,
            long containerDbGuid, DBEntityCollection dbEntityCollection)
        {
            var @params = new { ContainerDbGuid = containerDbGuid };

            // Delete items that no longer belong to this account
            var storedEntities = connection.Query<long>($"SELECT DbGuid FROM {tableName} WHERE ContainerDbGuid = @ContainerDbGuid", @params);
            var entitiesToDelete = storedEntities.Except(dbEntityCollection.Guids);
            if (entitiesToDelete.Any()) connection.Execute($"DELETE FROM {tableName} WHERE DbGuid IN ({string.Join(',', entitiesToDelete)})");

            // Insert and update
            IEnumerable<DBEntity> entries = dbEntityCollection.GetEntriesForContainer(containerDbGuid);

            connection.Execute(@$"INSERT IGNORE INTO {tableName} (DbGuid) VALUES (@DbGuid)", entries, transaction);
            connection.Execute(@$"UPDATE {tableName} SET ContainerDbGuid=@ContainerDbGuid, InventoryProtoGuid=@InventoryProtoGuid,
                                Slot=@Slot, EntityProtoGuid=@EntityProtoGuid, ArchiveData=@ArchiveData WHERE DbGuid=@DbGuid",
                                entries, transaction);
        }

        public static string DumpSQLiteDatabase(string dbFilePath, bool isExist)
        {
                StringBuilder tableCreation = new();
                StringBuilder foreignKeys = new();
                StringBuilder dataInsertion = new();

            using SQLiteConnection connection = new($"Data Source={dbFilePath};");
            connection.Open();

            List<string> tableNames = new();
            using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            foreach (string tableName in tableNames)
            {
                // Get table creation SQL
                using (var cmd = new SQLiteCommand($"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}';", connection))
                {
                    string createTableSql = cmd.ExecuteScalar() as string;
                    tableCreation.AppendLine(createTableSql + ";");

                    // Extract foreign key definitions
                    string[] lines = createTableSql.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.Trim().StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                        {
                            foreignKeys.AppendLine($"ALTER TABLE {tableName} ADD {line.Trim()};");
                        }
                    }
                }

                // Get column names and types
                List<(string Name, string Type)> columns = new();
                using (var cmd = new SQLiteCommand($"PRAGMA table_info('{tableName}');", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add((reader.GetString(1), reader.GetString(2))); // Name at index 1, Type at index 2
                    }
                }

                // Dump table data
                using (var cmd = new SQLiteCommand($"SELECT * FROM '{tableName}';", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StringBuilder insertSql = new();
                        if (!isExist) insertSql = new($"INSERT INTO `{tableName}` (");
                        if (isExist) insertSql = new($"INSERT IGNORE INTO `{tableName}` (");
                        insertSql.Append(string.Join(", ", columns.Select(c => c.Name)));
                        insertSql.Append(") VALUES (");

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            if (i > 0) insertSql.Append(", ");
                            if (reader.IsDBNull(i))
                            {
                                insertSql.Append("NULL");
                            }
                            else if (columns[i].Type.ToUpper() == "BLOB")
                            {
                                byte[] blobData = (byte[])reader.GetValue(i);
                                string hexString = BitConverter.ToString(blobData).Replace("-", "");
                                insertSql.Append($"X'{hexString}'");
                            }
                            else
                            {
                                string value = reader.GetValue(i).ToString().Replace("'", "''");
                                insertSql.Append($"'{value}'");
                            }
                        }
                        insertSql.Append(");");
                        dataInsertion.AppendLine(insertSql.ToString());
                    }
                }
            }

            // Get and append index creation statements
            StringBuilder indexCreation = new();
            using (var cmd = new SQLiteCommand("SELECT sql FROM sqlite_master WHERE type='index' AND sql IS NOT NULL;", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    indexCreation.AppendLine(reader.GetString(0) + ";");
                }
            }

            // Combine all parts in the correct order
            StringBuilder finalDump = new();
            finalDump.AppendLine("-- Script to Initialize a new database file\r\n");
            finalDump.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
            if (!isExist)
            {
                finalDump.Append(tableCreation);
                //finalDump.Append(foreignKeys);
                finalDump.Append(indexCreation);
            }
            finalDump.Append(dataInsertion);
            finalDump.Replace("\"", "");
            finalDump.Replace("`", "");
            finalDump.Replace("INTEGER", "BIGINT");
            finalDump.Replace("TEXT", "VARCHAR(50)");
            finalDump.Replace("BLOB", "BLOB(1000)");
            return finalDump.ToString();
        }
    }
}
