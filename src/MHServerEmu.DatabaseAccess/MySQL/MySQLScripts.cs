using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;

namespace MHServerEmu.DatabaseAccess.MySqlA
{
    public static class MySQLScripts
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        public static string GetInitializationScript()
        {
            string filePath = Path.Combine(FileHelper.DataDirectory, "MySql", "InitializeDatabase.sql");
            if (File.Exists(filePath) == false)
                return Logger.WarnReturn(string.Empty, $"GetDatabaseInitializationScript(): Initialization script file not found at {FileHelper.GetRelativePath(filePath)}");

            return File.ReadAllText(filePath);
        }

        public static string GetMigrationScript(int currentVersion)
        {
            string filePath = Path.Combine(FileHelper.DataDirectory, "MySql", "Migrations", $"{currentVersion}.sql");
            if (File.Exists(filePath) == false)
                return Logger.WarnReturn(string.Empty, $"GetMigrationScript(): Migration script for version {currentVersion} not found at {FileHelper.GetRelativePath(filePath)}");

            return File.ReadAllText(filePath);
        }
    }
}
