using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftFeatureTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/MythicRiftFeatureFlags.json";

        private static readonly object TuningLock = new();
        private static MythicRiftFeatureTuning _cachedTuning = CreateDefault();
        private static DateTime _cachedWriteTimeUtc = DateTime.MinValue;
        private static bool _cachedFileExists;

        public bool Enabled { get; set; } = true;
        public bool VendorEnabled { get; set; } = true;
        public string DisabledMessage { get; set; } = "Mythic Rifts are temporarily disabled.";
        public Dictionary<string, bool> Modes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(MythicRiftMode.Standard)] = true,
            [nameof(MythicRiftMode.Endless)] = true,
            [nameof(MythicRiftMode.BossGauntlet)] = true
        };

        public static JsonSerializerOptions JsonOptions => MythicRiftRewardTuning.JsonOptions;
        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static MythicRiftFeatureTuning CreateDefault()
        {
            MythicRiftFeatureTuning tuning = new();
            tuning.Normalize();
            return tuning;
        }

        public static MythicRiftFeatureTuning Load()
        {
            string configPath = ConfigPath;
            bool fileExists = File.Exists(configPath);
            DateTime writeTimeUtc = fileExists ? File.GetLastWriteTimeUtc(configPath) : DateTime.MinValue;

            if (_cachedTuning != null && _cachedFileExists == fileExists && _cachedWriteTimeUtc == writeTimeUtc)
                return _cachedTuning;

            lock (TuningLock)
            {
                fileExists = File.Exists(configPath);
                writeTimeUtc = fileExists ? File.GetLastWriteTimeUtc(configPath) : DateTime.MinValue;

                if (_cachedTuning != null && _cachedFileExists == fileExists && _cachedWriteTimeUtc == writeTimeUtc)
                    return _cachedTuning;

                MythicRiftFeatureTuning tuning = CreateDefault();
                if (fileExists)
                {
                    MythicRiftFeatureTuning loadedTuning = FileHelper.DeserializeJson<MythicRiftFeatureTuning>(configPath, JsonOptions);
                    if (loadedTuning != null)
                    {
                        loadedTuning.Normalize();
                        tuning = loadedTuning;
                    }
                }

                _cachedTuning = tuning;
                _cachedFileExists = fileExists;
                _cachedWriteTimeUtc = writeTimeUtc;
                return _cachedTuning;
            }
        }

        public bool IsModeEnabled(MythicRiftMode mode)
        {
            if (Enabled == false)
                return false;

            return Modes == null ||
                Modes.TryGetValue(mode.ToString(), out bool modeEnabled) == false ||
                modeEnabled;
        }

        public string GetDisabledMessage(MythicRiftMode mode)
        {
            if (string.IsNullOrWhiteSpace(DisabledMessage))
                return $"{MythicRiftManager.GetModeDisplayName(mode)} is temporarily disabled.";

            return DisabledMessage;
        }

        private void Normalize()
        {
            DisabledMessage = string.IsNullOrWhiteSpace(DisabledMessage)
                ? "Mythic Rifts are temporarily disabled."
                : DisabledMessage.Trim();

            Modes ??= new(StringComparer.OrdinalIgnoreCase);
            EnsureModeEntry(MythicRiftMode.Standard);
            EnsureModeEntry(MythicRiftMode.Endless);
            EnsureModeEntry(MythicRiftMode.BossGauntlet);
        }

        private void EnsureModeEntry(MythicRiftMode mode)
        {
            Modes.TryAdd(mode.ToString(), true);
        }
    }
}
