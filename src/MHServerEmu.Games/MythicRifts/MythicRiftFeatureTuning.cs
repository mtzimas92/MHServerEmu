using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftFeatureTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/MythicRiftFeatureFlags.json";

        private static readonly object TuningLock = new();
        private static MythicRiftFeatureTuning _cachedTuning;
        private static volatile bool _loaded;

        public bool Enabled { get; set; } = true;
        public bool VendorEnabled { get; set; } = true;
        public bool ResetOmegaTrainingProgressWeekly { get; set; } = false;
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
            if (_loaded)
                return _cachedTuning;

            lock (TuningLock)
            {
                if (_loaded)
                    return _cachedTuning;

                string configPath = ConfigPath;
                bool fileExists = File.Exists(configPath);

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
                _loaded = true;
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
