namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftScaling
    {
        private const double EarlyRiftLevelToD3EquivalentFactor = 0.40d;
        private const double MidRiftLevelToD3EquivalentFactor = 0.28d;
        private const double LateRiftLevelToD3EquivalentFactor = 0.18d;
        private const int MidScalingStartLevel = 21;
        private const int LateScalingStartLevel = 51;
        private const float MaxStandardHealthMultiplier = 12.0f;
        private const float MaxStandardDamageMultiplier = 3.0f;
        private const float MaxThirtyWaveDamageMultiplier = 3.0f;
        private const float MaxBossGauntletHealthMultiplier = 8.0f;
        private const float MaxBossGauntletDamageMultiplier = 2.25f;
        private const int MaxBossGauntletBossCount = 6;
        private static readonly float[] RiftPartyHealthMultipliersByBucket =
        {
            1.00f,
            1.50f,
            2.00f,
            2.50f,
            3.00f
        };
        private static readonly MythicRiftWaveProfile[] ThirtyWaveProfiles =
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

        public static int GetEffectivePlayerCount(int requestedPlayerCount)
        {
            if (requestedPlayerCount <= 1)
                return 1;

            if (requestedPlayerCount >= 5)
                return 5;

            return requestedPlayerCount;
        }

        public static float GetEquivalentD3RiftLevel(int riftLevel)
        {
            int normalizedLevel = Math.Max(riftLevel, 1);
            int earlyLevels = Math.Min(normalizedLevel - 1, MidScalingStartLevel - 2);
            int midLevels = Math.Min(Math.Max(normalizedLevel - MidScalingStartLevel + 1, 0), LateScalingStartLevel - MidScalingStartLevel);
            int lateLevels = Math.Max(normalizedLevel - LateScalingStartLevel + 1, 0);

            double equivalentLevel = 1d
                + (earlyLevels * EarlyRiftLevelToD3EquivalentFactor)
                + (midLevels * MidRiftLevelToD3EquivalentFactor)
                + (lateLevels * LateRiftLevelToD3EquivalentFactor);

            return (float)equivalentLevel;
        }

        public static float GetGroupHealthMultiplier(int requestedPlayerCount)
        {
            int effectivePlayerCount = GetEffectivePlayerCount(requestedPlayerCount);
            return RiftPartyHealthMultipliersByBucket[effectivePlayerCount - 1];
        }

        public static float GetHealthMultiplier(int riftLevel)
        {
            double tiersAboveBase = Math.Max(GetEquivalentD3RiftLevel(riftLevel) - 1d, 0d);
            return (float)Math.Pow(1.17d, tiersAboveBase);
        }

        public static float GetDamageMultiplier(int riftLevel)
        {
            int normalizedLevel = Math.Max(riftLevel, 1);
            int earlyLevels = Math.Min(normalizedLevel - 1, 30);
            int midLevels = Math.Min(Math.Max(normalizedLevel - 31, 0), 40);
            int lateLevels = Math.Max(normalizedLevel - 71, 0);

            double multiplier = 1d
                + (earlyLevels * 0.035d)
                + (midLevels * 0.020d)
                + (lateLevels * 0.005d);

            return (float)multiplier;
        }

        public static float GetStandardDamageMultiplier(int riftLevel)
        {
            return Math.Min(GetDamageMultiplier(riftLevel), MaxStandardDamageMultiplier);
        }

        public static float GetThirtyWaveDamageMultiplier(MythicRiftWaveProfile waveProfile)
        {
            float baselineDamage = GetDamageMultiplier(waveProfile.Wave);
            float wavePressureDamage = MathF.Sqrt(Math.Max(waveProfile.TotalBossHealthMultiplier, 1f));
            return Math.Min(Math.Max(baselineDamage, wavePressureDamage), MaxThirtyWaveDamageMultiplier);
        }

        public static MythicRiftWaveProfile GetThirtyWaveProfile(int riftLevel)
        {
            int normalizedLevel = Math.Max(riftLevel, 1);
            int wave = ((normalizedLevel - 1) % ThirtyWaveProfiles.Length) + 1;
            return ThirtyWaveProfiles[wave - 1];
        }

        public static int GetBossGauntletBossCount(int wave)
        {
            int normalizedWave = Math.Max(wave, 1);
            return Math.Clamp(1 + ((normalizedWave - 1) / 5), 1, MaxBossGauntletBossCount);
        }

        public static MythicRiftDifficultySnapshot BuildBossGauntletSnapshot(int wave, int requestedPlayerCount)
        {
            int normalizedWave = Math.Max(wave, 1);
            int effectivePlayerCount = GetEffectivePlayerCount(requestedPlayerCount);
            float groupHealthMultiplier = Math.Min(GetGroupHealthMultiplier(requestedPlayerCount), 2.0f);
            float waveHealthMultiplier = 1f + ((normalizedWave - 1) * 0.12f);
            float finalHealthMultiplier = Math.Min(waveHealthMultiplier * groupHealthMultiplier, MaxBossGauntletHealthMultiplier);
            float finalDamageMultiplier = Math.Min(1f + ((normalizedWave - 1) * 0.025f), MaxBossGauntletDamageMultiplier);

            return new MythicRiftDifficultySnapshot(
                normalizedWave,
                effectivePlayerCount,
                normalizedWave,
                groupHealthMultiplier,
                finalHealthMultiplier,
                finalDamageMultiplier);
        }

        public static MythicRiftDifficultySnapshot BuildSnapshot(int riftLevel, int requestedPlayerCount)
        {
            return BuildSnapshot(riftLevel, requestedPlayerCount, useThirtyWaveMode: false);
        }

        public static MythicRiftDifficultySnapshot BuildSnapshot(int riftLevel, int requestedPlayerCount, MythicRiftMode mode)
        {
            if (mode == MythicRiftMode.BossGauntlet)
                return BuildBossGauntletSnapshot(riftLevel, requestedPlayerCount);

            return BuildSnapshot(riftLevel, requestedPlayerCount, mode == MythicRiftMode.Endless);
        }

        public static MythicRiftDifficultySnapshot BuildSnapshot(int riftLevel, int requestedPlayerCount, bool useThirtyWaveMode)
        {
            int effectivePlayerCount = GetEffectivePlayerCount(requestedPlayerCount);
            if (useThirtyWaveMode)
            {
                MythicRiftWaveProfile waveProfile = GetThirtyWaveProfile(riftLevel);
                return new MythicRiftDifficultySnapshot(
                    riftLevel,
                    effectivePlayerCount,
                    waveProfile.Wave,
                    1f,
                    waveProfile.PerBossHealthMultiplier,
                    GetThirtyWaveDamageMultiplier(waveProfile));
            }

            float equivalentD3RiftLevel = GetEquivalentD3RiftLevel(riftLevel);
            float groupHealthMultiplier = GetGroupHealthMultiplier(requestedPlayerCount);
            float soloHealthMultiplier = GetHealthMultiplier(riftLevel);
            float finalHealthMultiplier = Math.Min(soloHealthMultiplier * groupHealthMultiplier, MaxStandardHealthMultiplier);
            float finalDamageMultiplier = GetStandardDamageMultiplier(riftLevel);

            return new MythicRiftDifficultySnapshot(
                riftLevel,
                effectivePlayerCount,
                equivalentD3RiftLevel,
                groupHealthMultiplier,
                finalHealthMultiplier,
                finalDamageMultiplier);
        }
    }
}
