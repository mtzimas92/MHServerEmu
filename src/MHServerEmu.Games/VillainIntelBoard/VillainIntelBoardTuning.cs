using System.Text.Json;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.System.Random;
using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.VillainIntelBoard
{
    public sealed class VillainIntelBoardTuning
    {
        public const string RelativeConfigPath = "Game/VillainIntelBoard/VillainIntelBoard.json";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };

        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public string ProfileName { get; set; } = "default";
        public bool Enabled { get; set; } = true;
        public int BoardSize { get; set; } = 6;
        public int[] RankBands { get; set; } = { 1, 3, 4, 6, 7, 10 };
        public int RerollCostCredits { get; set; } = 25000;
        public int AcceptCostCreditsPerRank { get; set; } = 2500;
        public float HealthMultiplierPerRank { get; set; } = 0.35f;
        public float DamageBonusPctPerRank { get; set; } = 0.08f;
        public float MaxHealthMultiplier { get; set; } = 5.0f;
        public float MaxDamageBonusPct { get; set; } = 0.75f;
        public List<string> NpcPrototypeNames { get; set; } = new();
        public List<VillainIntelRegionTuning> Regions { get; set; } = new();
        public List<VillainIntelThemeTuning> Themes { get; set; } = new();
        public List<VillainIntelRewardTuning> Rewards { get; set; } = new();

        public static VillainIntelBoardTuning CreateDefault()
        {
            return new()
            {
                NpcPrototypeNames =
                {
                    "Entity/Characters/NPCs/Mordo.prototype"
                }
            };
        }

        public int GetBoardSize() => Math.Clamp(BoardSize, 1, 12);

        public int GetAcceptCost(int rank) => Math.Max(0, AcceptCostCreditsPerRank * Math.Clamp(rank, 1, 10));

        public int RollRank(GRandom random, int slotIndex)
        {
            if (RankBands == null || RankBands.Length < 2)
                return 1;

            int bandCount = RankBands.Length / 2;
            int bandIndex = Math.Clamp(slotIndex % bandCount, 0, bandCount - 1);
            int min = Math.Clamp(RankBands[bandIndex * 2], 1, 10);
            int max = Math.Clamp(RankBands[(bandIndex * 2) + 1], min, 10);
            return random.Next(min, max + 1);
        }

        public float GetHealthMultiplier(int rank)
        {
            float multiplier = 1f + ((Math.Clamp(rank, 1, 10) - 1) * HealthMultiplierPerRank);
            return Math.Clamp(multiplier, 1f, Math.Max(1f, MaxHealthMultiplier));
        }

        public float GetDamageBonusPct(int rank)
        {
            float bonus = (Math.Clamp(rank, 1, 10) - 1) * DamageBonusPctPerRank;
            return Math.Clamp(bonus, 0f, Math.Max(0f, MaxDamageBonusPct));
        }
    }

    public sealed class VillainIntelRegionTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisplayName { get; set; }
        public string RegionPrototypeName { get; set; }
        public List<string> Tags { get; set; } = new();

        public PrototypeId RegionProtoRef { get; set; } = PrototypeId.Invalid;
    }

    public sealed class VillainIntelThemeTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisplayName { get; set; }
        public List<string> RegionTags { get; set; } = new();
        public List<VillainIntelTargetTuning> Targets { get; set; } = new();
    }

    public sealed class VillainIntelTargetTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisplayName { get; set; }
        public string PrototypeName { get; set; }
        public bool HeroBased { get; set; }
        public int Weight { get; set; } = 1;
        public List<string> RewardTags { get; set; } = new();

        public PrototypeId AgentProtoRef { get; set; } = PrototypeId.Invalid;
    }

    public sealed class VillainIntelRewardTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public int MinRank { get; set; } = 1;
        public int MaxRank { get; set; } = 0;
        public List<string> Tags { get; set; } = new();
        public List<string> LootTablePrototypeNames { get; set; } = new();
        public List<VillainIntelGuaranteedItemTuning> GuaranteedItems { get; set; } = new();

        public List<PrototypeId> LootTableRefs { get; } = new();

        public bool AppliesTo(int rank, IEnumerable<string> rewardTags)
        {
            if (Enabled == false)
                return false;

            if (rank < Math.Max(MinRank, 1))
                return false;

            if (MaxRank > 0 && rank > MaxRank)
                return false;

            if (Tags == null || Tags.Count == 0)
                return true;

            if (rewardTags == null)
                return false;

            HashSet<string> rewardTagSet = new(rewardTags, StringComparer.OrdinalIgnoreCase);
            return Tags.Any(tag => rewardTagSet.Contains(tag));
        }
    }

    public sealed class VillainIntelGuaranteedItemTuning
    {
        public bool Enabled { get; set; } = true;
        public string ItemPrototypeName { get; set; }
        public ulong ItemPrototypeRuntimeId { get; set; }
        public int Quantity { get; set; } = 1;
        public int MinRank { get; set; } = 1;
        public int MaxRank { get; set; } = 0;

        public PrototypeId ItemProtoRef { get; set; } = PrototypeId.Invalid;

        public bool AppliesTo(int rank)
        {
            if (Enabled == false || rank < Math.Max(MinRank, 1))
                return false;

            return MaxRank <= 0 || rank <= MaxRank;
        }
    }
}
