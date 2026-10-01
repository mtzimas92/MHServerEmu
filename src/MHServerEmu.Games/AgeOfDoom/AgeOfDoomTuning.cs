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
    public int TimeLimitMinutes { get; set; } = 25;
    public int SpawnIntervalSeconds { get; set; } = 5;
    public int MaximumLivingEnemiesPerPlayer { get; set; } = 8;
    public float SpawnRadius { get; set; } = 700f;
    public int IncursionKillsPerPlayer { get; set; } = 18;
    public int TimelineKillsPerPlayer { get; set; } = 24;
    public int CouncilBosses { get; set; } = 2;
    public List<int> DoomCountsPerStage { get; set; } = new() { 1, 2, 3, 4 };
    public string ObjectiveWidgetPrototype { get; set; }
    public List<string> MinionPrototypes { get; set; } = new();
    public List<string> ElitePrototypes { get; set; } = new();
    public List<string> CouncilBossPrototypes { get; set; } = new();
    public List<string> DoomPhasePrototypes { get; set; } = new();

    public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

    public static AgeOfDoomTuning Load()
    {
        if (File.Exists(ConfigPath) == false)
            return new() { Enabled = false };
        return FileHelper.DeserializeJson<AgeOfDoomTuning>(ConfigPath, JsonOptions) ?? new() { Enabled = false };
    }
}
