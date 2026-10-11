using MHServerEmu.Games.MythicRifts;

namespace MHServerEmu.Games.Tests.MythicRifts
{
    public class MythicRiftScalingTests
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(5, 5)]
        public void GetEffectivePlayerCount_UsesExpectedBuckets(int requestedPlayers, int expectedEffectivePlayers)
        {
            Assert.Equal(expectedEffectivePlayers, MythicRiftScaling.GetEffectivePlayerCount(requestedPlayers));
        }

        [Theory]
        [InlineData(1, 1.0f)]
        [InlineData(2, 1.5f)]
        [InlineData(3, 2.0f)]
        [InlineData(4, 2.5f)]
        [InlineData(5, 3.0f)]
        public void GetGroupHealthMultiplier_CapsFullPartyAtThreeTimesHealth(int requestedPlayers, float expectedGroupHealthMultiplier)
        {
            float actualMultiplier = MythicRiftScaling.GetGroupHealthMultiplier(requestedPlayers);
            Assert.InRange(actualMultiplier, expectedGroupHealthMultiplier - 0.001f, expectedGroupHealthMultiplier + 0.001f);
        }

        [Fact]
        public void BuildSnapshot_LevelTwentyFiveSolo_UsesConfiguredStandardScaling()
        {
            MythicRiftDifficultySnapshot snapshot = MythicRiftScaling.BuildSnapshot(25, 1);

            Assert.Equal(25, snapshot.RiftLevel);
            Assert.Equal(1, snapshot.EffectivePlayerCount);
            Assert.Equal(25f, snapshot.EquivalentD3RiftLevel);
            Assert.InRange(snapshot.GroupHealthMultiplier, 0.999f, 1.001f);
            Assert.Equal(1f, snapshot.HealthMultiplier);
            Assert.Equal(1f, snapshot.DamageMultiplier);
        }

        [Fact]
        public void BuildSnapshot_LevelTwentyFiveStandard_DoesNotApplyPartyHealthScaling()
        {
            MythicRiftDifficultySnapshot soloSnapshot = MythicRiftScaling.BuildSnapshot(25, 1);
            MythicRiftDifficultySnapshot groupSnapshot = MythicRiftScaling.BuildSnapshot(25, 4);

            Assert.Equal(4, groupSnapshot.EffectivePlayerCount);
            Assert.Equal(25f, groupSnapshot.EquivalentD3RiftLevel);
            Assert.Equal(1f, groupSnapshot.GroupHealthMultiplier);
            Assert.Equal(soloSnapshot.HealthMultiplier, groupSnapshot.HealthMultiplier);
            Assert.InRange(groupSnapshot.DamageMultiplier, soloSnapshot.DamageMultiplier - 0.001f, soloSnapshot.DamageMultiplier + 0.001f);
        }

        [Fact]
        public void BuildSnapshot_StandardPostCapScalingRemainsMonotonic()
        {
            MythicRiftDifficultySnapshot levelTwentyFive = MythicRiftScaling.BuildSnapshot(25, 1);
            MythicRiftDifficultySnapshot levelFifty = MythicRiftScaling.BuildSnapshot(50, 1);
            MythicRiftDifficultySnapshot levelOneHundredFifty = MythicRiftScaling.BuildSnapshot(150, 1);

            Assert.True(levelFifty.EquivalentD3RiftLevel > levelTwentyFive.EquivalentD3RiftLevel);
            Assert.True(levelFifty.HealthMultiplier > levelTwentyFive.HealthMultiplier);
            Assert.True(levelFifty.DamageMultiplier > levelTwentyFive.DamageMultiplier);
            Assert.True(levelOneHundredFifty.HealthMultiplier > levelFifty.HealthMultiplier);
            Assert.True(levelOneHundredFifty.DamageMultiplier > levelFifty.DamageMultiplier);
        }

        [Fact]
        public void BuildSnapshot_StandardDamageCapsAtHighLevels()
        {
            MythicRiftDifficultySnapshot levelTwoHundred = MythicRiftScaling.BuildSnapshot(200, 1);

            Assert.InRange(levelTwoHundred.DamageMultiplier, 1.999f, 2.001f);
        }

        [Fact]
        public void BuildSnapshot_StandardEffectiveHealthCapsAtHighLevels()
        {
            MythicRiftDifficultySnapshot levelTwoHundredFullParty = MythicRiftScaling.BuildSnapshot(200, 5);

            Assert.Equal(5, levelTwoHundredFullParty.EffectivePlayerCount);
            Assert.InRange(levelTwoHundredFullParty.GroupHealthMultiplier, 0.999f, 1.001f);
            Assert.InRange(levelTwoHundredFullParty.HealthMultiplier, 4.999f, 5.001f);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(9, 1)]
        [InlineData(10, 2)]
        [InlineData(20, 3)]
        [InlineData(25, 4)]
        [InlineData(28, 5)]
        [InlineData(30, 6)]
        public void EndlessBossCount_MatchesConfiguredBreakpoints(int level, int expectedBossCount)
        {
            int bossCount = MythicRiftScalingTuning.Load().GetBossCount(MythicRiftMode.Endless, level);
            Assert.Equal(expectedBossCount, bossCount);
        }

        [Fact]
        public void BuildSnapshot_EndlessUsesConfiguredScalingWithPartyHealthStacking()
        {
            MythicRiftDifficultySnapshot solo = MythicRiftScaling.BuildSnapshot(20, 1, useThirtyWaveMode: true);
            MythicRiftDifficultySnapshot party = MythicRiftScaling.BuildSnapshot(20, 4, useThirtyWaveMode: true);

            Assert.Equal(20f, solo.EquivalentD3RiftLevel);
            Assert.Equal(1f, solo.HealthMultiplier);
            Assert.Equal(2.5f, party.HealthMultiplier);
            Assert.Equal(2.5f, party.GroupHealthMultiplier);
            Assert.Equal(4, party.EffectivePlayerCount);
        }

        [Fact]
        public void BuildSnapshot_EndlessUsesConfiguredDamageMultiplier()
        {
            MythicRiftDifficultySnapshot waveThirty = MythicRiftScaling.BuildSnapshot(30, 1, useThirtyWaveMode: true);

            Assert.Equal(1f, waveThirty.DamageMultiplier);
        }

        [Fact]
        public void RunConfig_UsesThirtyWaveCycleOnlyForEndlessMode()
        {
            MythicRiftRunConfig standard = new() { Mode = MythicRiftMode.Standard };
            MythicRiftRunConfig endless = new() { Mode = MythicRiftMode.Endless };

            Assert.False(standard.UseThirtyWaveMode);
            Assert.True(endless.UseThirtyWaveMode);
        }
    }
}
