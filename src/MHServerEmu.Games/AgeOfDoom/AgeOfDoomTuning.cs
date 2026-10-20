using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.AgeOfDoom;

public sealed class AgeOfDoomTuning
{
    public const string RelativeConfigPath = "Game/AgeOfDoom/AgeOfDoom.json";
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public bool Enabled { get; set; } = true;
    public ulong MetaGamePrototypeId { get; set; } = 2140634644043404383;
    public ulong RegionPrototypeId { get; set; } = 9322383157057886124;
    public ulong DifficultyTierPrototypeId { get; set; } = 586640101754933627;
    public ulong ModeNameLocaleStringId { get; set; } = 18000000000000080300;
    public int TimeLimitMinutes { get; set; } = 25;
    public int SpawnIntervalSeconds { get; set; } = 5;
    public int MaximumLivingEnemiesPerPlayer { get; set; } = 8;
    public float SpawnRadius { get; set; } = 700f;
    public int IncursionKillsPerPlayer { get; set; } = 18;
    public int CouncilBosses { get; set; } = 4;
    public int MidtownDoomBosses { get; set; } = 8;
    public string MidtownDoomPrototype { get; set; } = "Entity/Characters/Bosses/PatrolMidtown/MidtownEventDoctorDoom.prototype";
    public string ObjectiveWidgetPrototype { get; set; }
    public int CinematicTimeoutSeconds { get; set; } = 180;
    public List<string> CinematicPrototypes { get; set; } = new();
    public List<string> MinionPrototypes { get; set; } = new();
    public List<string> CouncilBossPrototypes { get; set; } = new();
    public List<string> DoomPhasePrototypes { get; set; } = new();
    public List<AgeOfDoomCompletionRewardTuning> CompletionRewards { get; set; } = new();

    public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

    public static AgeOfDoomTuning Load()
    {
        if (File.Exists(ConfigPath) == false)
            return new() { Enabled = false };

        AgeOfDoomTuning tuning = FileHelper.DeserializeJson<AgeOfDoomTuning>(ConfigPath, JsonOptions) ?? new() { Enabled = false };
        tuning.CinematicPrototypes ??= new();
        tuning.CompletionRewards ??= new();
        foreach (AgeOfDoomCompletionRewardTuning reward in tuning.CompletionRewards)
            reward?.Normalize();
        return tuning;
    }
}

public sealed class AgeOfDoomCompletionRewardTuning
{
    public string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string LootTablePrototype { get; set; }
    public int Rolls { get; set; } = 1;
    public string ItemPrototype { get; set; }
    public int Quantity { get; set; } = 1;
    public int ItemLevel { get; set; }
    public string RarityPrototype { get; set; }
    public float ChancePercent { get; set; } = 100f;
    public string Delivery { get; set; } = "ground";

    public void Normalize()
    {
        Id = Id?.Trim();
        LootTablePrototype = LootTablePrototype?.Trim();
        ItemPrototype = ItemPrototype?.Trim();
        RarityPrototype = RarityPrototype?.Trim();
        Rolls = Math.Max(Rolls, 1);
        Quantity = Math.Max(Quantity, 1);
        ItemLevel = Math.Max(ItemLevel, 0);
        ChancePercent = Math.Clamp(ChancePercent, 0f, 100f);
        Delivery = string.Equals(Delivery?.Trim(), "inventory", StringComparison.OrdinalIgnoreCase)
            ? "inventory"
            : "ground";
    }
}
