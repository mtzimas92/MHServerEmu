using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftContentPoolTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/MythicRiftContentPool.json";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };

        public List<MythicRiftContentPoolEntryTuning> Content { get; set; } = new();

        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);
    }

    /// <summary>JSON schema for one entry in MythicRiftContentPool.json - mirrors the fields of the
    /// (now removed) hardcoded MythicRiftContentDefinition record, so this is a pure data move,
    /// not a behavior change.</summary>
    public sealed class MythicRiftContentPoolEntryTuning
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int DefaultKillQuota { get; set; }
        public string RegionPrototypeName { get; set; }
        public string MissionPrototypeName { get; set; }
        public string BossPrototypeName { get; set; }
        public string BossLootTablePrototypeName { get; set; }
        public bool RandomMapEligible { get; set; } = true;
        public bool RandomBossEligible { get; set; } = true;
        public bool IsSpecialRandomMap { get; set; }
        public bool UseOwnBossSourceWhenSelected { get; set; }
        public bool UseCustomPopulation { get; set; }
        public bool BossOnlyCheckpointEligible { get; set; }
        public string BossFamily { get; set; }
        public int MinRandomRiftLevel { get; set; } = 1;
        public int MaxRandomRiftLevel { get; set; }
        public int MaxPlayerCount { get; set; }
    }
}
