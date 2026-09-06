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
        public float MaxThirtyWaveDamageMultiplier { get; set; } = 2.4f;
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
        public List<MythicRiftWaveProfileTuning> ThirtyWaveProfiles { get; set; } = CreateDefaultThirtyWaveProfiles();

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
            MaxThirtyWaveDamageMultiplier = Math.Max(MaxThirtyWaveDamageMultiplier, 0.01f);
            MaxBossGauntletHealthMultiplier = Math.Max(MaxBossGauntletHealthMultiplier, 0.01f);
            MaxBossGauntletDamageMultiplier = Math.Max(MaxBossGauntletDamageMultiplier, 0.01f);
            MaxBossGauntletBossCount = Math.Clamp(MaxBossGauntletBossCount, 1, 20);
            BossGauntletGroupHealthMultiplierCap = Math.Max(BossGauntletGroupHealthMultiplierCap, 0.01f);
            BossGauntletHealthStepPerWave = Math.Max(BossGauntletHealthStepPerWave, 0f);
            BossGauntletDamageStepPerWave = Math.Max(BossGauntletDamageStepPerWave, 0f);

            RiftPartyHealthMultipliersByBucket = NormalizeMultiplierList(RiftPartyHealthMultipliersByBucket, new[] { 1.00f, 1.50f, 2.00f, 2.50f, 3.00f });
            ThirtyWaveProfiles = NormalizeThirtyWaveProfiles(ThirtyWaveProfiles);
        }

        public float GetGroupHealthMultiplier(int requestedPlayerCount)
        {
            int effectivePlayerCount = MythicRiftScaling.GetEffectivePlayerCount(requestedPlayerCount);
            int index = Math.Clamp(effectivePlayerCount - 1, 0, RiftPartyHealthMultipliersByBucket.Count - 1);
            return RiftPartyHealthMultipliersByBucket[index];
        }

        public MythicRiftWaveProfile GetThirtyWaveProfile(int riftLevel)
        {
            int normalizedLevel = Math.Max(riftLevel, 1);
            int wave = ((normalizedLevel - 1) % ThirtyWaveProfiles.Count) + 1;
            MythicRiftWaveProfileTuning profile = ThirtyWaveProfiles[wave - 1];
            return new MythicRiftWaveProfile(profile.Wave, profile.BossCount, profile.PerBossHealthMultiplier);
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

        private static List<MythicRiftWaveProfileTuning> NormalizeThirtyWaveProfiles(List<MythicRiftWaveProfileTuning> profiles)
        {
            List<MythicRiftWaveProfileTuning> normalized = profiles?
                .Where(profile => profile != null)
                .OrderBy(profile => profile.Wave)
                .ToList() ?? new();

            if (normalized.Count == 0)
                normalized = CreateDefaultThirtyWaveProfiles();

            for (int i = 0; i < normalized.Count; i++)
            {
                normalized[i].Normalize(i + 1);
            }

            return normalized;
        }

        private static List<MythicRiftWaveProfileTuning> CreateDefaultThirtyWaveProfiles()
        {
            return new()
            {
                new(1, 1, 1.00f),
                new(2, 1, 1.50f),
                new(3, 1, 2.00f),
                new(4, 1, 2.50f),
                new(5, 1, 3.00f),
                new(6, 1, 3.50f),
                new(7, 1, 4.00f),
                new(8, 1, 4.50f),
                new(9, 1, 5.00f),
                new(10, 2, 2.50f),
                new(11, 2, 3.00f),
                new(12, 2, 3.50f),
                new(13, 2, 4.00f),
                new(14, 2, 4.50f),
                new(15, 2, 5.00f),
                new(16, 2, 5.50f),
                new(17, 2, 6.00f),
                new(18, 2, 6.50f),
                new(19, 2, 7.00f),
                new(20, 3, 5.00f),
                new(21, 3, 5.50f),
                new(22, 3, 6.00f),
                new(23, 3, 6.50f),
                new(24, 3, 7.00f),
                new(25, 4, 5.00f),
                new(26, 4, 5.50f),
                new(27, 4, 6.00f),
                new(28, 5, 6.50f),
                new(29, 5, 7.00f),
                new(30, 6, 7.00f)
            };
        }
    }

    public sealed class MythicRiftWaveProfileTuning
    {
        public MythicRiftWaveProfileTuning()
        {
        }

        public MythicRiftWaveProfileTuning(int wave, int bossCount, float perBossHealthMultiplier)
        {
            Wave = wave;
            BossCount = bossCount;
            PerBossHealthMultiplier = perBossHealthMultiplier;
        }

        public int Wave { get; set; }
        public int BossCount { get; set; }
        public float PerBossHealthMultiplier { get; set; }

        public void Normalize(int fallbackWave)
        {
            Wave = Math.Max(Wave, fallbackWave);
            BossCount = Math.Max(BossCount, 1);
            PerBossHealthMultiplier = Math.Max(PerBossHealthMultiplier, 0.01f);
        }
    }
}
