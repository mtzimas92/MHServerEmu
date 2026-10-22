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
        private const ulong ProtectorCommendationItemPrototypeId = 810646464305634596;
        private const int WeeklyChampionCommendationCap = 350;
        private const string WeeklyChampionCommendationClaimId = "mythic-rift:account:champion-commendations-total";
        private static readonly ConcurrentDictionary<(ulong PlayerDbId, PrototypeId MissionRef), long> RecentDeathGrants = new();
        private const long RecentDeathGrantWindowMs = 10 * 60 * 1000;
        private static readonly OmegaContentRewardTuning Tuning = OmegaContentRewardTuning.Load();

        public static void TryHandleMissionCompletion(in PlayerCompletedMissionGameEvent evt)
        {
            Player player = evt.Player;
            if (player?.CurrentAvatar == null || (evt.Participant == false && evt.Contributor == false))
                return;

            OmegaContentRewardTuning tuning = GetTuning();
            if (tuning.Enabled == false)
                return;

            foreach (OmegaContentActivityTuning activity in tuning.GetActivities(evt.MissionRef))
            {
                Logger.Info($"[OmegaRaidRewardTrace] stage=mission-complete activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} participant={evt.Participant} contributor={evt.Contributor} region={player.CurrentAvatar.Region?.PrototypeDataRef.GetNameFormatted() ?? "none"} difficulty={player.CurrentAvatar.Region?.DifficultyTierRef.GetNameFormatted() ?? "none"}");

                PrototypeId difficultyRef = activity.RequiredDifficultyRef;
                if (difficultyRef != PrototypeId.Invalid && player.CurrentAvatar.Region?.DifficultyTierRef != difficultyRef)
                    return;

                if (WasRecentlyGrantedFromEntityDeath(player.DatabaseUniqueId, evt.MissionRef))
                {
                    Logger.Info($"[OmegaRaidRewardTrace] stage=mission-complete-skipped activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reason=recent-entity-death-grant");
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

            foreach (OmegaContentActivityTuning activity in tuning.GetActivities(mission.PrototypeDataRef))
            {
                PrototypeId completionEntityRef = activity.CompletionEntityRef;
                if (completionEntityRef != PrototypeId.Invalid &&
                    GameDatabase.DataDirectory.PrototypeIsAPrototype(entity.PrototypeDataRef, completionEntityRef) == false)
                {
                    Logger.Info($"[OmegaRaidRewardTrace] stage=entity-trigger-skipped activity={activity.Id} mission={mission.PrototypeDataRef.GetNameFormatted()} entity={entity.PrototypeDataRef.GetNameFormatted()} requiredEntity={completionEntityRef.GetNameFormatted()} reason=not-completion-entity");
                    continue;
                }

                using var recipientsHandle = HashSetPool<Player>.Get(out HashSet<Player> recipients);
                using var playersHandle = ListPool<Player>.Get(out List<Player> players);

                if (mission.GetParticipants(players))
                    recipients.UnionWith(players);
                if (mission.GetContributors(players))
                    recipients.UnionWith(players);
                if (killer != null)
                    recipients.Add(killer);

                Player groupAnchor = killer;
                if (groupAnchor == null)
                    foreach (Player recipient in recipients)
                    {
                        groupAnchor = recipient;
                        break;
                    }

                var group = groupAnchor?.GetParty();
                using var groupMemberIdsHandle = ListPool<ulong>.Get(out List<ulong> groupMemberIds);
                using var unresolvedMemberIdsHandle = ListPool<ulong>.Get(out List<ulong> unresolvedMemberIds);
                if (group != null)
                {
                    foreach (var member in group)
                    {
                        groupMemberIds.Add(member.Key);
                        Player raidMember = mission.Game.EntityManager.GetEntityByDbGuid<Player>(member.Key);
                        if (raidMember != null)
                            recipients.Add(raidMember);
                        else
                            unresolvedMemberIds.Add(member.Key);
                    }

                    // Raid portals can divide members between regions while they remain in the
                    // same game and group. Resolve from the live game roster as a fallback in
                    // case this region's party snapshot is incomplete.
                    foreach (Player gamePlayer in new PlayerIterator(mission.Game))
                    {
                        if (gamePlayer?.GetParty()?.PartyId == group.PartyId)
                            recipients.Add(gamePlayer);
                    }
                }

                Logger.Info($"[OmegaRaidRewardTrace] stage=recipients activity={activity.Id} mission={mission.PrototypeDataRef.GetNameFormatted()} gameId=0x{mission.Game.Id:X} entity={entity.PrototypeDataRef.GetNameFormatted()} entityRegion={entity.Region?.PrototypeDataRef.GetNameFormatted() ?? "none"} killerDbId=0x{killer?.DatabaseUniqueId ?? 0:X} groupId=0x{group?.PartyId ?? 0:X} groupType={group?.Type.ToString() ?? "none"} groupMembers=[{string.Join(',', groupMemberIds.Select(id => $"0x{id:X}"))}] unresolvedMembers=[{string.Join(',', unresolvedMemberIds.Select(id => $"0x{id:X}"))}] recipients=[{string.Join(',', recipients.Select(player => $"0x{player.DatabaseUniqueId:X}:{player.CurrentAvatar?.Region?.PrototypeDataRef.GetNameFormatted() ?? "none"}"))}]");

                foreach (Player player in recipients)
                {
                    var avatar = player?.CurrentAvatar;
                    if (avatar?.Region == null)
                    {
                        Logger.Info($"[OmegaRaidRewardTrace] stage=skipped activity={activity.Id} playerDbId=0x{player?.DatabaseUniqueId ?? 0:X} reason=no-avatar-region");
                        continue;
                    }

                    PrototypeId difficultyRef = activity.RequiredDifficultyRef;
                    if (difficultyRef != PrototypeId.Invalid && avatar.Region.DifficultyTierRef != difficultyRef)
                    {
                        Logger.Info($"[OmegaRaidRewardTrace] stage=skipped activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reason=difficulty actual={avatar.Region.DifficultyTierRef.GetNameFormatted()} required={difficultyRef.GetNameFormatted()} region={avatar.Region.PrototypeDataRef.GetNameFormatted()}");
                        continue;
                    }

                    RecordDeathGrant(player.DatabaseUniqueId, mission.PrototypeDataRef);
                    WorldEntity rewardSource = avatar.Region == entity.Region ? entity : avatar;
                    GrantActivityRewards(player, activity, rewardSource);
                }

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
            (ulong PlayerDbId, PrototypeId MissionRef) key = (playerDbId, missionRef);
            if (RecentDeathGrants.TryGetValue(key, out long grantedAt) == false)
                return false;

            if (Environment.TickCount64 - grantedAt <= RecentDeathGrantWindowMs)
                return true;

            RecentDeathGrants.TryRemove(key, out _);
            return false;
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
                    Logger.Info($"[OmegaRaidRewardTrace] stage=reward-roll activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reward={reward.Id} roll={chanceRoll:F2} chance={reward.ChancePercent:F2} result=miss");
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
                        Logger.Info($"[OmegaRaidRewardTrace] stage=reward-skipped activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reward={reward.Id} reason=period-limit claimed={claimed} limit={reward.PeriodLimit} periodMarker={periodMarker}");
                        continue;
                    }
                }

                PrototypeId itemRef = reward.ItemPrototypeRef;
                if (itemRef == PrototypeId.Invalid)
                {
                    Logger.Warn($"Invalid Omega content reward item: activity={activity.Id} reward={reward.Id} runtimeId={reward.ItemPrototypeRuntimeId} prototype={reward.ItemPrototype}");
                    continue;
                }

                long championPeriodMarker = 0;
                if (itemRef == (PrototypeId)ChampionCommendationItemPrototypeId)
                {
                    championPeriodMarker = GetPeriodMarker("weekly");
                    int weeklyClaimed = player.OmegaContentRewardProgress.GetClaimedAmount(WeeklyChampionCommendationClaimId, championPeriodMarker);
                    quantity = Math.Min(quantity, Math.Max(WeeklyChampionCommendationCap - weeklyClaimed, 0));
                    if (quantity <= 0)
                    {
                        Logger.Info($"[OmegaRaidRewardTrace] stage=reward-skipped activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reward={reward.Id} reason=champion-cap claimed={weeklyClaimed} limit={WeeklyChampionCommendationCap} periodMarker={championPeriodMarker}");
                        continue;
                    }
                }

                WorldEntity deliverySource = rewardSource;
                if (string.Equals(activity.Id, "cosmic-axis", StringComparison.OrdinalIgnoreCase) &&
                    (itemRef == (PrototypeId)ChampionCommendationItemPrototypeId ||
                     itemRef == (PrototypeId)ProtectorCommendationItemPrototypeId))
                {
                    deliverySource = player.CurrentAvatar;
                }

                if (GiveStackedItem(player, itemRef, reward.ItemRarityPrototypeRef, quantity, reward.ItemLevel, deliverySource) == false)
                {
                    Logger.Warn($"Failed to deliver Omega content reward: playerDbId=0x{player.DatabaseUniqueId:X} activity={activity.Id} reward={reward.Id} item={itemRef.GetNameFormatted()} quantity={quantity} itemLevel={reward.ItemLevel}");
                    continue;
                }

                Logger.Info($"[OmegaRaidRewardTrace] stage=reward-delivered activity={activity.Id} playerDbId=0x{player.DatabaseUniqueId:X} reward={reward.Id} item={itemRef.GetNameFormatted()} quantity={quantity} itemLevel={reward.ItemLevel} roll={chanceRoll:F2} chance={reward.ChancePercent:F2} source={(deliverySource == player.CurrentAvatar ? "player" : "encounter")}");

                if (reward.PeriodLimit > 0)
                    player.OmegaContentRewardProgress.AddClaimedAmount(reward.Id, periodMarker, quantity);

                if (championPeriodMarker != 0)
                    player.OmegaContentRewardProgress.AddClaimedAmount(WeeklyChampionCommendationClaimId, championPeriodMarker, quantity);

            }
        }

        private static bool GiveStackedItem(Player player, PrototypeId itemRef, PrototypeId rarityRef, int quantity, int itemLevel, WorldEntity rewardSource)
        {
            ItemPrototype itemProto = GameDatabase.GetPrototype<ItemPrototype>(itemRef);
            if (itemProto == null)
                return false;

            int maxStack = Math.Max(itemProto.StackSettings?.MaxStacks ?? 1, 1);
            using var summaryHandle = LootResultSummaryPool.Get(out LootResultSummary summary);
            int remaining = quantity;
            while (remaining > 0)
            {
                ItemSpec itemSpec = OmegaTierItemFactory.ShouldUseFactory(itemRef, rarityRef)
                    ? OmegaTierItemFactory.CreateItemSpec(player.Game, itemRef, rarityRef, LootContext.Drop, player, itemLevel)
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
            return Tuning;
        }
    }
}
