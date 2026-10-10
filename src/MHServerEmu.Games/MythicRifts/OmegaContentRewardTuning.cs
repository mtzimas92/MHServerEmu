using System.Text.Json;
using System.Text.Json.Serialization;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class OmegaContentRewardTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/OmegaContentRewards.json";
        public bool Enabled { get; set; } = true;
        public bool EnableDiagnostics { get; set; }
        public List<OmegaContentActivityTuning> Activities { get; set; } = new();
        [JsonIgnore] public Dictionary<PrototypeId, List<OmegaContentActivityTuning>> ActivitiesByMission { get; } = new();
        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        private static readonly Lazy<OmegaContentRewardTuning> CachedTuning = new(LoadCore);

        public static OmegaContentRewardTuning Load() => CachedTuning.Value;

        private static OmegaContentRewardTuning LoadCore()
        {
            OmegaContentRewardTuning tuning = FileHelper.DeserializeJson<OmegaContentRewardTuning>(ConfigPath, MythicRiftRewardTuning.JsonOptions)
                ?? new OmegaContentRewardTuning { Enabled = false };
            tuning.Activities ??= new();
            foreach (OmegaContentActivityTuning activity in tuning.Activities)
            {
                activity?.Normalize();
                if (activity?.Enabled != true || activity.CompletionMissionRef == PrototypeId.Invalid)
                    continue;

                if (tuning.ActivitiesByMission.TryGetValue(activity.CompletionMissionRef, out List<OmegaContentActivityTuning> activities) == false)
                {
                    activities = new();
                    tuning.ActivitiesByMission[activity.CompletionMissionRef] = activities;
                }

                activities.Add(activity);
            }
            return tuning;
        }

        public IReadOnlyList<OmegaContentActivityTuning> GetActivities(PrototypeId missionRef)
        {
            return ActivitiesByMission.TryGetValue(missionRef, out List<OmegaContentActivityTuning> activities)
                ? activities
                : Array.Empty<OmegaContentActivityTuning>();
        }
    }

    public sealed class OmegaContentActivityTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string CompletionMission { get; set; }
        public string RequiredDifficulty { get; set; } = "Difficulty/Tiers/Tier5Omega1.prototype";
        public List<OmegaContentRewardEntryTuning> Rewards { get; set; } = new();
        [JsonIgnore] public PrototypeId CompletionMissionRef { get; private set; }
        [JsonIgnore] public PrototypeId RequiredDifficultyRef { get; private set; }

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? CompletionMission?.Trim() : Id.Trim();
            CompletionMission = CompletionMission?.Trim();
            RequiredDifficulty = RequiredDifficulty?.Trim();
            CompletionMissionRef = string.IsNullOrWhiteSpace(CompletionMission)
                ? PrototypeId.Invalid
                : GameDatabase.GetPrototypeRefByName(CompletionMission);
            RequiredDifficultyRef = string.IsNullOrWhiteSpace(RequiredDifficulty)
                ? PrototypeId.Invalid
                : GameDatabase.GetPrototypeRefByName(RequiredDifficulty);
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
        [JsonIgnore] public PrototypeId ItemPrototypeRef { get; private set; }

        public void Normalize(string activityId)
        {
            ItemPrototype = ItemPrototype?.Trim();
            ItemPrototypeRef = ItemPrototypeRuntimeId != 0
                ? (PrototypeId)ItemPrototypeRuntimeId
                : string.IsNullOrWhiteSpace(ItemPrototype)
                    ? PrototypeId.Invalid
                    : GameDatabase.GetPrototypeRefByName(ItemPrototype);
            Id = string.IsNullOrWhiteSpace(Id) ? $"{activityId}:{ItemPrototypeRuntimeId}:{ItemPrototype}" : Id.Trim();
            Quantity = Math.Max(Quantity, 0);
            ChancePercent = Math.Clamp(ChancePercent, 0f, 100f);
            Period = string.Equals(Period?.Trim(), "weekly", StringComparison.OrdinalIgnoreCase) ? "weekly" : string.Empty;
            PeriodLimit = Math.Max(PeriodLimit, 0);
            ItemLevel = Math.Max(ItemLevel, 1);
        }
    }
}
