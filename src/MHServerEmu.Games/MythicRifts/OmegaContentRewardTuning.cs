using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class OmegaContentRewardTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/OmegaContentRewards.json";
        public bool Enabled { get; set; } = true;
        public List<OmegaContentActivityTuning> Activities { get; set; } = new();
        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static OmegaContentRewardTuning Load()
        {
            OmegaContentRewardTuning tuning = FileHelper.DeserializeJson<OmegaContentRewardTuning>(ConfigPath, MythicRiftRewardTuning.JsonOptions)
                ?? new OmegaContentRewardTuning { Enabled = false };
            tuning.Activities ??= new();
            foreach (OmegaContentActivityTuning activity in tuning.Activities)
                activity?.Normalize();
            return tuning;
        }
    }

    public sealed class OmegaContentActivityTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string CompletionMission { get; set; }
        public string RequiredDifficulty { get; set; } = "Difficulty/Tiers/Tier5Omega1.prototype";
        public List<OmegaContentRewardEntryTuning> Rewards { get; set; } = new();

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? CompletionMission?.Trim() : Id.Trim();
            CompletionMission = CompletionMission?.Trim();
            RequiredDifficulty = RequiredDifficulty?.Trim();
            Rewards ??= new();
            foreach (OmegaContentRewardEntryTuning reward in Rewards)
                reward?.Normalize(Id);
        }
    }

    public sealed class OmegaContentRewardEntryTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string ItemPrototype { get; set; }
        public ulong ItemPrototypeRuntimeId { get; set; }
        public int Quantity { get; set; } = 1;
        public float ChancePercent { get; set; } = 100f;
        public string Period { get; set; }
        public int PeriodLimit { get; set; }
        public int ItemLevel { get; set; } = 1;

        public void Normalize(string activityId)
        {
            ItemPrototype = ItemPrototype?.Trim();
            Id = string.IsNullOrWhiteSpace(Id) ? $"{activityId}:{ItemPrototypeRuntimeId}:{ItemPrototype}" : Id.Trim();
            Quantity = Math.Max(Quantity, 0);
            ChancePercent = Math.Clamp(ChancePercent, 0f, 100f);
            Period = string.Equals(Period?.Trim(), "weekly", StringComparison.OrdinalIgnoreCase) ? "weekly" : string.Empty;
            PeriodLimit = Math.Max(PeriodLimit, 0);
            ItemLevel = Math.Max(ItemLevel, 1);
        }
    }
}
