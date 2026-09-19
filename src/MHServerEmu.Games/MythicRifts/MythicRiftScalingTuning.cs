using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftScalingTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/CosmicRiftScaling.json";

        private static readonly object TuningLock = new();
        private static MythicRiftScalingTuning _cachedTuning = CreateDefault();
        private static DateTime _cachedWriteTimeUtc = DateTime.MinValue;
        private static bool _cachedFileExists;

        public string ProfileName { get; set; } = "default-scaling";
        public bool Enabled { get; set; } = true;
        public double EarlyRiftLevelToD3EquivalentFactor { get; set; } = 0.40d;
        public double MidRiftLevelToD3EquivalentFactor { get; set; } = 0.28d;
        public double LateRiftLevelToD3EquivalentFactor { get; set; } = 0.18d;
        public int MidScalingStartLevel { get; set; } = 21;
        public int LateScalingStartLevel { get; set; } = 51;
        public float StandardHealthGrowthBase { get; set; } = 1.17f;
        public float MaxStandardHealthMultiplier { get; set; } = 12.0f;
        public float StandardDamageEarlyStep { get; set; } = 0.030f;
        public float StandardDamageMidStep { get; set; } = 0.015f;
        public float StandardDamageLateStep { get; set; } = 0.004f;
        public int StandardDamageMidStartLevel { get; set; } = 31;
        public int StandardDamageLateStartLevel { get; set; } = 71;
        public float MaxStandardDamageMultiplier { get; set; } = 2.4f;
        public float MaxBossGauntletHealthMultiplier { get; set; } = 7.0f;
        public float MaxBossGauntletDamageMultiplier { get; set; } = 2.0f;
        public int MaxBossGauntletBossCount { get; set; } = 5;
        public float BossGauntletGroupHealthMultiplierCap { get; set; } = 1.75f;
        public float BossGauntletHealthStepPerWave { get; set; } = 0.10f;
        public float BossGauntletDamageStepPerWave { get; set; } = 0.018f;
        public List<float> RiftPartyHealthMultipliersByBucket { get; set; } = new()
        {
            1.00f,
            1.50f,
            2.00f,
            2.50f,
            3.00f
        };
        public Dictionary<string, MythicRiftModeScalingTuning> Modes { get; set; } = CreateDefaultModes();

        public static JsonSerializerOptions JsonOptions => MythicRiftRewardTuning.JsonOptions;
        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static MythicRiftScalingTuning CreateDefault()
        {
            MythicRiftScalingTuning tuning = new();
            tuning.Normalize();
            return tuning;
        }

        public static MythicRiftScalingTuning Load()
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

                MythicRiftScalingTuning tuning = CreateDefault();

                if (fileExists)
                {
                    MythicRiftScalingTuning loadedTuning = FileHelper.DeserializeJson<MythicRiftScalingTuning>(configPath, JsonOptions);
                    if (loadedTuning != null && loadedTuning.Enabled)
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

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "default-scaling";

            EarlyRiftLevelToD3EquivalentFactor = Math.Max(EarlyRiftLevelToD3EquivalentFactor, 0d);
            MidRiftLevelToD3EquivalentFactor = Math.Max(MidRiftLevelToD3EquivalentFactor, 0d);
            LateRiftLevelToD3EquivalentFactor = Math.Max(LateRiftLevelToD3EquivalentFactor, 0d);
            MidScalingStartLevel = Math.Max(MidScalingStartLevel, 2);
            LateScalingStartLevel = Math.Max(LateScalingStartLevel, MidScalingStartLevel + 1);
            StandardHealthGrowthBase = Math.Max(StandardHealthGrowthBase, 1.0f);
            MaxStandardHealthMultiplier = Math.Max(MaxStandardHealthMultiplier, 0.01f);
            StandardDamageEarlyStep = Math.Max(StandardDamageEarlyStep, 0f);
            StandardDamageMidStep = Math.Max(StandardDamageMidStep, 0f);
            StandardDamageLateStep = Math.Max(StandardDamageLateStep, 0f);
            StandardDamageMidStartLevel = Math.Max(StandardDamageMidStartLevel, 2);
            StandardDamageLateStartLevel = Math.Max(StandardDamageLateStartLevel, StandardDamageMidStartLevel + 1);
            MaxStandardDamageMultiplier = Math.Max(MaxStandardDamageMultiplier, 0.01f);
            MaxBossGauntletHealthMultiplier = Math.Max(MaxBossGauntletHealthMultiplier, 0.01f);
            MaxBossGauntletDamageMultiplier = Math.Max(MaxBossGauntletDamageMultiplier, 0.01f);
            MaxBossGauntletBossCount = Math.Clamp(MaxBossGauntletBossCount, 1, 20);
            BossGauntletGroupHealthMultiplierCap = Math.Max(BossGauntletGroupHealthMultiplierCap, 0.01f);
            BossGauntletHealthStepPerWave = Math.Max(BossGauntletHealthStepPerWave, 0f);
            BossGauntletDamageStepPerWave = Math.Max(BossGauntletDamageStepPerWave, 0f);

            RiftPartyHealthMultipliersByBucket = NormalizeMultiplierList(RiftPartyHealthMultipliersByBucket, new[] { 1.00f, 1.50f, 2.00f, 2.50f, 3.00f });
            Modes ??= CreateDefaultModes();
            foreach (MythicRiftMode mode in Enum.GetValues<MythicRiftMode>())
            {
                string key = mode.ToString();
                if (Modes.TryGetValue(key, out MythicRiftModeScalingTuning modeTuning) == false || modeTuning == null)
                    Modes[key] = CreateDefaultModes()[key];
                Modes[key].Normalize();
            }
        }

        public MythicRiftModeScalingTuning GetMode(MythicRiftMode mode)
        {
            return Modes.TryGetValue(mode.ToString(), out MythicRiftModeScalingTuning tuning)
                ? tuning
                : CreateDefaultModes()[mode.ToString()];
        }

        public MythicRiftLevelScalingTuning GetLevel(MythicRiftMode mode, int level)
        {
            MythicRiftModeScalingTuning modeTuning = GetMode(mode);
            int normalizedLevel = Math.Max(level, 1);
            return modeTuning.Levels.LastOrDefault(entry => entry.Level <= normalizedLevel);
        }

        public string GetDifficultyTierPrototypeName(MythicRiftMode mode, int level) => GetLevel(mode, level)?.DifficultyTierPrototype;
        public int GetBossCount(MythicRiftMode mode, int level) => Math.Max(GetLevel(mode, level)?.BossCount ?? 1, 1);
        public TimeSpan GetTimeLimit(MythicRiftMode mode) => TimeSpan.FromMinutes(Math.Max(GetMode(mode).TimeLimitMinutes, 1));

        private static Dictionary<string, MythicRiftModeScalingTuning> CreateDefaultModes()
        {
            return new(StringComparer.OrdinalIgnoreCase)
            {
                [MythicRiftMode.Standard.ToString()] = MythicRiftModeScalingTuning.CreateStandard(),
                [MythicRiftMode.Endless.ToString()] = MythicRiftModeScalingTuning.CreateEndless(),
                [MythicRiftMode.BossGauntlet.ToString()] = MythicRiftModeScalingTuning.CreateBossGauntlet()
            };
        }

        public float GetGroupHealthMultiplier(int requestedPlayerCount)
        {
            int effectivePlayerCount = MythicRiftScaling.GetEffectivePlayerCount(requestedPlayerCount);
            int index = Math.Clamp(effectivePlayerCount - 1, 0, RiftPartyHealthMultipliersByBucket.Count - 1);
            return RiftPartyHealthMultipliersByBucket[index];
        }

        private static List<float> NormalizeMultiplierList(List<float> values, IReadOnlyList<float> fallback)
        {
            List<float> normalized = values?
                .Where(value => value > 0f)
                .Select(value => Math.Max(value, 0.01f))
                .ToList() ?? new();

            if (normalized.Count == 0)
                normalized.AddRange(fallback);

            while (normalized.Count < 5)
                normalized.Add(normalized[^1]);

            if (normalized.Count > 5)
                normalized = normalized.Take(5).ToList();

            return normalized;
        }

    }

    public sealed class MythicRiftModeScalingTuning
    {
        public bool AllowParty { get; set; } = true;
        public int TimeLimitMinutes { get; set; } = 10;
        public bool ApplyCheckpointBossHealthMultiplier { get; set; }
        public List<MythicRiftLevelScalingTuning> Levels { get; set; } = new();

        public void Normalize()
        {
            TimeLimitMinutes = Math.Max(TimeLimitMinutes, 1);
            Levels = Levels?.Where(entry => entry != null).OrderBy(entry => entry.Level).ToList() ?? new();
            if (Levels.Count == 0)
                Levels.Add(new MythicRiftLevelScalingTuning());
            foreach (MythicRiftLevelScalingTuning level in Levels)
                level.Normalize();
        }

        public static MythicRiftModeScalingTuning CreateStandard() => new()
        {
            AllowParty = false,
            TimeLimitMinutes = 5,
            Levels = MythicRiftLevelScalingTuning.CreateDifficultyBands(includeBossCounts: false, includeInfiniteGrowth: true)
        };

        public static MythicRiftModeScalingTuning CreateEndless() => new()
        {
            AllowParty = true,
            TimeLimitMinutes = 10,
            Levels = new()
            {
                new(1, "Difficulty/Tiers/Tier2Heroic.prototype", 1f, 1f, 1),
                new(5, "Difficulty/CosmicGate.prototype", 1f, 1f, 1),
                new(10, "Difficulty/Tiers/Tier3Superheroic.prototype", 1f, 1f, 2),
                new(16, "Difficulty/Tiers/Tier4Cosmic.prototype", 1f, 1f, 2),
                new(20, "Difficulty/Tiers/Tier4Cosmic.prototype", 1f, 1f, 3),
                new(25, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 4),
                new(28, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 5),
                new(30, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 6)
            }
        };

        public static MythicRiftModeScalingTuning CreateBossGauntlet() => new()
        {
            AllowParty = true,
            TimeLimitMinutes = 1440,
            Levels = new()
            {
                new(1, "Difficulty/Tiers/Tier3Superheroic.prototype", 1f, 1f, 1),
                new(6, "Difficulty/Tiers/Tier3Superheroic.prototype", 1f, 1f, 2),
                new(7, "Difficulty/Tiers/Tier4Cosmic.prototype", 1f, 1f, 2),
                new(11, "Difficulty/Tiers/Tier4Cosmic.prototype", 1f, 1f, 3),
                new(16, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 4),
                new(21, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 5),
                new(26, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 6),
                new(31, "Difficulty/Tiers/Tier5Omega1.prototype", 1.1284f, 1.0514f, 7),
                new(32, "Difficulty/Tiers/Tier5Omega1.prototype", 1.2527f, 1.1011f, 7),
                new(33, "Difficulty/Tiers/Tier5Omega1.prototype", 1.3732f, 1.1493f, 7),
                new(34, "Difficulty/Tiers/Tier5Omega1.prototype", 1.4900f, 1.1960f, 7),
                new(35, "Difficulty/Tiers/Tier5Omega1.prototype", 1.6035f, 1.2414f, 7),
                new(36, "Difficulty/Tiers/Tier5Omega1.prototype", 1.7138f, 1.2855f, 8),
                new(37, "Difficulty/Tiers/Tier5Omega1.prototype", 1.8211f, 1.3284f, 8),
                new(38, "Difficulty/Tiers/Tier5Omega1.prototype", 1.9255f, 1.3702f, 8),
                new(39, "Difficulty/Tiers/Tier5Omega1.prototype", 2.0272f, 1.4109f, 8),
                new(40, "Difficulty/Tiers/Tier5Omega1.prototype", 2.1263f, 1.4505f, 8),
                new(41, "Difficulty/Tiers/Tier5Omega1.prototype", 2.2230f, 1.4892f, 9),
                new(42, "Difficulty/Tiers/Tier5Omega1.prototype", 2.3174f, 1.5269f, 9),
                new(43, "Difficulty/Tiers/Tier5Omega1.prototype", 2.4095f, 1.5638f, 9),
                new(44, "Difficulty/Tiers/Tier5Omega1.prototype", 2.4995f, 1.5998f, 9),
                new(45, "Difficulty/Tiers/Tier5Omega1.prototype", 2.5875f, 1.6350f, 9),
                new(46, "Difficulty/Tiers/Tier5Omega1.prototype", 2.6735f, 1.6694f, 9),
                new(47, "Difficulty/Tiers/Tier5Omega1.prototype", 2.7577f, 1.7031f, 9),
                new(48, "Difficulty/Tiers/Tier5Omega1.prototype", 2.8402f, 1.7361f, 9),
                new(49, "Difficulty/Tiers/Tier5Omega1.prototype", 2.9209f, 1.7684f, 9),
                new(50, "Difficulty/Tiers/Tier5Omega1.prototype", 3.0000f, 1.8000f, 10)
            }
        };
    }

    public sealed class MythicRiftLevelScalingTuning
    {
        public int Level { get; set; } = 1;
        public string DifficultyTierPrototype { get; set; } = "Difficulty/Tiers/Tier2Heroic.prototype";
        public float HealthMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;
        public int BossCount { get; set; } = 1;

        public MythicRiftLevelScalingTuning() { }
        public MythicRiftLevelScalingTuning(int level, string tier, float health, float damage, int bossCount)
        {
            Level = level;
            DifficultyTierPrototype = tier;
            HealthMultiplier = health;
            DamageMultiplier = damage;
            BossCount = bossCount;
        }

        public void Normalize()
        {
            Level = Math.Max(Level, 1);
            HealthMultiplier = Math.Max(HealthMultiplier, 0.01f);
            DamageMultiplier = Math.Max(DamageMultiplier, 0.01f);
            BossCount = Math.Clamp(BossCount, 1, 20);
        }

        public static List<MythicRiftLevelScalingTuning> CreateDifficultyBands(bool includeBossCounts, bool includeInfiniteGrowth)
        {
            List<MythicRiftLevelScalingTuning> levels = new()
            {
                new(1, "Difficulty/Tiers/Tier2Heroic.prototype", 1f, 1f, 1),
                new(5, "Difficulty/CosmicGate.prototype", 1f, 1f, 1),
                new(10, "Difficulty/Tiers/Tier3Superheroic.prototype", 1f, 1f, 1),
                new(16, "Difficulty/Tiers/Tier4Cosmic.prototype", 1f, 1f, 1),
                new(25, "Difficulty/Tiers/Tier5Omega1.prototype", 1f, 1f, 1)
            };
            if (includeInfiniteGrowth)
            {
                float health = 1f;
                for (int level = 31; level <= 48; level++)
                {
                    health *= 1.05f;
                    levels.Add(new(level, "Difficulty/Tiers/Tier5Omega1.prototype", health, 1f, 1));
                }
            }
            return levels;
        }
    }

}
