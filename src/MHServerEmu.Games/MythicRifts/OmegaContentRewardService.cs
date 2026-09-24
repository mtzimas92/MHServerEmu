using MHServerEmu.Core.Memory;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;
using MHServerEmu.Games.OmegaTierItems;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.MythicRifts
{
    public static class OmegaContentRewardService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly object LoadLock = new();
        private static OmegaContentRewardTuning _tuning;

        public static void TryHandleMissionCompletion(in PlayerCompletedMissionGameEvent evt)
        {
            Player player = evt.Player;
            if (player?.CurrentAvatar == null || (evt.Participant == false && evt.Contributor == false))
                return;

            OmegaContentRewardTuning tuning = GetTuning();
            if (tuning.Enabled == false)
                return;

            foreach (OmegaContentActivityTuning activity in tuning.Activities)
            {
                if (activity?.Enabled != true)
                    continue;

                PrototypeId missionRef = GameDatabase.GetPrototypeRefByName(activity.CompletionMission);
                if (missionRef == PrototypeId.Invalid || missionRef != evt.MissionRef)
                    continue;

                PrototypeId difficultyRef = GameDatabase.GetPrototypeRefByName(activity.RequiredDifficulty);
                if (difficultyRef != PrototypeId.Invalid && player.CurrentAvatar.Region?.DifficultyTierRef != difficultyRef)
                    return;

                GrantActivityRewards(player, activity);
                return;
            }
        }

        private static void GrantActivityRewards(Player player, OmegaContentActivityTuning activity)
        {
            foreach (OmegaContentRewardEntryTuning reward in activity.Rewards)
            {
                if (reward?.Enabled != true || reward.Quantity <= 0)
                    continue;
                if (player.Game.Random.NextFloat() * 100f >= reward.ChancePercent)
                    continue;

                long periodMarker = GetPeriodMarker(reward.Period);
                int quantity = reward.Quantity;
                if (reward.PeriodLimit > 0)
                {
                    int claimed = player.OmegaContentRewardProgress.GetClaimedAmount(reward.Id, periodMarker);
                    quantity = Math.Min(quantity, Math.Max(reward.PeriodLimit - claimed, 0));
                    if (quantity <= 0)
                    {
                        Logger.Info($"[OmegaContentRewardTrace] result=blocked playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} period={reward.Period} periodMarker={periodMarker} claimed={claimed} limit={reward.PeriodLimit}");
                        continue;
                    }
                }

                PrototypeId itemRef = reward.ItemPrototypeRuntimeId != 0
                    ? (PrototypeId)reward.ItemPrototypeRuntimeId
                    : GameDatabase.GetPrototypeRefByName(reward.ItemPrototype);
                if (itemRef == PrototypeId.Invalid || GiveStackedItem(player, itemRef, quantity, reward.ItemLevel) == false)
                    continue;

                if (reward.PeriodLimit > 0)
                    player.OmegaContentRewardProgress.AddClaimedAmount(reward.Id, periodMarker, quantity);

                Logger.Info($"[OmegaContentRewardTrace] result=granted playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} item={itemRef.GetNameFormatted()} quantity={quantity} period={reward.Period} periodMarker={periodMarker} limit={reward.PeriodLimit}");
            }
        }

        private static bool GiveStackedItem(Player player, PrototypeId itemRef, int quantity, int itemLevel)
        {
            ItemPrototype itemProto = GameDatabase.GetPrototype<ItemPrototype>(itemRef);
            if (itemProto == null)
                return false;

            int maxStack = Math.Max(itemProto.StackSettings?.MaxStacks ?? 1, 1);
            using var summaryHandle = LootResultSummaryPool.Get(out LootResultSummary summary);
            int remaining = quantity;
            while (remaining > 0)
            {
                ItemSpec itemSpec = OmegaTierItemFactory.ShouldUseFactory(itemRef, PrototypeId.Invalid)
                    ? OmegaTierItemFactory.CreateItemSpec(player.Game, itemRef, PrototypeId.Invalid, LootContext.Drop, player, itemLevel)
                    : player.Game.LootManager.CreateItemSpec(itemRef, LootContext.Drop, player, itemLevel);
                if (itemSpec == null)
                    return false;

                itemSpec.StackCount = Math.Min(remaining, maxStack);
                remaining -= itemSpec.StackCount;
                summary.Add(new LootResult(itemSpec));
            }

            return player.Game.LootManager.GiveLootFromSummary(summary, player, PrototypeId.Invalid);
        }

        private static long GetPeriodMarker(string period)
        {
            DateTime utcNow = DateTime.UtcNow;
            if (string.Equals(period, "weekly", StringComparison.OrdinalIgnoreCase))
            {
                const DayOfWeek rolloverDay = DayOfWeek.Wednesday;
                TimeSpan rolloverTime = TimeSpan.FromHours(10);
                int daysSinceRolloverDay = ((int)utcNow.DayOfWeek - (int)rolloverDay + 7) % 7;
                DateTime periodStart = utcNow.Date.AddDays(-daysSinceRolloverDay).Add(rolloverTime);
                if (periodStart > utcNow)
                    periodStart = periodStart.AddDays(-7);

                utcNow = periodStart;
            }
            else
            {
                DateTime periodStart = utcNow.Date.AddHours(10);
                if (periodStart > utcNow)
                    periodStart = periodStart.AddDays(-1);

                utcNow = periodStart;
            }

            return new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        private static OmegaContentRewardTuning GetTuning()
        {
            if (_tuning != null)
                return _tuning;

            lock (LoadLock)
                return _tuning ??= OmegaContentRewardTuning.Load();
        }
    }
}
