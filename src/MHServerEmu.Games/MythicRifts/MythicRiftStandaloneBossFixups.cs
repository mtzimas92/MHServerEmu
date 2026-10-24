using System.Runtime.CompilerServices;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftStandaloneBossFixups
    {
        private sealed class StandaloneBossState
        {
            public bool AllowMissingAffixSettingsFallback { get; set; }
        }

        private static readonly ConditionalWeakTable<Agent, StandaloneBossState> StandaloneBosses = new();
        private const float StandaloneBossAggroRange = 3000f;

        public static bool IsStandaloneBoss(Agent agent)
        {
            return agent != null && StandaloneBosses.TryGetValue(agent, out _);
        }

        public static bool AllowsMissingAffixSettingsFallback(Agent agent)
        {
            return agent != null
                && StandaloneBosses.TryGetValue(agent, out StandaloneBossState state)
                && state.AllowMissingAffixSettingsFallback;
        }

        public static void Apply(Agent agent, bool allowMissingAffixSettingsFallback = false)
        {
            if (agent == null)
                return;

            StandaloneBossState state = StandaloneBosses.GetValue(agent, static _ => new());
            if (allowMissingAffixSettingsFallback)
                state.AllowMissingAffixSettingsFallback = true;

            if (agent.AIController == null)
                return;

            PropertyCollection blackboard = agent.AIController.Blackboard.PropertyCollection;
            blackboard[PropertyEnum.AIAggroRangeOverrideHostile] = StandaloneBossAggroRange;
            blackboard[PropertyEnum.AIAggroRangeOverrideAlly] = StandaloneBossAggroRange;

            AgentPrototype bossProto = agent.WorldEntityPrototype as AgentPrototype;
            bool isModokProfile = bossProto?.BehaviorProfile?.Brain.As<Prototype>() is ProceduralProfileMODOKPrototype;
            if (isModokProfile == false)
                return;

            blackboard[PropertyEnum.AICustomStateVal1] = 2; // ProceduralProfileMODOKPrototype.State.GenericProcedural
            blackboard[PropertyEnum.AICustomTimeVal1] = (long)agent.Game.CurrentTime.TotalMilliseconds + 3_600_000_000L;
        }

        public static void Clear(Agent agent)
        {
            if (agent != null)
                StandaloneBosses.Remove(agent);
        }
    }
}
