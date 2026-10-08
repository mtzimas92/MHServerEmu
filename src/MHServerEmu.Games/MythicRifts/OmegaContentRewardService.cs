using System.Collections.Concurrent;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;
using MHServerEmu.Games.Missions;
using MHServerEmu.Games.OmegaTierItems;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.MythicRifts
{
    public static class OmegaContentRewardService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private const ulong ChampionCommendationItemPrototypeId = 2852929430040615658;
        private const int WeeklyChampionCommendationCap = 500;
        private const string WeeklyChampionCommendationClaimId = "mythic-rift:account:champion-commendations-total";
        private static readonly object LoadLock = new();
        private static readonly ConcurrentDictionary<(ulong PlayerDbId, PrototypeId MissionRef), long> RecentDeathGrants = new();
        private const long RecentDeathGrantWindowMs = 10 * 60 * 1000;
        private static OmegaContentRewardTuning _tuning;

        public static void TryHandleMissionCompletion(in PlayerCompletedMissionGameEvent evt)
        {
            Player player = evt.Player;
            if (player?.CurrentAvatar == null || (evt.Participant == false && evt.Contributor == false))
                return;

            OmegaContentRewardTuning tuning = GetTuning();
            if (tuning.Enabled == false)
                return;

            Logger.Info($"[OmegaContentRewardTrace] result=mission-observed playerDbId=0x{player.DatabaseUniqueId:X} mission={evt.MissionRef.GetNameFormatted()} participant={evt.Participant} contributor={evt.Contributor} region={player.CurrentAvatar.Region?.PrototypeDataRef.GetNameFormatted()} difficulty={player.CurrentAvatar.Region?.DifficultyTierRef.GetNameFormatted()}");

            foreach (OmegaContentActivityTuning activity in tuning.Activities)
            {
                if (activity?.Enabled != true)
                    continue;

                PrototypeId missionRef = GameDatabase.GetPrototypeRefByName(activity.CompletionMission);
                if (missionRef == PrototypeId.Invalid)
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=invalid-completion-mission activity={activity.Id} configuredMission={activity.CompletionMission}");
                    continue;
                }
                if (missionRef != evt.MissionRef)
                    continue;

                PrototypeId difficultyRef = GameDatabase.GetPrototypeRefByName(activity.RequiredDifficulty);
                if (difficultyRef != PrototypeId.Invalid && player.CurrentAvatar.Region?.DifficultyTierRef != difficultyRef)
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=difficulty-mismatch playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} mission={evt.MissionRef.GetNameFormatted()} required={difficultyRef.GetNameFormatted()} actual={player.CurrentAvatar.Region?.DifficultyTierRef.GetNameFormatted()}");
                    return;
                }

                Logger.Info($"[OmegaContentRewardTrace] result=activity-matched playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} mission={evt.MissionRef.GetNameFormatted()} rewardCount={activity.Rewards.Count}");

                if (WasRecentlyGrantedFromEntityDeath(player.DatabaseUniqueId, evt.MissionRef))
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=duplicate-completion-suppressed playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} mission={evt.MissionRef.GetNameFormatted()}");
                    return;
                }

                GrantActivityRewards(player, activity);
                return;
            }
        }

        public static void TryHandleMissionEntityDeath(Mission mission, WorldEntity entity, Player killer, long nextCount, long requiredCount)
        {
            if (mission == null || entity == null || nextCount < requiredCount)
                return;

            OmegaContentRewardTuning tuning = GetTuning();
            if (tuning.Enabled == false)
                return;

            foreach (OmegaContentActivityTuning activity in tuning.Activities)
            {
                if (activity?.Enabled != true)
                    continue;

                PrototypeId missionRef = GameDatabase.GetPrototypeRefByName(activity.CompletionMission);
                if (missionRef != mission.PrototypeDataRef)
                    continue;

                Logger.Info($"[OmegaContentRewardTrace] result=mission-entity-death activity={activity.Id} mission={mission.PrototypeDataRef.GetNameFormatted()} entity={entity.PrototypeDataRef.GetNameFormatted()} entityId=0x{entity.Id:X} entityPos={entity.RegionLocation.Position} killerDbId=0x{killer?.DatabaseUniqueId ?? 0:X} conditionCount={nextCount}/{requiredCount}");

                using var recipientsHandle = HashSetPool<Player>.Get(out HashSet<Player> recipients);
                using var playersHandle = ListPool<Player>.Get(out List<Player> players);

                if (mission.GetParticipants(players))
                    recipients.UnionWith(players);
                if (mission.GetContributors(players))
                    recipients.UnionWith(players);
                if (killer != null)
                    recipients.Add(killer);

                int grantedRecipients = 0;
                foreach (Player player in recipients)
                {
                    if (player?.CurrentAvatar == null || player.CurrentAvatar.Region != entity.Region)
                        continue;

                    PrototypeId difficultyRef = GameDatabase.GetPrototypeRefByName(activity.RequiredDifficulty);
                    if (difficultyRef != PrototypeId.Invalid && player.CurrentAvatar.Region.DifficultyTierRef != difficultyRef)
                    {
                        Logger.Info($"[OmegaContentRewardTrace] result=difficulty-mismatch playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} mission={mission.PrototypeDataRef.GetNameFormatted()} required={difficultyRef.GetNameFormatted()} actual={player.CurrentAvatar.Region.DifficultyTierRef.GetNameFormatted()}");
                        continue;
                    }

                    RecordDeathGrant(player.DatabaseUniqueId, mission.PrototypeDataRef);
                    GrantActivityRewards(player, activity, entity);
                    grantedRecipients++;
                }

                Logger.Info($"[OmegaContentRewardTrace] result=death-rewards-processed activity={activity.Id} mission={mission.PrototypeDataRef.GetNameFormatted()} recipients={grantedRecipients}");
                return;
            }
        }

        private static void RecordDeathGrant(ulong playerDbId, PrototypeId missionRef)
        {
            long now = Environment.TickCount64;
            RecentDeathGrants[(playerDbId, missionRef)] = now;

            foreach (var entry in RecentDeathGrants)
                if (now - entry.Value > RecentDeathGrantWindowMs)
                    RecentDeathGrants.TryRemove(entry.Key, out _);
        }

        private static bool WasRecentlyGrantedFromEntityDeath(ulong playerDbId, PrototypeId missionRef)
        {
            if (RecentDeathGrants.TryGetValue((playerDbId, missionRef), out long grantedAt) == false)
                return false;

            return Environment.TickCount64 - grantedAt <= RecentDeathGrantWindowMs;
        }

        private static void GrantActivityRewards(Player player, OmegaContentActivityTuning activity, WorldEntity rewardSource = null)
        {
            foreach (OmegaContentRewardEntryTuning reward in activity.Rewards)
            {
                if (reward?.Enabled != true || reward.Quantity <= 0)
                    continue;
                float chanceRoll = player.Game.Random.NextFloat() * 100f;
                if (chanceRoll >= reward.ChancePercent)
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=chance-failed playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} roll={chanceRoll:F4} chance={reward.ChancePercent:F4}");
                    continue;
                }

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
                if (itemRef == PrototypeId.Invalid)
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=invalid-item playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} runtimeId={reward.ItemPrototypeRuntimeId} prototype={reward.ItemPrototype}");
                    continue;
                }

                long championPeriodMarker = 0;
                if (itemRef == (PrototypeId)ChampionCommendationItemPrototypeId)
                {
                    championPeriodMarker = GetPeriodMarker("weekly");
                    int weeklyClaimed = player.OmegaContentRewardProgress.GetClaimedAmount(WeeklyChampionCommendationClaimId, championPeriodMarker);
                    quantity = Math.Min(quantity, Math.Max(WeeklyChampionCommendationCap - weeklyClaimed, 0));
                    if (quantity <= 0)
                        continue;
                }

                if (GiveStackedItem(player, itemRef, quantity, reward.ItemLevel, rewardSource) == false)
                {
                    Logger.Info($"[OmegaContentRewardTrace] result=delivery-failed playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} item={itemRef.GetNameFormatted()} quantity={quantity} itemLevel={reward.ItemLevel}");
                    continue;
                }

                if (reward.PeriodLimit > 0)
                    player.OmegaContentRewardProgress.AddClaimedAmount(reward.Id, periodMarker, quantity);

                if (championPeriodMarker != 0)
                    player.OmegaContentRewardProgress.AddClaimedAmount(WeeklyChampionCommendationClaimId, championPeriodMarker, quantity);

                Logger.Info($"[OmegaContentRewardTrace] result=granted playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} item={itemRef.GetNameFormatted()} quantity={quantity} period={reward.Period} periodMarker={periodMarker} limit={reward.PeriodLimit}");
            }
        }

        private static bool GiveStackedItem(Player player, PrototypeId itemRef, int quantity, int itemLevel, WorldEntity rewardSource)
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

            if (rewardSource?.IsInWorld == true && player.CurrentAvatar?.Region == rewardSource.Region)
            {
                using var inputSettingsHandle = LootInputSettingsPool.Get(out LootInputSettings inputSettings);
                inputSettings.Initialize(LootContext.Drop, player, rewardSource, Math.Max(itemLevel, 1));
                return player.Game.LootManager.SpawnLootFromSummary(summary, inputSettings);
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
