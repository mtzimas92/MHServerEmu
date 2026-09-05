using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftStandaloneBossFixups
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly HashSet<ulong> StandaloneBossIds = new();
        private static readonly HashSet<ulong> StandaloneBossAffixFallbackIds = new();
        private const float StandaloneBossAggroRange = 3000f;

        public static bool IsStandaloneBoss(Agent agent)
        {
            return agent != null && StandaloneBossIds.Contains(agent.Id);
        }

        public static bool AllowsMissingAffixSettingsFallback(Agent agent)
        {
            return agent != null && StandaloneBossAffixFallbackIds.Contains(agent.Id);
        }

        public static void Apply(Agent agent, bool allowMissingAffixSettingsFallback = false)
        {
            if (agent == null)
                return;

            StandaloneBossIds.Add(agent.Id);
            if (allowMissingAffixSettingsFallback)
                StandaloneBossAffixFallbackIds.Add(agent.Id);

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
            // Logger.Debug($"ApplyStandaloneBossFixups(): forced standalone MODOK {agent} into GenericProcedural with long aggro range.");
        }

        public static void Clear(ulong entityId)
        {
            if (entityId == 0)
                return;

            StandaloneBossIds.Remove(entityId);
            StandaloneBossAffixFallbackIds.Remove(entityId);
        }
    }
}
