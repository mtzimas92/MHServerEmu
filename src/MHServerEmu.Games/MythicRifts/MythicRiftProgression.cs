namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftProgression
    {
        public const int EndlessCycleLength = 30;

        public static int ResolveNextUnlockedLevel(int currentUnlockedLevel, int completedLevel)
        {
            int normalizedCurrentLevel = Math.Max(currentUnlockedLevel, 1);
            int completedRunNextLevel = Math.Max(completedLevel + 1, 1);
            return Math.Max(normalizedCurrentLevel, Math.Min(normalizedCurrentLevel + 1, completedRunNextLevel));
        }

        public static int NormalizeEndlessCycleLevel(int level)
        {
            int normalizedLevel = Math.Max(level, 1);
            return ((normalizedLevel - 1) % EndlessCycleLength) + 1;
        }

        public static int ResolveNextEndlessCycleLevel(int currentLevel, int completedLevel)
        {
            int normalizedCurrentLevel = NormalizeEndlessCycleLevel(currentLevel);
            int normalizedCompletedLevel = NormalizeEndlessCycleLevel(completedLevel);
            if (normalizedCompletedLevel >= EndlessCycleLength)
                return 1;

            int completedRunNextLevel = normalizedCompletedLevel + 1;
            return Math.Min(EndlessCycleLength, Math.Max(normalizedCurrentLevel, Math.Min(normalizedCurrentLevel + 1, completedRunNextLevel)));
        }
    }
}
