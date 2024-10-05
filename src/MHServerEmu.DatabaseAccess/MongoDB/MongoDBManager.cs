using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using MHServerEmu.Core.Config;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.System.Time;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.DatabaseAccess.MongoDB
{
    public class MongoDBManager : IDBManager
    {
        private const int CurrentSchemaVersion = 2;
        private const int NumTestAccounts = 5;
        private const int NumPlayerDataWriteAttempts = 3;

        private static readonly Logger Logger = LogManager.CreateLogger();
        private readonly object _writeLock = new object();
        private readonly ConcurrentDictionary<long, object> _accountLocks = new ConcurrentDictionary<long, object>();

        private IMongoDatabase _database;
        private TimeSpan _lastBackupTime;
        private int _maxBackupNumber;
        private TimeSpan _backupInterval;

        public static MongoDBManager Instance { get; } = new MongoDBManager();

        private MongoDBManager() { }

        public bool Initialize()
        {
            var config = ConfigManager.Instance.GetConfig<MongoDBManagerConfig>();
            var client = new MongoClient(config.ConnectionString);
            _database = client.GetDatabase(config.DatabaseName);

            _maxBackupNumber = config.MaxBackupNumber;
            _backupInterval = TimeSpan.FromMinutes(config.BackupIntervalMinutes);

            if (!CollectionExists("Account"))
            {
                InitializeCollections();
                CreateTestAccounts(NumTestAccounts);
            }
            else
            {
                if (!MigrateDatabaseToCurrentSchema())
                    return false;
            }

            _lastBackupTime = Clock.GameTime;

            Logger.Info($"Connected to MongoDB database: {config.DatabaseName}");
            return true;
        }

        public bool TryQueryAccountByEmail(string email, out DBAccount account)
        {
            var collection = _database.GetCollection<DBAccount>("Account");
            account = collection.Find(a => a.Email == email).FirstOrDefault();
            return account != null;
        }

        public bool QueryIsPlayerNameTaken(string playerName)
        {
            var collection = _database.GetCollection<DBAccount>("Account");
            return collection.Find(a => a.PlayerName == playerName).Any();
        }

        public bool InsertAccount(DBAccount account)
        {
            object accountLock = _accountLocks.GetOrAdd(account.Id, _ => new object());
            lock (accountLock)
            {
                try
                {
                    var collection = _database.GetCollection<DBAccount>("Account");
                    collection.InsertOne(account);
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
            object accountLock = _accountLocks.GetOrAdd(account.Id, _ => new object());
            lock (accountLock)
            {
                try
                {
                    var collection = _database.GetCollection<DBAccount>("Account");
                    var result = collection.ReplaceOne(a => a.Id == account.Id, account);
                    return result.IsAcknowledged && result.ModifiedCount > 0;
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
            var collection = _database.GetCollection<DBAccount>("Account");
            var loadedAccount = collection.Find(a => a.Id == account.Id).FirstOrDefault();

            if (loadedAccount != null)
            {
                account.Player = loadedAccount.Player ?? new DBPlayer(account.Id);
                account.ClearEntities();
                account.Avatars.AddRange(loadedAccount.Avatars ?? new DBEntityCollection());
                account.TeamUps.AddRange(loadedAccount.TeamUps ?? new DBEntityCollection());
                account.Items.AddRange(loadedAccount.Items ?? new DBEntityCollection());
                account.ControlledEntities.AddRange(loadedAccount.ControlledEntities ?? new DBEntityCollection());
            }
            else
            {
                account.Player = new DBPlayer(account.Id);
                account.ClearEntities();
                Logger.Info($"Initialized player data for account 0x{account.Id:X}");
            }

            return true;
        }

        public bool SavePlayerData(DBAccount account)
        {
            for (int i = 0; i < NumPlayerDataWriteAttempts; i++)
            {
                if (DoSavePlayerData(account))
                {
                    TryCreateBackup();
                    return Logger.InfoReturn(true, $"Successfully written player data for account [{account}]");
                }
            }

            return Logger.WarnReturn(false, $"SavePlayerData(): Failed to write player data for account [{account}]");
        }

        private bool CollectionExists(string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var collections = _database.ListCollections(new ListCollectionsOptions { Filter = filter });
            return collections.Any();
        }

        private void InitializeCollections()
        {
            _database.CreateCollection("Account");

            var versionCollection = _database.GetCollection<BsonDocument>("Version");
            versionCollection.InsertOne(new BsonDocument { { "SchemaVersion", CurrentSchemaVersion } });

            Logger.Info($"Initialized MongoDB collections using schema version {CurrentSchemaVersion}");
        }

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

        private bool DoSavePlayerData(DBAccount account)
        {
            object accountLock = _accountLocks.GetOrAdd(account.Id, _ => new object());
            lock (accountLock)
            {
                try
                {
                    var collection = _database.GetCollection<DBAccount>("Account");
                    var filter = Builders<DBAccount>.Filter.Eq(a => a.Id, account.Id);
                    var update = Builders<DBAccount>.Update
                        .Set(a => a.Player, account.Player)
                        .Set(a => a.Avatars, account.Avatars)
                        .Set(a => a.TeamUps, account.TeamUps)
                        .Set(a => a.Items, account.Items)
                        .Set(a => a.ControlledEntities, account.ControlledEntities);

                    var options = new UpdateOptions { IsUpsert = true };
                    var result = collection.UpdateOne(filter, update, options);

                    Logger.Info($"DoSavePlayerData: Updated document for account {account.Id}. Acknowledged: {result.IsAcknowledged}, Matched: {result.MatchedCount}, Modified: {result.ModifiedCount}, Upserted: {result.UpsertedId != null}");

                    return result.IsAcknowledged;
                }
                catch (Exception e)
                {
                    Logger.Warn($"DoSavePlayerData(): MongoDB error for account [{account}]: {e.Message}");
                    return false;
                }
            }
        }

        private void TryCreateBackup()
        {
            TimeSpan now = Clock.GameTime;

            if ((now - _lastBackupTime) < _backupInterval)
                return;

            var config = ConfigManager.Instance.GetConfig<MongoDBManagerConfig>();
            string backupPath = Path.Combine(FileHelper.DataDirectory, "Backups", "MongoDB");
            Directory.CreateDirectory(backupPath);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"{config.DatabaseName}_{timestamp}.gz";
            string fullBackupPath = Path.Combine(backupPath, backupFileName);

            string mongodumpPath = "mongodump"; // Assume mongodump is in PATH, or provide full path

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = mongodumpPath,
                Arguments = $"--uri=\"{config.ConnectionString}\" --gzip --archive=\"{fullBackupPath}\" --db={config.DatabaseName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    Logger.Info($"Created MongoDB database backup: {fullBackupPath}");
                    _lastBackupTime = now;

                    // Remove old backups
                    var backupFiles = Directory.GetFiles(backupPath).OrderByDescending(f => f).Skip(_maxBackupNumber);
                    foreach (var file in backupFiles)
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Logger.Error($"Failed to create MongoDB database backup. Exit code: {process.ExitCode}");
                }
            }
        }

        private int GetSchemaVersion()
        {
            var versionCollection = _database.GetCollection<BsonDocument>("Version");
            var version = versionCollection.Find(new BsonDocument()).FirstOrDefault();
            return version?["SchemaVersion"].AsInt32 ?? -1;
        }

        private void SetSchemaVersion(int version)
        {
            var versionCollection = _database.GetCollection<BsonDocument>("Version");
            var filter = new BsonDocument();
            var update = Builders<BsonDocument>.Update.Set("SchemaVersion", version);
            versionCollection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });
        }

        private bool MigrateDatabaseToCurrentSchema()
        {
            int schemaVersion = GetSchemaVersion();
            if (schemaVersion > CurrentSchemaVersion)
                return Logger.ErrorReturn(false, $"Initialize(): Existing database uses unsupported schema version {schemaVersion} (current = {CurrentSchemaVersion})");

            while (schemaVersion < CurrentSchemaVersion)
            {
                Logger.Info($"Migrating version {schemaVersion} => {schemaVersion + 1}...");
                // Implement migration logic here
                schemaVersion++;
                SetSchemaVersion(schemaVersion);
            }

            return true;
        }
    }
}