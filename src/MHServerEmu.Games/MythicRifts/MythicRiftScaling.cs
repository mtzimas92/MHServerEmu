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

        public static int GetBossGauntletBossCount(int wave)
        {
            return MythicRiftScalingTuning.Load().GetBossCount(MythicRiftMode.BossGauntlet, wave);
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
            return BuildSnapshot(riftLevel, requestedPlayerCount, MythicRiftMode.Standard);
        }

        public static MythicRiftDifficultySnapshot BuildSnapshot(int riftLevel, int requestedPlayerCount, MythicRiftMode mode)
        {
            MythicRiftScalingTuning tuning = MythicRiftScalingTuning.Load();
            MythicRiftLevelScalingTuning levelTuning = tuning.GetLevel(mode, riftLevel);
            if (levelTuning != null)
            {
                int effectivePlayerCount = GetEffectivePlayerCount(requestedPlayerCount);
                float groupHealthMultiplier = 1f;
                return new MythicRiftDifficultySnapshot(
                    Math.Max(riftLevel, 1),
                    effectivePlayerCount,
                    Math.Max(riftLevel, 1),
                    groupHealthMultiplier,
                    Math.Max(levelTuning.HealthMultiplier * groupHealthMultiplier, 0.01f),
                    Math.Max(levelTuning.DamageMultiplier, 0.01f));
            }

            if (mode == MythicRiftMode.BossGauntlet)
                return BuildBossGauntletSnapshot(riftLevel, requestedPlayerCount);

            return BuildSnapshot(riftLevel, requestedPlayerCount, mode == MythicRiftMode.Endless);
        }

        public static MythicRiftDifficultySnapshot BuildSnapshot(int riftLevel, int requestedPlayerCount, bool useThirtyWaveMode)
        {
            return BuildSnapshot(
                riftLevel,
                requestedPlayerCount,
                useThirtyWaveMode ? MythicRiftMode.Endless : MythicRiftMode.Standard);
        }
    }
}
