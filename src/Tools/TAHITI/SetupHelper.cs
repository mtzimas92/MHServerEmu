using System.Reflection;
using System.Security.Cryptography;

namespace TAHITI_ConnectionTool
{
    public enum SetupResult
    {
        Success,
        InvalidFilePath,
        ClientNotFound,
        ClientVersionMismatch,
        ClientDataNotFound,
        ServerNotFound
    }

    internal static class SetupHelper
    {
        private const string ExecutableHash = "6DC9BCDB145F98E5C2D7A1F7E25AEB75507A9D1A";  // Win64 1.52.0.1700

        /// <summary>
        /// Sets up MHServerEmu using the client in the specified directory.
        /// </summary>

        public static SetupResult RunSetup(string clientRootDirectory)
        {
            // Validate directory path
            if (string.IsNullOrWhiteSpace(clientRootDirectory))
                return SetupResult.InvalidFilePath;

            // Find and verify the client executable
            if (FindClientExecutablePath(clientRootDirectory, out string clientDirectory, out string clientExecutablePath) == false)
                return SetupResult.ClientNotFound;           

            byte[] executableData = File.ReadAllBytes(clientExecutablePath);
            string executableHash = Convert.ToHexString(SHA1.HashData(executableData));

            if (ExecutableHash != executableHash)
                return SetupResult.ClientVersionMismatch;

            CreateBatFiles(clientExecutablePath);

            return SetupResult.Success;
        }
    
        /// <summary>
        /// Returns the text message for the specified <see cref="SetupResult"/>.
        /// </summary>
        public static string GetResultText(SetupResult result)
        {
            // TODO: translations
            return result switch
            {
                SetupResult.Success =>                  "Setup successful.",
                SetupResult.InvalidFilePath =>          "Invalid file path.",
                SetupResult.ClientNotFound =>           "Marvel Heroes game client not found.",
                SetupResult.ClientVersionMismatch =>    "Game client version mismatch. Please make sure you have version 1.52.0.1700.",
                _ =>                                    "Unknown error.",
            };
        }

        /// <summary>
        /// Searches for the game client in the specified directory.
        /// </summary>
        private static bool FindClientExecutablePath(string rootDirectory, out string clientDirectory, out string clientExecutablePath)
        {
            // Check if we are in the client root directory
            clientDirectory = rootDirectory;
            clientExecutablePath = Path.Combine(clientDirectory, "UnrealEngine3", "Binaries", "Win64", "MarvelHeroesOmega.exe");

            if (File.Exists(clientExecutablePath))
                return true;

            // Check if we are in the executable directory instead of the client root
            clientExecutablePath = Path.Combine(rootDirectory, "MarvelHeroesOmega.exe");
            if (File.Exists(clientExecutablePath))
            {
                // Adjust client directory
                clientDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "..", "..", ".."));
                return true;
            }

            // Not found
            return false;
        }


        /// <summary>
        /// Creates .bat files required for managing the server and the client.
        /// </summary>
        private static void CreateBatFiles(string clientExecutablePath)
        {
            string toolDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string batFilePath = Path.Combine(toolDirectory, "StartTahitiServer.bat");

            using (StreamWriter writer = new(batFilePath))
                writer.WriteLine($"@start \"\" \"{clientExecutablePath}\" -robocopy -nosteam -siteconfigurl=mhtahiti.com/SiteConfig.xml");
        }
    }
}
