using Gazillion;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.System.Random;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI;

namespace MHServerEmu.Games.VillainIntelBoard
{
    public static class VillainIntelBoardManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private static readonly LocaleStringId MainDialogTextRef = (LocaleStringId)18000000000000080300;
        private static readonly LocaleStringId ReviewDialogTextRef = (LocaleStringId)18000000000000080301;
        private static readonly LocaleStringId ViewBoardButtonRef = (LocaleStringId)18000000000000080310;
        private static readonly LocaleStringId RerollBoardButtonRef = (LocaleStringId)18000000000000080311;
        private static readonly LocaleStringId NextBountyButtonRef = (LocaleStringId)18000000000000080312;
        private static readonly LocaleStringId CollectRewardButtonRef = (LocaleStringId)18000000000000080313;
        private static readonly LocaleStringId BoardRerolledBannerRef = (LocaleStringId)18000000000000080320;
        private static readonly LocaleStringId HuntStartedBannerRef = (LocaleStringId)18000000000000080321;
        private static readonly LocaleStringId HuntSpawnedBannerRef = (LocaleStringId)18000000000000080322;
        private static readonly LocaleStringId HuntDefeatedBannerRef = (LocaleStringId)18000000000000080323;
        private static readonly LocaleStringId RewardCollectedBannerRef = (LocaleStringId)18000000000000080324;
        private static readonly LocaleStringId NotEnoughCreditsBannerRef = (LocaleStringId)18000000000000080325;
        private static readonly LocaleStringId InvalidBoardBannerRef = (LocaleStringId)18000000000000080326;
        private static readonly LocaleStringId GenericBountyTargetButtonRef = (LocaleStringId)18000000000000080330;

        private static readonly PrototypeId AvengersTowerHubRegionRef = (PrototypeId)9142075282174842340;
        private static readonly PrototypeId AvengersTowerLandingMarkerRef = (PrototypeId)6460789910415087113;
        private const float BoardNpcSideOffset = 360f;
        private const float BoardNpcY = 570f;

        private static VillainIntelBoardTuning _tuning = VillainIntelBoardTuning.CreateDefault();
        private static string _lastLoadMessage = "Villain Intel Board has not loaded tuning yet.";
        private static readonly Dictionary<ulong, VillainIntelBoardState> BoardsByPlayerDbId = new();
        private static readonly Dictionary<ulong, VillainIntelActiveHunt> ActiveHuntsByPlayerDbId = new();
        private static readonly Dictionary<ulong, VillainIntelPendingHunt> PendingHuntsByPlayerDbId = new();

        public static string LastLoadMessage => _lastLoadMessage;
        public static VillainIntelBoardTuning Tuning => _tuning;

        public static bool TryReloadTuning(out string message)
        {
            string configPath = VillainIntelBoardTuning.ConfigPath;
            VillainIntelBoardTuning previous = _tuning ?? VillainIntelBoardTuning.CreateDefault();

            if (File.Exists(configPath) == false)
            {
                _tuning = VillainIntelBoardTuning.CreateDefault();
                message = $"Villain Intel Board tuning file not found at {FileHelper.GetRelativePath(configPath)}; using built-in fallback.";
                _lastLoadMessage = message;
                return true;
            }

            VillainIntelBoardTuning loaded = FileHelper.DeserializeJson<VillainIntelBoardTuning>(configPath, VillainIntelBoardTuning.JsonOptions);
            if (loaded == null)
            {
                _tuning = previous;
                message = $"Failed to load Villain Intel Board tuning from {FileHelper.GetRelativePath(configPath)}; keeping '{previous.ProfileName}'.";
                _lastLoadMessage = message;
                return false;
            }

            ResolveTuning(loaded);
            _tuning = loaded.Enabled ? loaded : VillainIntelBoardTuning.CreateDefault();
            message = loaded.Enabled
                ? $"Loaded Villain Intel Board profile '{loaded.ProfileName}' from {FileHelper.GetRelativePath(configPath)}. themes={loaded.Themes.Count} regions={loaded.Regions.Count} rewards={loaded.Rewards.Count}"
                : $"Villain Intel Board tuning file loaded but disabled; using built-in fallback. path={FileHelper.GetRelativePath(configPath)}";
            _lastLoadMessage = message;
            return true;
        }

        public static void SpawnBoardNpc(Region region)
        {
            if (region == null || region.PrototypeDataRef != AvengersTowerHubRegionRef)
                return;

            TryReloadTuningIfNeeded();
            PrototypeId npcRef = ResolveBoardNpcRef();
            if (npcRef == PrototypeId.Invalid)
            {
                Logger.Warn("SpawnBoardNpc(): Unable to resolve any Villain Intel Board NPC prototype.");
                return;
            }

            Vector3 landingPosition = Vector3.Zero;
            Orientation landingOrientation = Orientation.Zero;
            if (region.FindTargetLocation(ref landingPosition, ref landingOrientation, PrototypeId.Invalid, PrototypeId.Invalid, AvengersTowerLandingMarkerRef) == false)
            {
                Logger.Warn("SpawnBoardNpc(): Failed to find Avengers Tower landing marker.");
                return;
            }

            float yaw = landingOrientation.Yaw;
            Vector3 npcPosition = new(landingPosition.X + (MathF.Cos(yaw) * BoardNpcSideOffset), BoardNpcY, landingPosition.Z);
            Orientation npcOrientation = Orientation.FromDeltaVector(landingPosition - npcPosition);

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = npcRef;
            settings.Position = npcPosition;
            settings.Orientation = npcOrientation;
            settings.RegionId = region.Id;

            WorldEntity npc = region.Game.EntityManager.CreateEntity(settings) as WorldEntity;
            if (npc == null)
            {
                Logger.Warn($"SpawnBoardNpc(): Failed to create Villain Intel Board NPC {npcRef.GetNameFormatted()}.");
                return;
            }

            npc.Properties[PropertyEnum.Interactable] = (int)TriBool.True;
            Logger.Info($"SpawnBoardNpc(): spawned Villain Intel Board NPC {npcRef.GetNameFormatted()} id=0x{npc.Id:X}.");
        }

        public static bool TryUseBoardNpc(Player player, WorldEntity interactableObject)
        {
            if (player == null || interactableObject == null)
                return false;

            TryReloadTuningIfNeeded();
            PrototypeId npcRef = ResolveBoardNpcRef();
            if (npcRef == PrototypeId.Invalid || interactableObject.PrototypeDataRef != npcRef)
                return false;

            ShowMainDialog(player, interactableObject);
            return true;
        }

        public static void OnAvatarEnteredWorld(Player player, Avatar avatar, Region region)
        {
            if (player == null || avatar == null || region == null)
                return;

            if (PendingHuntsByPlayerDbId.TryGetValue(player.DatabaseUniqueId, out VillainIntelPendingHunt pending) == false)
                return;

            if (pending.RegionProtoRef != region.PrototypeDataRef)
                return;

            PendingHuntsByPlayerDbId.Remove(player.DatabaseUniqueId);
            SpawnAcceptedBounty(player, avatar, region, pending);
        }

        public static VillainIntelBoardState GetBoard(Player player)
        {
            if (player == null)
                return null;

            TryReloadTuningIfNeeded();
            if (BoardsByPlayerDbId.TryGetValue(player.DatabaseUniqueId, out VillainIntelBoardState board) == false)
            {
                board = new();
                BoardsByPlayerDbId[player.DatabaseUniqueId] = board;
            }

            if (board.NeedsReroll(_tuning.GetBoardSize()))
                GenerateBoard(player, board, spendCredits: false);

            return board;
        }

        public static string ForceReroll(Player player, bool spendCredits)
        {
            if (player == null)
                return "No player.";

            VillainIntelBoardState board = GetBoard(player);
            if (board == null)
                return "Board unavailable.";

            if (spendCredits && SpendCredits(player, Math.Max(_tuning.RerollCostCredits, 0)) == false)
            {
                player.SendBannerMessage(NotEnoughCreditsBannerRef);
                return $"Not enough credits. Reroll costs {_tuning.RerollCostCredits}.";
            }

            GenerateBoard(player, board, spendCredits: false);
            player.SendBannerMessage(BoardRerolledBannerRef);
            return $"Villain Intel Board rerolled with {board.Entries.Count} bounties.";
        }

        public static string AcceptBounty(Player player, int slotIndex)
        {
            VillainIntelBoardState board = GetBoard(player);
            if (board == null || slotIndex < 0 || slotIndex >= board.Entries.Count)
            {
                player?.SendBannerMessage(InvalidBoardBannerRef);
                return "Invalid bounty slot.";
            }

            VillainIntelBoardEntry entry = board.Entries[slotIndex];
            if (entry.CanAccept == false)
                return $"Slot {slotIndex + 1} is {entry.Status}.";

            if (ActiveHuntsByPlayerDbId.ContainsKey(player.DatabaseUniqueId) || PendingHuntsByPlayerDbId.ContainsKey(player.DatabaseUniqueId))
                return "You already have an active Villain Intel bounty.";

            int cost = _tuning.GetAcceptCost(entry.Rank);
            if (SpendCredits(player, cost) == false)
            {
                player.SendBannerMessage(NotEnoughCreditsBannerRef);
                return $"Not enough credits. Bounty costs {cost}.";
            }

            entry.Status = VillainIntelBoardEntryStatus.Active;
            PendingHuntsByPlayerDbId[player.DatabaseUniqueId] = new(entry);

            Avatar avatar = player.CurrentAvatar;
            if (avatar == null)
                return "No active avatar.";

            using (Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>())
            {
                teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Debug);
                teleporter.BypassQueueRegionForRift = true;
                teleporter.DifficultyTierRef = player.GetDifficultyTierForRegion(entry.RegionProtoRef);
                if (teleporter.DifficultyTierRef == PrototypeId.Invalid)
                    teleporter.DifficultyTierRef = GameDatabase.GlobalsPrototype.DifficultyTierDefault;

                if (teleporter.TeleportToTarget(entry.RegionProtoRef) == false)
                {
                    entry.Status = VillainIntelBoardEntryStatus.Failed;
                    PendingHuntsByPlayerDbId.Remove(player.DatabaseUniqueId);
                    return $"Failed to teleport to {entry.RegionName}.";
                }
            }

            player.SendBannerMessage(HuntStartedBannerRef);
            return $"Accepted slot {slotIndex + 1}: rank {entry.Rank} {entry.TargetName} in {entry.RegionName}.";
        }

        public static string CollectReward(Player player, int slotIndex)
        {
            VillainIntelBoardState board = GetBoard(player);
            if (board == null || slotIndex < 0 || slotIndex >= board.Entries.Count)
                return "Invalid bounty slot.";

            VillainIntelBoardEntry entry = board.Entries[slotIndex];
            if (entry.CanCollect == false)
                return $"Slot {slotIndex + 1} is not ready to collect.";

            GrantRewards(player, player.CurrentAvatar, entry, player.CurrentAvatar);
            entry.Status = VillainIntelBoardEntryStatus.RewardCollected;
            player.SendBannerMessage(RewardCollectedBannerRef);
            return $"Collected Villain Intel reward for {entry.TargetName}.";
        }

        public static List<string> BuildStatusLines(Player player)
        {
            VillainIntelBoardState board = GetBoard(player);
            List<string> lines = new() { _lastLoadMessage };
            if (board == null)
                return lines;

            for (int i = 0; i < board.Entries.Count; i++)
            {
                VillainIntelBoardEntry entry = board.Entries[i];
                int cost = _tuning.GetAcceptCost(entry.Rank);
                lines.Add($"{i + 1}: rank={entry.Rank} status={entry.Status} cost={cost} target={entry.TargetName} theme={entry.ThemeName} region={entry.RegionName} heroBased={entry.HeroBased}");
            }

            return lines;
        }

        private static void ShowMainDialog(Player player, WorldEntity npc)
        {
            Game game = player.Game;
            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnMainDialogResponse;
            dialog.Message.LocaleString = MainDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, ViewBoardButtonRef, ButtonStyle.SecondaryPositive);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, RerollBoardButtonRef, ButtonStyle.SecondaryNegative);
            game.GameDialogManager.ShowDialog(dialog);

            void OnMainDialogResponse(ulong playerGuid, DialogResponse response)
            {
                Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
                if (responsePlayer == null)
                    return;

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    ShowReviewDialog(responsePlayer, npc, 0);
                    return;
                }

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                    ForceReroll(responsePlayer, spendCredits: true);
            }
        }

        private static void ShowReviewDialog(Player player, WorldEntity npc, int requestedIndex)
        {
            VillainIntelBoardState board = GetBoard(player);
            if (board == null || board.Entries.Count == 0)
                return;

            int index = ((requestedIndex % board.Entries.Count) + board.Entries.Count) % board.Entries.Count;
            board.ReviewIndex = index;
            VillainIntelBoardEntry entry = board.Entries[index];

            Game game = player.Game;
            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnReviewDialogResponse;
            dialog.Message.LocaleString = ReviewDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;

            LocaleStringId primaryButton = entry.CanCollect
                ? CollectRewardButtonRef
                : ResolveTargetButtonText(entry.TargetProtoRef);

            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, primaryButton, ButtonStyle.SecondaryPositive, entry.CanAccept || entry.CanCollect);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, NextBountyButtonRef, ButtonStyle.SecondaryPositive);
            game.GameDialogManager.ShowDialog(dialog);

            void OnReviewDialogResponse(ulong playerGuid, DialogResponse response)
            {
                Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
                if (responsePlayer == null)
                    return;

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    VillainIntelBoardEntry selected = board.Entries.ElementAtOrDefault(index);
                    if (selected == null)
                        return;

                    if (selected.CanCollect)
                        CollectReward(responsePlayer, index);
                    else
                        AcceptBounty(responsePlayer, index);
                    return;
                }

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                    ShowReviewDialog(responsePlayer, npc, index + 1);
            }
        }

        private static void GenerateBoard(Player player, VillainIntelBoardState board, bool spendCredits)
        {
            board.Entries.Clear();
            board.ReviewIndex = 0;

            List<VillainIntelThemeTuning> themes = _tuning.Themes
                .Where(theme => theme.Enabled && theme.Targets.Any(target => target.Enabled && target.AgentProtoRef != PrototypeId.Invalid))
                .ToList();

            if (themes.Count == 0)
                return;

            GRandom random = player.Game.Random;
            int size = _tuning.GetBoardSize();
            HashSet<string> usedTargetIds = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < size; i++)
            {
                VillainIntelThemeTuning theme = themes[random.Next(themes.Count)];
                VillainIntelTargetTuning target = PickTarget(theme, random, usedTargetIds) ?? PickAnyTarget(themes, random, usedTargetIds);
                VillainIntelRegionTuning region = PickRegion(theme, random) ?? _tuning.Regions.FirstOrDefault(regionEntry => regionEntry.Enabled && regionEntry.RegionProtoRef != PrototypeId.Invalid);

                if (target == null || region == null)
                    continue;

                usedTargetIds.Add(target.Id);
                int rank = _tuning.RollRank(random, i);

                board.Entries.Add(new()
                {
                    SlotIndex = i,
                    Rank = rank,
                    ThemeId = theme.Id,
                    ThemeName = string.IsNullOrWhiteSpace(theme.DisplayName) ? theme.Id : theme.DisplayName,
                    TargetId = target.Id,
                    TargetName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.AgentProtoRef.GetNameFormatted() : target.DisplayName,
                    RegionId = region.Id,
                    RegionName = string.IsNullOrWhiteSpace(region.DisplayName) ? region.Id : region.DisplayName,
                    HeroBased = target.HeroBased,
                    TargetProtoRef = target.AgentProtoRef,
                    RegionProtoRef = region.RegionProtoRef,
                    RewardTags = target.RewardTags?.ToList() ?? new()
                });
            }
        }

        private static VillainIntelTargetTuning PickTarget(VillainIntelThemeTuning theme, GRandom random, HashSet<string> usedTargetIds)
        {
            List<VillainIntelTargetTuning> candidates = theme.Targets
                .Where(target => target.Enabled && target.AgentProtoRef != PrototypeId.Invalid && usedTargetIds.Contains(target.Id) == false)
                .ToList();

            if (candidates.Count == 0)
                candidates = theme.Targets.Where(target => target.Enabled && target.AgentProtoRef != PrototypeId.Invalid).ToList();

            return PickWeighted(candidates, random);
        }

        private static VillainIntelTargetTuning PickAnyTarget(IEnumerable<VillainIntelThemeTuning> themes, GRandom random, HashSet<string> usedTargetIds)
        {
            List<VillainIntelTargetTuning> candidates = themes
                .SelectMany(theme => theme.Targets)
                .Where(target => target.Enabled && target.AgentProtoRef != PrototypeId.Invalid && usedTargetIds.Contains(target.Id) == false)
                .ToList();

            return PickWeighted(candidates, random);
        }

        private static VillainIntelTargetTuning PickWeighted(List<VillainIntelTargetTuning> candidates, GRandom random)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            int totalWeight = candidates.Sum(target => Math.Max(target.Weight, 1));
            int roll = random.Next(totalWeight);
            foreach (VillainIntelTargetTuning candidate in candidates)
            {
                roll -= Math.Max(candidate.Weight, 1);
                if (roll < 0)
                    return candidate;
            }

            return candidates[^1];
        }

        private static VillainIntelRegionTuning PickRegion(VillainIntelThemeTuning theme, GRandom random)
        {
            List<VillainIntelRegionTuning> candidates = _tuning.Regions
                .Where(region => region.Enabled &&
                                 region.RegionProtoRef != PrototypeId.Invalid &&
                                 (theme.RegionTags == null ||
                                  theme.RegionTags.Count == 0 ||
                                  region.Tags.Any(tag => theme.RegionTags.Contains(tag, StringComparer.OrdinalIgnoreCase))))
                .ToList();

            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }

        private static void SpawnAcceptedBounty(Player player, Avatar avatar, Region region, VillainIntelPendingHunt pending)
        {
            AgentPrototype agentProto = pending.Entry.TargetProtoRef.As<AgentPrototype>();
            if (agentProto == null)
            {
                pending.Entry.Status = VillainIntelBoardEntryStatus.Failed;
                Logger.Warn($"SpawnAcceptedBounty(): target prototype is not an AgentPrototype: {pending.Entry.TargetProtoRef.GetNameFormatted()}.");
                return;
            }

            Vector3 spawnPosition;
            if (agentProto.Bounds == null ||
                EntityHelper.GetSpawnPositionNearAvatar(avatar, region, agentProto.Bounds, 450f, out spawnPosition) == false)
            {
                spawnPosition = avatar.RegionLocation.Position + avatar.Forward * 220f;
            }

            spawnPosition = RegionLocation.ProjectToFloor(region, spawnPosition);
            Orientation spawnOrientation = Orientation.FromDeltaVector(avatar.RegionLocation.Position - spawnPosition);

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = pending.Entry.TargetProtoRef;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.RegionId = region.Id;
            settings.IsPopulation = true;

            using PropertyCollection properties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            int level = Math.Max(avatar.CharacterLevel, 60);
            properties[PropertyEnum.CharacterLevel] = level;
            properties[PropertyEnum.CombatLevel] = level;
            properties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
            properties[PropertyEnum.Rank] = agentProto.Rank?.DataRef ?? PrototypeId.Invalid;
            properties[PropertyEnum.HealthPctBonus] = _tuning.GetHealthMultiplier(pending.Entry.Rank) - 1f;
            properties[PropertyEnum.DamagePctBonus] = _tuning.GetDamageBonusPct(pending.Entry.Rank);
            settings.Properties = properties;

            Agent bounty = player.Game.EntityManager.CreateEntity(settings) as Agent;
            if (bounty == null)
            {
                pending.Entry.Status = VillainIntelBoardEntryStatus.Failed;
                Logger.Warn($"SpawnAcceptedBounty(): failed to spawn bounty {pending.Entry.TargetProtoRef.GetNameFormatted()}.");
                return;
            }

            MythicRiftStandaloneBossFixups.Apply(bounty, allowMissingAffixSettingsFallback: true);

            VillainIntelActiveHunt active = new(player.DatabaseUniqueId, pending.Entry, region, bounty.Id);
            active.EntityDeadAction = (in EntityDeadGameEvent evt) => OnBountyEntityDead(active, evt);
            region.EntityDeadEvent.AddActionBack(active.EntityDeadAction);
            ActiveHuntsByPlayerDbId[player.DatabaseUniqueId] = active;

            player.SendBannerMessage(HuntSpawnedBannerRef);
            Logger.Info($"SpawnAcceptedBounty(): spawned rank {pending.Entry.Rank} bounty {pending.Entry.TargetName} for playerDbId=0x{player.DatabaseUniqueId:X} entityId=0x{bounty.Id:X}.");
        }

        private static void OnBountyEntityDead(VillainIntelActiveHunt active, in EntityDeadGameEvent evt)
        {
            if (active == null || evt.Defender == null || evt.Defender.Id != active.TargetEntityId)
                return;

            active.Region?.EntityDeadEvent.RemoveAction(active.EntityDeadAction);
            ActiveHuntsByPlayerDbId.Remove(active.PlayerDbId);

            Player player = evt.Killer ?? active.Region?.Game.EntityManager.GetEntityByDbGuid<Player>(active.PlayerDbId);
            if (player == null)
            {
                active.Entry.Status = VillainIntelBoardEntryStatus.Defeated;
                return;
            }

            active.Entry.Status = VillainIntelBoardEntryStatus.Defeated;
            GrantRewards(player, player.CurrentAvatar, active.Entry, evt.Defender);
            active.Entry.Status = VillainIntelBoardEntryStatus.RewardCollected;
            player.SendBannerMessage(HuntDefeatedBannerRef);
            Logger.Info($"OnBountyEntityDead(): playerDbId=0x{active.PlayerDbId:X} defeated Villain Intel bounty {active.Entry.TargetName} rank={active.Entry.Rank}.");
        }

        private static void GrantRewards(Player player, Avatar avatar, VillainIntelBoardEntry entry, WorldEntity sourceEntity)
        {
            if (player == null || entry == null)
                return;

            foreach (VillainIntelRewardTuning reward in _tuning.Rewards)
            {
                if (reward.AppliesTo(entry.Rank, entry.RewardTags) == false)
                    continue;

                foreach (PrototypeId lootTableRef in reward.LootTableRefs)
                {
                    using LootInputSettings inputSettings = ObjectPoolManager.Instance.Get<LootInputSettings>();
                    inputSettings.Initialize(LootContext.Drop, player, sourceEntity ?? avatar);
                    player.Game.LootManager.SpawnLootFromTable(lootTableRef, inputSettings, 1);
                }

                foreach (VillainIntelGuaranteedItemTuning guaranteed in reward.GuaranteedItems)
                {
                    if (guaranteed.AppliesTo(entry.Rank) == false || guaranteed.ItemProtoRef == PrototypeId.Invalid)
                        continue;

                    int quantity = Math.Max(guaranteed.Quantity, 1);
                    for (int i = 0; i < quantity; i++)
                        player.Game.LootManager.SpawnItem(guaranteed.ItemProtoRef, LootContext.Drop, player, sourceEntity ?? avatar);
                }
            }
        }

        private static bool SpendCredits(Player player, int amount)
        {
            if (amount <= 0)
                return true;

            PrototypeId creditsRef = GameDatabase.CurrencyGlobalsPrototype.Credits;
            int currentCredits = player.Properties[PropertyEnum.Currency, creditsRef];
            if (currentCredits < amount)
                return false;

            player.Properties.AdjustProperty(-amount, new(PropertyEnum.Currency, creditsRef));
            return true;
        }

        private static void ResolveTuning(VillainIntelBoardTuning tuning)
        {
            foreach (VillainIntelRegionTuning region in tuning.Regions ?? new())
            {
                region.RegionProtoRef = ResolvePrototype(region.RegionPrototypeName);
                if (region.Enabled && region.RegionProtoRef == PrototypeId.Invalid)
                    Logger.Warn($"ResolveTuning(): Villain Intel region '{region.Id}' failed to resolve {region.RegionPrototypeName}.");
            }

            foreach (VillainIntelThemeTuning theme in tuning.Themes ?? new())
            {
                foreach (VillainIntelTargetTuning target in theme.Targets ?? new())
                {
                    target.AgentProtoRef = ResolvePrototype(target.PrototypeName);
                    if (target.Enabled && target.AgentProtoRef == PrototypeId.Invalid)
                        Logger.Warn($"ResolveTuning(): Villain Intel target '{target.Id}' failed to resolve {target.PrototypeName}.");
                }
            }

            foreach (VillainIntelRewardTuning reward in tuning.Rewards ?? new())
            {
                reward.LootTableRefs.Clear();
                foreach (string lootTableName in reward.LootTablePrototypeNames ?? new())
                {
                    PrototypeId lootTableRef = ResolvePrototype(lootTableName);
                    if (lootTableRef != PrototypeId.Invalid)
                        reward.LootTableRefs.Add(lootTableRef);
                    else if (reward.Enabled)
                        Logger.Warn($"ResolveTuning(): Villain Intel reward '{reward.Id}' failed to resolve loot table {lootTableName}.");
                }

                foreach (VillainIntelGuaranteedItemTuning item in reward.GuaranteedItems ?? new())
                {
                    item.ItemProtoRef = item.ItemPrototypeRuntimeId != 0
                        ? (PrototypeId)item.ItemPrototypeRuntimeId
                        : ResolvePrototype(item.ItemPrototypeName);
                }
            }
        }

        private static PrototypeId ResolvePrototype(string prototypeName)
        {
            if (string.IsNullOrWhiteSpace(prototypeName))
                return PrototypeId.Invalid;

            return GameDatabase.GetPrototypeRefByName(prototypeName);
        }

        private static PrototypeId ResolveBoardNpcRef()
        {
            foreach (string prototypeName in _tuning.NpcPrototypeNames ?? new())
            {
                PrototypeId protoRef = ResolvePrototype(prototypeName);
                if (protoRef != PrototypeId.Invalid && protoRef.As<WorldEntityPrototype>() != null)
                    return protoRef;
            }

            return PrototypeId.Invalid;
        }

        private static LocaleStringId ResolveTargetButtonText(PrototypeId targetProtoRef)
        {
            WorldEntityPrototype targetProto = targetProtoRef.As<WorldEntityPrototype>();
            return targetProto?.DisplayName != LocaleStringId.Blank
                ? targetProto.DisplayName
                : GenericBountyTargetButtonRef;
        }

        private static void TryReloadTuningIfNeeded()
        {
            if (_tuning == null || _lastLoadMessage == "Villain Intel Board has not loaded tuning yet.")
                TryReloadTuning(out _);
        }

        private sealed record VillainIntelPendingHunt(VillainIntelBoardEntry Entry)
        {
            public PrototypeId RegionProtoRef => Entry.RegionProtoRef;
        }

        private sealed class VillainIntelActiveHunt
        {
            public ulong PlayerDbId { get; }
            public VillainIntelBoardEntry Entry { get; }
            public Region Region { get; }
            public ulong TargetEntityId { get; }
            public Event<EntityDeadGameEvent>.Action EntityDeadAction { get; set; }

            public VillainIntelActiveHunt(ulong playerDbId, VillainIntelBoardEntry entry, Region region, ulong targetEntityId)
            {
                PlayerDbId = playerDbId;
                Entry = entry;
                Region = region;
                TargetEntityId = targetEntityId;
            }
        }
    }
}
