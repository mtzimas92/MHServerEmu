using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftHazardTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/CosmicRiftHazards.json";

        public string ProfileName { get; set; } = "default-hazards";
        public bool Enabled { get; set; } = true;
        public bool UseKeywordDiscovery { get; set; } = true;
        public int MaxActiveHazards { get; set; } = 4;
        public float SpawnIntervalSeconds { get; set; } = 14f;
        public float DurationSeconds { get; set; } = 8f;
        public float SpawnDistance { get; set; } = 420f;
        public float SpawnSearchDistance { get; set; } = 260f;
        public int MinStandardRiftLevel { get; set; } = 30;
        public int MinRiftGauntletWave { get; set; } = 10;
        public int MinBossGauntletWave { get; set; } = 10;
        public List<string> DiscoveryNameKeywords { get; set; } = new()
        {
            "hazard",
            "hotspot",
            "lava",
            "fire",
            "poison",
            "acid",
            "laser",
            "bomb",
            "explosion",
            "lightning"
        };
        public List<string> BlockedNameKeywords { get; set; } = new()
        {
            "mission",
            "trigger",
            "portal",
            "transition",
            "checkpoint",
            "objective",
            "script",
            "ui"
        };
        public List<MythicRiftHazardEntryTuning> Hazards { get; set; } = new();

        public static JsonSerializerOptions JsonOptions => MythicRiftRewardTuning.JsonOptions;
        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static MythicRiftHazardTuning CreateDefault()
        {
            MythicRiftHazardTuning tuning = new();
            tuning.Normalize();
            return tuning;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "default-hazards";

            MaxActiveHazards = Math.Clamp(MaxActiveHazards, 0, 12);
            SpawnIntervalSeconds = Math.Clamp(SpawnIntervalSeconds, 3f, 120f);
            DurationSeconds = Math.Clamp(DurationSeconds, 1f, 60f);
            SpawnDistance = Math.Clamp(SpawnDistance, 100f, 2500f);
            SpawnSearchDistance = Math.Clamp(SpawnSearchDistance, 0f, 1500f);
            MinStandardRiftLevel = Math.Max(MinStandardRiftLevel, 1);
            MinRiftGauntletWave = Math.Max(MinRiftGauntletWave, 1);
            MinBossGauntletWave = Math.Max(MinBossGauntletWave, 1);
            DiscoveryNameKeywords = NormalizeStringList(DiscoveryNameKeywords);
            BlockedNameKeywords = NormalizeStringList(BlockedNameKeywords);
            Hazards ??= new();

            foreach (MythicRiftHazardEntryTuning hazard in Hazards)
                hazard?.Normalize();
        }

        private static List<string> NormalizeStringList(List<string> values)
        {
            return values?
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new();
        }
    }

    public sealed class MythicRiftHazardEntryTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string Prototype { get; set; }
        public int Weight { get; set; } = 1;
        public int MinRiftLevel { get; set; } = 1;
        public int MaxRiftLevel { get; set; }
        public int MinWave { get; set; } = 1;
        public int MaxWave { get; set; }
        public List<string> Modes { get; set; } = new();

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = string.IsNullOrWhiteSpace(Prototype) ? "unnamed-hazard" : Prototype;

            Prototype = Prototype?.Trim() ?? string.Empty;
            Weight = Math.Clamp(Weight, 1, 20);
            MinRiftLevel = Math.Max(MinRiftLevel, 1);
            MaxRiftLevel = Math.Max(MaxRiftLevel, 0);
            MinWave = Math.Max(MinWave, 1);
            MaxWave = Math.Max(MaxWave, 0);
            Modes ??= new();
        }

        public bool AppliesTo(MythicRiftRunState runState)
        {
            if (Enabled == false || string.IsNullOrWhiteSpace(Prototype) || runState?.Config == null)
                return false;

            if (MythicRiftRewardTuning.MatchesModes(runState, Modes) == false)
                return false;

            int riftLevel = MythicRiftRewardTuning.GetRewardRiftLevel(runState);
            if (riftLevel < MinRiftLevel || (MaxRiftLevel > 0 && riftLevel > MaxRiftLevel))
                return false;

            int wave = MythicRiftRewardTuning.GetRewardWaveNumber(runState);
            if (wave < MinWave || (MaxWave > 0 && wave > MaxWave))
                return false;

            return true;
        }
    }
}
