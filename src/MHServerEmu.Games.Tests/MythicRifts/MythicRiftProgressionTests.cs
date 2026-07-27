using MHServerEmu.Games.MythicRifts;

namespace MHServerEmu.Games.Tests.MythicRifts
{
    public class MythicRiftProgressionTests
    {
        [Theory]
        [InlineData(1, 50, 2)]
        [InlineData(10, 50, 11)]
        [InlineData(25, 25, 26)]
        [InlineData(30, 20, 30)]
        public void ResolveNextUnlockedLevel_AdvancesAtMostOnePersonalLevel(int currentLevel, int completedLevel, int expectedLevel)
        {
            Assert.Equal(expectedLevel, MythicRiftProgression.ResolveNextUnlockedLevel(currentLevel, completedLevel));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(30, 30)]
        [InlineData(31, 1)]
        [InlineData(60, 30)]
        [InlineData(61, 1)]
        public void NormalizeEndlessCycleLevel_WrapsAfterThirty(int level, int expectedLevel)
        {
            Assert.Equal(expectedLevel, MythicRiftProgression.NormalizeEndlessCycleLevel(level));
        }

        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(29, 29, 30)]
        [InlineData(30, 30, 1)]
        [InlineData(30, 60, 1)]
        [InlineData(31, 31, 2)]
        public void ResolveNextEndlessCycleLevel_WrapsCompletionAfterThirty(int currentLevel, int completedLevel, int expectedLevel)
        {
            Assert.Equal(expectedLevel, MythicRiftProgression.ResolveNextEndlessCycleLevel(currentLevel, completedLevel));
        }
    }
}
