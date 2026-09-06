namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftScaling
    {
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
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            int normalizedLevel = Math.Max(riftLevel, 1);
            int earlyLevels = Math.Min(normalizedLevel - 1, tuning.MidScalingStartLevel - 2);
            int midLevels = Math.Min(Math.Max(normalizedLevel - tuning.MidScalingStartLevel + 1, 0), tuning.LateScalingStartLevel - tuning.MidScalingStartLevel);
            int lateLevels = Math.Max(normalizedLevel - tuning.LateScalingStartLevel + 1, 0);

            double equivalentLevel = 1d
                + (earlyLevels * tuning.EarlyRiftLevelToD3EquivalentFactor)
                + (midLevels * tuning.MidRiftLevelToD3EquivalentFactor)
                + (lateLevels * tuning.LateRiftLevelToD3EquivalentFactor);

            return (float)equivalentLevel;
        }

        public static float GetGroupHealthMultiplier(int requestedPlayerCount)
        {
            return MythicRiftScalingTuning.Load().GetGroupHealthMultiplier(requestedPlayerCount);
        }

        public static float GetHealthMultiplier(int riftLevel)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            double tiersAboveBase = Math.Max(GetEquivalentD3RiftLevel(riftLevel) - 1d, 0d);
            return (float)Math.Pow(tuning.StandardHealthGrowthBase, tiersAboveBase);
        }

        public static float GetDamageMultiplier(int riftLevel)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            int normalizedLevel = Math.Max(riftLevel, 1);
            int earlyLevels = Math.Min(normalizedLevel - 1, tuning.StandardDamageMidStartLevel - 1);
            int midLevels = Math.Min(Math.Max(normalizedLevel - tuning.StandardDamageMidStartLevel, 0), tuning.StandardDamageLateStartLevel - tuning.StandardDamageMidStartLevel);
            int lateLevels = Math.Max(normalizedLevel - tuning.StandardDamageLateStartLevel, 0);

            double multiplier = 1d
                + (earlyLevels * tuning.StandardDamageEarlyStep)
                + (midLevels * tuning.StandardDamageMidStep)
                + (lateLevels * tuning.StandardDamageLateStep);

            return (float)multiplier;
        }

        public static float GetStandardDamageMultiplier(int riftLevel)
        {
            return Math.Min(GetDamageMultiplier(riftLevel), MythicRiftScalingTuning.Load().MaxStandardDamageMultiplier);
        }

        public static float GetThirtyWaveDamageMultiplier(MythicRiftWaveProfile waveProfile)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            float baselineDamage = GetDamageMultiplier(waveProfile.Wave);
            float wavePressureDamage = MathF.Sqrt(Math.Max(waveProfile.TotalBossHealthMultiplier, 1f));
            return Math.Min(Math.Max(baselineDamage, wavePressureDamage), tuning.MaxThirtyWaveDamageMultiplier);
        }

        public static MythicRiftWaveProfile GetThirtyWaveProfile(int riftLevel)
        {
            return MythicRiftScalingTuning.Load().GetThirtyWaveProfile(riftLevel);
        }

        public static int GetBossGauntletBossCount(int wave)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            int normalizedWave = Math.Max(wave, 1);
            return Math.Clamp(1 + ((normalizedWave - 1) / 6), 1, tuning.MaxBossGauntletBossCount);
        }

        public static MythicRiftDifficultySnapshot BuildBossGauntletSnapshot(int wave, int requestedPlayerCount)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            int normalizedWave = Math.Max(wave, 1);
            int effectivePlayerCount = GetEffectivePlayerCount(requestedPlayerCount);
            float groupHealthMultiplier = Math.Min(GetGroupHealthMultiplier(requestedPlayerCount), tuning.BossGauntletGroupHealthMultiplierCap);
            float waveHealthMultiplier = 1f + ((normalizedWave - 1) * tuning.BossGauntletHealthStepPerWave);
            float finalHealthMultiplier = Math.Min(waveHealthMultiplier * groupHealthMultiplier, tuning.MaxBossGauntletHealthMultiplier);
            float finalDamageMultiplier = Math.Min(1f + ((normalizedWave - 1) * tuning.BossGauntletDamageStepPerWave), tuning.MaxBossGauntletDamageMultiplier);

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
            float finalHealthMultiplier = Math.Min(soloHealthMultiplier * groupHealthMultiplier, MythicRiftScalingTuning.Load().MaxStandardHealthMultiplier);
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
