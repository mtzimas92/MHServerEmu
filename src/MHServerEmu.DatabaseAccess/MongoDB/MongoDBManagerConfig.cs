using MHServerEmu.Core.Config;

namespace MHServerEmu.DatabaseAccess.MongoDB
{
    /// <summary>
    /// Configuration settings for the MongoDBManager.
    /// </summary>
    public class MongoDBManagerConfig : ConfigContainer
    {
        public string ConnectionString { get; private set; } = "mongodb://localhost:27017";
        public string DatabaseName { get; private set; } = "MHServerEmu";
        public int MaxBackupNumber { get; private set; } = 5;
        public int BackupIntervalMinutes { get; private set; } = 15;
    }
}
