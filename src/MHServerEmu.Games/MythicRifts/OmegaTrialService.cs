using Gazillion;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Missions;
using MHServerEmu.Games.Navi;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.MythicRifts
{
    public static class OmegaTrialService
    {
        // private static readonly Logger Logger = LogManager.CreateLogger();

        private const string SilverSableOmegaTrialGuidePrototypeName = "Entity/Characters/NPCs/SilverSableHelicarrier.prototype";
        private const string SurturTrialEntryTargetPrototypeName = "Regions/RAIDS/MuspelheimRaid/ConnectionNodes/SurturRaidReduxEntryTargetBase.prototype";
        private const string SurturFinalEncounterHotspotPrototypeName = "Entity/Missions/Hotspots/SurturRaidFinalEncTriggerHotspot.prototype";
        private const string LokiPhase1PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase1.prototype";
        private const string LokiPhase2PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase2.prototype";
        private const string SurturBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturBoss.prototype";
        private const string SurturBossSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/SurturBossSpawner.prototype";
        private const string ReturnPortalPrototypeName = "Entity/Transitions/ReturnToLastBaseDR.prototype";
        private const string HelicarrierEntryTargetPrototypeName = "Regions/HUBS/Helicarrier/Connections/HelicarrierEntryTarget.prototype";
        private const string OmegaTrialLevelWidgetPrototypeName = "UI/MetaGame/MissionName.prototype";
        private const string OmegaTrialQuotaWidgetPrototypeName = "UI/MetaGame/DangerRoom/DangerRoomCounterBarBASE.prototype";
        private const string OmegaTrialTimerWidgetPrototypeName = "UI/MetaGame/DangerRoom/DangerRoomTimer.prototype";

        private static readonly PrototypeId OmegaTrialDifficultyTierRef = (PrototypeId)1087474643293441873;
        private static readonly PrototypeId OmegaTrialLevelWidgetPrototypeRef = (PrototypeId)7164846210465729875UL;
        private static readonly PrototypeId OmegaTrialQuotaWidgetPrototypeRef = (PrototypeId)1488507445230442250UL;
        private static readonly PrototypeId OmegaTrialTimerWidgetPrototypeRef = (PrototypeId)15369535438503023451UL;
        private static readonly LocaleStringId SilverSableOmegaTrialFlavorTextRef = (LocaleStringId)18000000000000040200;
        private static readonly LocaleStringId OmegaTrialTitleTextRef = (LocaleStringId)18000000000000040201;
        private static readonly LocaleStringId OmegaTrialDefeatLokiTextRef = (LocaleStringId)18000000000000040202;
        private static readonly LocaleStringId OmegaTrialDefeatSurturTextRef = (LocaleStringId)18000000000000040203;
        private static readonly LocaleStringId OmegaPatrolUnlockedMessageRef = (LocaleStringId)18000000000000080203;
        private static readonly LocaleStringId YesButtonRef = (LocaleStringId)14959079863731815684;
        private static readonly LocaleStringId NoButtonRef = (LocaleStringId)16244338063872951558;

        private const float TrialPlayerToMobDamageMultiplier = 1f;
        private const float TrialMobToPlayerDamageMultiplier = 1f;
        private const float LokiSpawnDistance = 600f;
        private const float SurturSpawnDistance = 1200f;
        private const float BossSpawnSearchDistance = 3200f;
        private static readonly TimeSpan LokiFightTimeLimit = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SurturFightTimeLimit = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PostEntryNativeSuppressionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan TrialReturnPortalLifespan = TimeSpan.FromMinutes(10);
        private static readonly Vector3 SurturFinalEncounterHotspotExportPosition = new(11024f, 13072f, 176f);
        private static readonly Vector3 SurturFinalBossEncounterExportPosition = new(12193.019f, 14205.562f, 255.99998f);

        private static readonly object SyncRoot = new();
        private static readonly Dictionary<ulong, OmegaTrialState> TrialStates = new();
        private static readonly Dictionary<ulong, Event<EntityDeadGameEvent>.Action> RegionEntityDeadActions = new();

        private enum OmegaTrialStage
        {
            Traveling,
            LokiPhase1Active,
            LokiActive,
            SurturActive
        }

        public static bool TryUseOmegaTrialGuide(Player player, WorldEntity interactableObject)
        {
            if (player == null || interactableObject == null || IsSilverSableOmegaTrialGuide(interactableObject) == false)
                return false;

            UseSilverSableOmegaTrialGuide(player, interactableObject);
            return true;
        }

        public static bool ShouldSuppressNativeTrialMissionNotification(Player player, PrototypeId missionRef)
        {
            if (player == null)
                return false;

            lock (SyncRoot)
            {
                if (TrialStates.TryGetValue(player.DatabaseUniqueId, out OmegaTrialState state) == false)
                    return false;

                if (state.Stage == OmegaTrialStage.Traveling)
                    return true;

                return player.CurrentAvatar?.Region != null &&
                       state.RegionId == player.CurrentAvatar.Region.Id;
            }
        }

        public static bool ShouldSuppressNativeTrialBannerMessage(Player player, BannerMessagePrototype bannerMessage)
        {
            if (player == null || bannerMessage == null)
                return false;

            if (bannerMessage.BannerText == OmegaPatrolUnlockedMessageRef)
                return false;

            return ShouldSuppressNativeTrialMissionNotification(player, PrototypeId.Invalid);
        }

        public static void OnAvatarEnteredRegion(Player player, Region region)
        {
            if (player == null || region == null)
                return;

            OmegaTrialState state;
            lock (SyncRoot)
            {
                if (TrialStates.TryGetValue(player.DatabaseUniqueId, out state) == false)
                    return;
            }

            PrototypeId trialRegionRef = GetSurturTrialRegionRef();
            if (trialRegionRef == PrototypeId.Invalid || region.PrototypeDataRef != trialRegionRef)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=wrong-region playerDbId=0x{player.DatabaseUniqueId:X} region={region.PrototypeDataRef.GetNameFormatted()} expected={trialRegionRef.GetNameFormatted()}");
                EndTrial(player.DatabaseUniqueId);
                return;
            }

            if (state.Stage != OmegaTrialStage.Traveling)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=entered-existing playerDbId=0x{player.DatabaseUniqueId:X} region={region.PrototypeDataRef.GetNameFormatted()} regionId=0x{region.Id:X} trialStage={state.Stage}");
                return;
            }

            // Logger.Info($"[OmegaTrialTrace] stage=entered playerDbId=0x{player.DatabaseUniqueId:X} avatar={player.CurrentAvatar} region={region.PrototypeDataRef.GetNameFormatted()} regionId=0x{region.Id:X} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            state.RegionId = region.Id;
            state.CachedRegion = region;
            ApplyTrialRegionScaling(state, region);
            EnsureRegionListeners(region);
            SuppressNativeTrialContent(player, region, state);
            SchedulePostEntryNativeSuppression(player, state);
            bool movedToArena = TryMoveAvatarToFinalArena(player, region, state);
            // Logger.Info($"[OmegaTrialTrace] stage=arena-move playerDbId=0x{player.DatabaseUniqueId:X} regionId=0x{region.Id:X} moved={movedToArena} avatarPosition={player.CurrentAvatar?.RegionLocation.Position}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);

            if (TrySpawnBossForTrial(player, region, state, GetPrototypeRefByName(LokiPhase1PrototypeName), LokiSpawnDistance, out Agent loki) == false)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-failed playerDbId=0x{player.DatabaseUniqueId:X} boss={LokiPhase1PrototypeName} regionId=0x{region.Id:X}");
                TrySpawnReturnPortal(player, region, state);
                EndTrial(player.DatabaseUniqueId);
                return;
            }

            state.Stage = OmegaTrialStage.LokiPhase1Active;
            state.LokiEntityId = loki.Id;
            StartPhaseTimer(player, state, LokiFightTimeLimit);
            SendTrialChat(player, "Defeat Loki. Time limit: 5 minutes.");
            // Logger.Info($"[OmegaTrialTrace] stage=spawned-loki-phase1 playerDbId=0x{player.DatabaseUniqueId:X} entityId=0x{loki.Id:X} position={loki.RegionLocation.Position} level={loki.Properties[PropertyEnum.CharacterLevel]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        public static void Update(TimeSpan currentTime)
        {
            List<OmegaTrialState> activeStates;
            lock (SyncRoot)
            {
                if (TrialStates.Count == 0)
                    return;

                activeStates = new(TrialStates.Values);
            }

            foreach (OmegaTrialState state in activeStates)
            {
                lock (SyncRoot)
                {
                    if (TrialStates.ContainsKey(state.PlayerDbId) == false)
                        continue;
                }

                Region region = state.CachedRegion;
                Game game = region?.Game;
                Player player = game?.EntityManager.GetEntityByDbGuid<Player>(state.PlayerDbId);

                if (player?.CurrentAvatar == null || region == null || region.ShutdownRequested)
                    continue;

                if (state.PostEntryNativeSuppressionPending && currentTime >= state.PostEntryNativeSuppressionAt)
                {
                    state.PostEntryNativeSuppressionPending = false;
                    SuppressNativeTrialContent(player, region, state);
                }

                if (state.PhaseExpiresAt <= TimeSpan.Zero || currentTime < state.PhaseExpiresAt)
                    continue;

                if (IsTimedStage(state.Stage) == false)
                    continue;

                // Logger.Info($"[OmegaTrialTrace] stage=phase-timeout playerDbId=0x{state.PlayerDbId:X} trialStage={state.Stage} regionId=0x{region.Id:X}");
                SendStopTrialTimer(player, state);
                TrySpawnReturnPortal(player, region, state);
                EndTrial(state.PlayerDbId);
            }
        }

        private static bool IsSilverSableOmegaTrialGuide(WorldEntity interactableObject)
        {
            PrototypeId silverSableRef = GameDatabase.GetPrototypeRefByName(SilverSableOmegaTrialGuidePrototypeName);
            return silverSableRef != PrototypeId.Invalid && interactableObject.PrototypeDataRef == silverSableRef;
        }

        private static void UseSilverSableOmegaTrialGuide(Player player, WorldEntity silverSable)
        {
            Game game = player.Game;

            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnSilverSableOmegaTrialGuideDialogResponse;
            dialog.Message.LocaleString = SilverSableOmegaTrialFlavorTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = silverSable.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, YesButtonRef, ButtonStyle.SecondaryPositive, false, true);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, NoButtonRef, ButtonStyle.SecondaryNegative, false, true);
            game.GameDialogManager.PostDialogToClient(dialog);
            // Logger.Info($"[OmegaTrialTrace] stage=dialog-posted playerDbId=0x{player.DatabaseUniqueId:X} guide={silverSable}");

            void OnSilverSableOmegaTrialGuideDialogResponse(ulong playerGuid, DialogResponse response)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=dialog-response playerDbId=0x{playerGuid:X} response={response.ButtonIndex}");
                if (response.ButtonIndex != GameDialogResultEnum.eGDR_Option1)
                    return;

                Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
                if (responsePlayer == null)
                    return;

                StartOmegaTrial(responsePlayer);
            }
        }

        private static void StartOmegaTrial(Player player)
        {
            if (player.GetParty() != null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=start-rejected playerDbId=0x{player.DatabaseUniqueId:X} reason=party");
                player.SendBannerMessage(GameDatabase.UIGlobalsPrototype.MessageRegionRestricted);
                return;
            }

            PrototypeId entryTargetRef = GetPrototypeRefByName(SurturTrialEntryTargetPrototypeName);
            if (entryTargetRef == PrototypeId.Invalid)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=start-rejected playerDbId=0x{player.DatabaseUniqueId:X} reason=missing-entry-target target={SurturTrialEntryTargetPrototypeName}");
                return;
            }

            EndTrial(player.DatabaseUniqueId);

            lock (SyncRoot)
            {
                TrialStates[player.DatabaseUniqueId] = new()
                {
                    PlayerDbId = player.DatabaseUniqueId,
                    Stage = OmegaTrialStage.Traveling
                };
            }

            using var teleporterHandle = TeleporterPool.Get(out Teleporter teleporter);
            teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Transition);
            teleporter.DifficultyTierRef = OmegaTrialDifficultyTierRef;
            teleporter.BypassQueueRegionForRift = true;

            // Logger.Info($"[OmegaTrialTrace] stage=teleport-start playerDbId=0x{player.DatabaseUniqueId:X} target={entryTargetRef.GetNameFormatted()} difficulty={OmegaTrialDifficultyTierRef.GetNameFormatted()}");
            if (teleporter.TeleportToTarget(entryTargetRef) == false)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=teleport-failed playerDbId=0x{player.DatabaseUniqueId:X} target={entryTargetRef.GetNameFormatted()}");
                EndTrial(player.DatabaseUniqueId);
            }
        }

        private static void EnsureRegionListeners(Region region)
        {
            if (region == null)
                return;

            Event<EntityDeadGameEvent>.Action deadAction = (in EntityDeadGameEvent evt) => OnRegionEntityDead(region.Game, region.Id, evt);
            lock (SyncRoot)
            {
                if (RegionEntityDeadActions.ContainsKey(region.Id) == false)
                {
                    region.EntityDeadEvent.AddActionBack(deadAction);
                    RegionEntityDeadActions[region.Id] = deadAction;
                }
            }
        }

        private static void OnRegionEntityDead(Game game, ulong regionId, in EntityDeadGameEvent evt)
        {
            if (game == null || evt.Defender == null)
                return;

            List<OmegaTrialState> activeRegionStates = null;
            lock (SyncRoot)
            {
                foreach (OmegaTrialState state in TrialStates.Values)
                {
                    if (state.RegionId == regionId)
                    {
                        activeRegionStates ??= new();
                        activeRegionStates.Add(state);
                    }
                }
            }

            if (activeRegionStates == null)
                return;

            foreach (OmegaTrialState state in activeRegionStates)
            {
                Player player = game.EntityManager.GetEntityByDbGuid<Player>(state.PlayerDbId);
                if (player?.CurrentAvatar == null || player.CurrentAvatar.Region?.Id != regionId)
                {
                    // Logger.Info($"[OmegaTrialTrace] stage=ending-missing-player playerDbId=0x{state.PlayerDbId:X} regionId=0x{regionId:X}");
                    EndTrial(state.PlayerDbId);
                    continue;
                }

                if (state.Stage == OmegaTrialStage.LokiPhase1Active && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase1PrototypeName))
                {
                    // Logger.Info($"[OmegaTrialTrace] stage=loki-phase1-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                    SpawnLokiPhase2(player, player.CurrentAvatar.Region, state);
                    continue;
                }

                if (state.Stage == OmegaTrialStage.LokiActive && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase2PrototypeName))
                {
                    // Logger.Info($"[OmegaTrialTrace] stage=loki-phase2-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                    SpawnSurturPhase(player, player.CurrentAvatar.Region, state);
                    continue;
                }

                if (state.Stage == OmegaTrialStage.SurturActive && IsTrackedDeath(evt.Defender, state.SurturEntityId, SurturBossPrototypeName))
                {
                    // Logger.Info($"[OmegaTrialTrace] stage=surtur-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                    CompleteTrial(player, state);
                }
            }
        }

        private static void SpawnLokiPhase2(Player player, Region region, OmegaTrialState state)
        {
            PrototypeId lokiRef = GetPrototypeRefByName(LokiPhase2PrototypeName);
            if (TrySpawnBossForTrial(player, region, state, lokiRef, LokiSpawnDistance, out Agent loki) == false)
            {
                string regionIdText = region != null ? $"0x{region.Id:X}" : "unknown";
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-failed playerDbId=0x{state.PlayerDbId:X} boss={LokiPhase2PrototypeName} regionId={regionIdText}");
                SendStopTrialTimer(player, state);
                TrySpawnReturnPortal(player, region, state);
                EndTrial(state.PlayerDbId);
                return;
            }

            state.Stage = OmegaTrialStage.LokiActive;
            state.LokiEntityId = loki.Id;
            // Logger.Info($"[OmegaTrialTrace] stage=spawned-loki-phase2 playerDbId=0x{state.PlayerDbId:X} entityId=0x{loki.Id:X} position={loki.RegionLocation.Position} level={loki.Properties[PropertyEnum.CharacterLevel]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static void SpawnSurturPhase(Player player, Region region, OmegaTrialState state)
        {
            SuppressNativeTrialContent(player, region, state);

            PrototypeId surturRef = GetPrototypeRefByName(SurturBossPrototypeName);
            bool spawnedSurtur = TrySpawnBossAtNativeSpawner(player, region, state, surturRef, SurturBossSpawnerPrototypeName, out Agent surtur) ||
                                 TrySpawnSurturAtNativeFinalEncounterMarker(player, region, state, surturRef, out surtur) ||
                                 TrySpawnBossForTrial(player, region, state, surturRef, SurturSpawnDistance, out surtur);

            if (spawnedSurtur == false)
            {
                string regionIdText = region != null ? $"0x{region.Id:X}" : "unknown";
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-failed playerDbId=0x{state.PlayerDbId:X} boss={SurturBossPrototypeName} regionId={regionIdText}");
                SendStopTrialTimer(player, state);
                TrySpawnReturnPortal(player, region, state);
                EndTrial(state.PlayerDbId);
                return;
            }

            state.Stage = OmegaTrialStage.SurturActive;
            state.SurturEntityId = surtur.Id;
            StartPhaseTimer(player, state, SurturFightTimeLimit);
            SendTrialChat(player, "Defeat Surtur. Time limit: 5 minutes.");
            // Logger.Info($"[OmegaTrialTrace] stage=spawned-surtur playerDbId=0x{state.PlayerDbId:X} entityId=0x{surtur.Id:X} position={surtur.RegionLocation.Position} level={surtur.Properties[PropertyEnum.CharacterLevel]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static void CompleteTrial(Player player, OmegaTrialState state)
        {
            bool grantedAccess = RiftAccessTeleportService.GrantOmegaPatrolAccessForCurrentAvatar(player);
            // Logger.Info($"[OmegaTrialTrace] stage=complete playerDbId=0x{state.PlayerDbId:X} grantedOmegaAccess={grantedAccess}");
            if (grantedAccess)
            {
                player.SendBannerMessage(OmegaPatrolUnlockedMessageRef, doNotQueue: true, showImmediately: true);
                SendTrialChat(player, "Omega Patrols unlocked. Use the portal to return to the Helicarrier.");
            }

            SendStopTrialTimer(player, state);
            TrySpawnReturnPortal(player, state.CachedRegion, state);
            RefreshTrialWidgets(state.CachedRegion?.UIDataProvider, state, player.Game.CurrentTime, complete: true);
            EndTrial(state.PlayerDbId, clearWidgets: false);
        }

        private static void SchedulePostEntryNativeSuppression(Player player, OmegaTrialState state)
        {
            if (player?.Game == null || state == null)
                return;

            state.PostEntryNativeSuppressionPending = true;
            state.PostEntryNativeSuppressionAt = player.Game.CurrentTime + PostEntryNativeSuppressionDelay;
        }

        private static bool IsTimedStage(OmegaTrialStage stage)
        {
            return stage == OmegaTrialStage.LokiPhase1Active ||
                   stage == OmegaTrialStage.LokiActive ||
                   stage == OmegaTrialStage.SurturActive;
        }

        private static void SendTrialChat(Player player, string message)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
                return;

            player.Game?.ChatManager?.SendChatFromCustomSystem(player, $"[Omega Trial] {message}", showSender: false);
        }

        private static void StartPhaseTimer(Player player, OmegaTrialState state, TimeSpan duration)
        {
            if (player?.Game == null || state == null || duration <= TimeSpan.Zero)
                return;

            state.PhaseStartedAt = player.Game.CurrentTime;
            state.PhaseExpiresAt = state.PhaseStartedAt + duration;
            state.TimerMetaGameId = state.RegionId != 0 ? state.RegionId : state.PlayerDbId;
            SendStartTrialTimer(player, state, duration);
        }

        private static void SendStartTrialTimer(Player player, OmegaTrialState state, TimeSpan duration)
        {
            if (player == null || state == null || state.TimerMetaGameId == 0)
                return;

            try
            {
                NetMessageStartPvPTimer message = NetMessageStartPvPTimer.CreateBuilder()
                    .SetMetaGameId(state.TimerMetaGameId)
                    .SetStartTime((uint)Math.Min(duration.TotalMilliseconds, uint.MaxValue))
                    .SetEndTime(0)
                    .SetLowTimeWarning((uint)TimeSpan.FromMinutes(2).TotalMilliseconds)
                    .SetCriticalTimeWarning((uint)TimeSpan.FromMinutes(1).TotalMilliseconds)
                    .SetLabelOverrideTextId((ulong)OmegaTrialTitleTextRef)
                    .Build();

                player.SendMessage(message);
                // Logger.Info($"[OmegaTrialTrace] stage=timer-start playerDbId=0x{state.PlayerDbId:X} trialStage={state.Stage} timerId=0x{state.TimerMetaGameId:X} durationMs={(long)duration.TotalMilliseconds}");
            }
            catch (Exception)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=timer-start-failed playerDbId=0x{state.PlayerDbId:X} reason={e.Message}");
            }
        }

        private static void SendStopTrialTimer(Player player, OmegaTrialState state)
        {
            if (player == null || state == null || state.TimerMetaGameId == 0)
                return;

            try
            {
                NetMessageStopPvPTimer message = NetMessageStopPvPTimer.CreateBuilder()
                    .SetMetaGameId(state.TimerMetaGameId)
                    .Build();

                player.SendMessage(message);
                state.PhaseExpiresAt = TimeSpan.Zero;
            }
            catch (Exception)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=timer-stop-failed playerDbId=0x{state.PlayerDbId:X} reason={e.Message}");
            }
        }

        private static bool TrySpawnReturnPortal(Player player, Region region, OmegaTrialState state)
        {
            if (player?.CurrentAvatar == null || region?.Game?.EntityManager == null || state == null)
                return false;

            if (state.ReturnPortalEntityId != Entity.InvalidId && region.Game.EntityManager.GetEntity<Transition>(state.ReturnPortalEntityId) != null)
                return true;

            PrototypeId portalRef = GetPrototypeRefByName(ReturnPortalPrototypeName);
            PrototypeId targetRef = GetPrototypeRefByName(HelicarrierEntryTargetPrototypeName);
            if (portalRef == PrototypeId.Invalid || targetRef == PrototypeId.Invalid)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=return-portal-failed playerDbId=0x{state.PlayerDbId:X} reason=missing-prototype portal={portalRef.GetNameFormatted()} target={targetRef.GetNameFormatted()}");
                return false;
            }

            if (TryResolveReturnPortalLocation(player.CurrentAvatar, region, state, out Vector3 position, out Orientation orientation, out Cell cell) == false)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=return-portal-failed playerDbId=0x{state.PlayerDbId:X} reason=no-location regionId=0x{region.Id:X}");
                return false;
            }

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = portalRef;
            settings.RegionId = region.Id;
            settings.Position = position;
            settings.Orientation = orientation;
            settings.Cell = cell;
            settings.Lifespan = TrialReturnPortalLifespan;
            settings.SourceEntityId = player.CurrentAvatar.Id;

            using var settingsPropertiesHandle = PropertyCollectionPool.Get(out PropertyCollection settingsProperties);
            settingsProperties[PropertyEnum.Interactable] = (int)TriBool.True;
            settingsProperties[PropertyEnum.InteractableUsesLeft] = -1;
            settingsProperties[PropertyEnum.Visible] = true;
            settings.Properties = settingsProperties;

            Transition portal = region.Game.EntityManager.CreateEntity(settings) as Transition;
            if (portal == null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=return-portal-failed playerDbId=0x{state.PlayerDbId:X} reason=create-failed");
                return false;
            }

            if (portal.ConfigureDirectTarget(targetRef) == false)
            {
                portal.Destroy();
                // Logger.Info($"[OmegaTrialTrace] stage=return-portal-failed playerDbId=0x{state.PlayerDbId:X} reason=target-failed target={targetRef.GetNameFormatted()}");
                return false;
            }

            state.ReturnPortalEntityId = portal.Id;
            // Logger.Info($"[OmegaTrialTrace] stage=return-portal-spawned playerDbId=0x{state.PlayerDbId:X} entityId=0x{portal.Id:X} position={position} target={targetRef.GetNameFormatted()}");
            return true;
        }

        private static bool TryResolveReturnPortalLocation(Avatar avatar, Region region, OmegaTrialState state, out Vector3 position, out Orientation orientation, out Cell cell)
        {
            position = Vector3.Zero;
            orientation = Orientation.Zero;
            cell = null;

            if (avatar == null || region == null)
                return false;

            if (state?.HasArenaAnchor == true)
            {
                position = state.ArenaAnchorPosition;
                orientation = state.ArenaAnchorOrientation;
                cell = state.ArenaAnchorCell ?? region.GetCellAtPosition(position);
            }
            else
            {
                position = avatar.RegionLocation.Position + (avatar.Forward * 250f);
                orientation = avatar.RegionLocation.Orientation;
                cell = avatar.Cell ?? region.GetCellAtPosition(position);
            }

            position = RegionLocation.ProjectToFloor(region, cell, position);
            cell = region.GetCellAtPosition(position) ?? cell;
            return cell != null && cell.IntersectsXY(position);
        }

        private static void SuppressNativeTrialContent(Player player, Region region, OmegaTrialState state)
        {
            if (region == null || state == null)
                return;

            int metagamesRemoved = SuppressNativeTrialMetaGames(region);
            int missionsSuppressed = SuppressNativeTrialMissions(player, region);
            (int spawnEventsDestroyed, int agentsDestroyed) = ClearNativeTrialPopulation(region, state);
            int widgetsRemoved = RemoveNativeTrialWidgets(region.UIDataProvider);
            int dialogsRemoved = player?.Game?.GameDialogManager?.RemoveDialogsFromClient(state.PlayerDbId) ?? 0;
            // Logger.Info($"[OmegaTrialTrace] stage=suppress-native playerDbId=0x{state.PlayerDbId:X} regionId=0x{region.Id:X} metagamesRemoved={metagamesRemoved} missionsSuppressed={missionsSuppressed} spawnEventsDestroyed={spawnEventsDestroyed} agentsDestroyed={agentsDestroyed} widgetsRemoved={widgetsRemoved} dialogsRemoved={dialogsRemoved}");
        }

        private static int SuppressNativeTrialMetaGames(Region region)
        {
            if (region?.Game?.EntityManager == null)
                return 0;

            int removedCount = 0;
            while (region.MetaGames.Count > 0)
            {
                ulong metaGameId = region.MetaGames[0];
                Entity metaGame = region.Game.EntityManager.GetEntity<Entity>(metaGameId);
                metaGame?.Destroy();
                region.MetaGames.Remove(metaGameId);
                removedCount++;
            }

            return removedCount;
        }

        private static int SuppressNativeTrialMissions(Player player, Region region)
        {
            return SuppressNativeTrialMissionsFromManager(player, region, region?.MissionManager)
                + SuppressNativeTrialMissionsFromManager(player, region, player?.MissionManager);
        }

        private static int SuppressNativeTrialMissionsFromManager(Player player, Region region, MissionManager missionManager)
        {
            if (player == null || region == null || missionManager == null)
                return 0;

            int suppressedCount = 0;
            List<PrototypeId> activeMissionRefs = new(missionManager.ActiveMissions);
            foreach (PrototypeId missionRef in activeMissionRefs)
            {
                Mission mission = missionManager.FindMissionByDataRef(missionRef);
                if (mission == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                    continue;

                if (mission.IsSuspended == false)
                    mission.SetSuspendedState(true);

                SendNativeMissionTrackerSuppression(player, mission);
                ClearNativeMissionWidgets(region, mission);
                suppressedCount++;
            }

            return suppressedCount;
        }

        private static void SendNativeMissionTrackerSuppression(Player player, Mission mission)
        {
            if (player == null || mission == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                return;

            NetMessageMissionUpdate missionMessage = NetMessageMissionUpdate.CreateBuilder()
                .SetMissionPrototypeId((ulong)mission.PrototypeDataRef)
                .SetMissionState((uint)MissionState.Inactive)
                .SetSuppressNotification(true)
                .SetSuspendedState(true)
                .Build();

            player.SendMessage(missionMessage);

            foreach (MissionObjective objective in mission.Objectives)
                SendNativeObjectiveTrackerSuppression(player, mission, objective);
        }

        private static void SendNativeObjectiveTrackerSuppression(Player player, Mission mission, MissionObjective objective)
        {
            if (player == null || mission == null || objective == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                return;

            NetMessageMissionObjectiveUpdate objectiveMessage = NetMessageMissionObjectiveUpdate.CreateBuilder()
                .SetMissionPrototypeId((ulong)mission.PrototypeDataRef)
                .SetObjectiveIndex(objective.PrototypeIndex)
                .SetObjectiveState((uint)MissionObjectiveState.Invalid)
                .SetCurrentCount(0)
                .SetRequiredCount(0)
                .SetFailCurrentCount(0)
                .SetFailRequiredCount(0)
                .SetSuppressNotification(true)
                .SetSuspendedState(true)
                .Build();

            player.SendMessage(objectiveMessage);
        }

        private static void ClearNativeMissionWidgets(Region region, Mission mission)
        {
            UIDataProvider uiDataProvider = region?.UIDataProvider;
            if (uiDataProvider == null || mission == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                return;

            foreach (MissionObjective objective in mission.Objectives)
            {
                MissionObjectivePrototype objectiveProto = objective?.Prototype;
                if (objectiveProto == null)
                    continue;

                if (objectiveProto.MetaGameWidget != PrototypeId.Invalid)
                    uiDataProvider.DeleteWidget(objectiveProto.MetaGameWidget, mission.PrototypeDataRef);

                if (objectiveProto.MetaGameWidgetFail != PrototypeId.Invalid)
                    uiDataProvider.DeleteWidget(objectiveProto.MetaGameWidgetFail, mission.PrototypeDataRef);
            }

            PrototypeId missionNameWidgetRef = GameDatabase.UIGlobalsPrototype?.MetaGameWidgetMissionName ?? PrototypeId.Invalid;
            if (missionNameWidgetRef != PrototypeId.Invalid)
                uiDataProvider.DeleteWidget(missionNameWidgetRef, mission.PrototypeDataRef);
        }

        private static (int SpawnEventsDestroyed, int AgentsDestroyed) ClearNativeTrialPopulation(Region region, OmegaTrialState state)
        {
            if (region == null || state == null)
                return (0, 0);

            int spawnEventsDestroyed = 0;
            foreach (Area area in region.IterateAreas())
            {
                var spawnEvent = area?.PopulationArea?.SpawnEvent;
                if (spawnEvent == null)
                    continue;

                spawnEvent.Destroy();
                spawnEventsDestroyed++;
            }

            List<Agent> nativeAgentsToDestroy = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not Agent agent)
                    continue;

                if (ShouldDestroyNativeTrialAgent(agent, state))
                {
                    nativeAgentsToDestroy ??= new();
                    nativeAgentsToDestroy.Add(agent);
                }
            }

            if (nativeAgentsToDestroy == null)
                return (spawnEventsDestroyed, 0);

            foreach (Agent nativeAgent in nativeAgentsToDestroy)
                nativeAgent.Destroy();

            return (spawnEventsDestroyed, nativeAgentsToDestroy.Count);
        }

        private static int RemoveNativeTrialWidgets(UIDataProvider uiDataProvider)
        {
            if (uiDataProvider == null)
                return 0;

            PrototypeId contextRef = GetOmegaTrialWidgetContextRef();
            if (contextRef == PrototypeId.Invalid)
                return 0;

            return MythicRiftUiController.RemoveNativeWidgets(
                uiDataProvider,
                contextRef,
                GetOmegaTrialLevelWidgetPrototypeRef(),
                GetOmegaTrialQuotaWidgetPrototypeRef(),
                GetOmegaTrialTimerWidgetPrototypeRef());
        }

        private static void RefreshTrialWidgets(UIDataProvider uiDataProvider, OmegaTrialState state, TimeSpan currentTime, bool complete = false)
        {
            if (uiDataProvider == null || state == null)
                return;

            PrototypeId contextRef = GetOmegaTrialWidgetContextRef();
            if (contextRef == PrototypeId.Invalid)
                return;

            UIWidgetMissionText levelWidget = GetTrialMissionTextWidget(uiDataProvider, GetOmegaTrialLevelWidgetPrototypeRef(), contextRef);
            if (levelWidget != null)
            {
                levelWidget.SetAreaContext(contextRef);
                levelWidget.SetText(OmegaTrialTitleTextRef, complete ? OmegaPatrolUnlockedMessageRef : GetStageObjectiveTextRef(state.Stage));
            }

            UIWidgetGenericFraction objectiveWidget = GetTrialGenericFractionWidget(uiDataProvider, GetOmegaTrialQuotaWidgetPrototypeRef(), contextRef);
            if (objectiveWidget != null)
            {
                objectiveWidget.SetAreaContext(contextRef);
                objectiveWidget.SetCount(complete ? 2 : GetStageProgressCount(state.Stage), 2);
            }

            UIWidgetGenericFraction timerWidget = GetTrialGenericFractionWidget(uiDataProvider, GetOmegaTrialTimerWidgetPrototypeRef(), contextRef);
            long remainingMs = GetPhaseTimeRemainingMs(state, currentTime);
            if (timerWidget != null && complete == false && remainingMs > 0)
            {
                timerWidget.SetAreaContext(contextRef);
                timerWidget.SetTimeRemaining(remainingMs);
            }
            else
            {
                uiDataProvider.DeleteWidget(GetOmegaTrialTimerWidgetPrototypeRef(), contextRef);
            }

            // Logger.Info($"[OmegaTrialTrace] stage=widgets-refreshed playerDbId=0x{state.PlayerDbId:X} context={contextRef.GetNameFormatted()} trialStage={state.Stage} complete={complete} levelWidget={(levelWidget != null)} objectiveWidget={(objectiveWidget != null)} timerWidget={(timerWidget != null)} count={(complete ? 2 : GetStageProgressCount(state.Stage))}/2 remainingMs={remainingMs}");
        }

        private static void ClearTrialWidgets(UIDataProvider uiDataProvider)
        {
            if (uiDataProvider == null)
                return;

            PrototypeId contextRef = GetOmegaTrialWidgetContextRef();
            if (contextRef == PrototypeId.Invalid)
                return;

            uiDataProvider.DeleteWidget(GetOmegaTrialLevelWidgetPrototypeRef(), contextRef);
            uiDataProvider.DeleteWidget(GetOmegaTrialQuotaWidgetPrototypeRef(), contextRef);
            uiDataProvider.DeleteWidget(GetOmegaTrialTimerWidgetPrototypeRef(), contextRef);
        }

        private static LocaleStringId GetStageObjectiveTextRef(OmegaTrialStage stage)
        {
            return stage switch
            {
                OmegaTrialStage.SurturActive => OmegaTrialDefeatSurturTextRef,
                _ => OmegaTrialDefeatLokiTextRef
            };
        }

        private static int GetStageProgressCount(OmegaTrialStage stage)
        {
            return stage switch
            {
                OmegaTrialStage.SurturActive => 1,
                _ => 0
            };
        }

        private static long GetPhaseTimeRemainingMs(OmegaTrialState state, TimeSpan currentTime)
        {
            if (state == null || state.PhaseExpiresAt <= TimeSpan.Zero || IsTimedStage(state.Stage) == false)
                return 0;

            TimeSpan remaining = state.PhaseExpiresAt - currentTime;
            return remaining > TimeSpan.Zero ? (long)Math.Min(remaining.TotalMilliseconds, int.MaxValue) : 0;
        }

        private static UIWidgetGenericFraction GetTrialGenericFractionWidget(UIDataProvider uiDataProvider, PrototypeId widgetRef, PrototypeId contextRef)
        {
            if (uiDataProvider == null || widgetRef == PrototypeId.Invalid)
                return null;

            if (GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef) is not UIWidgetGenericFractionPrototype)
                return null;

            return uiDataProvider.GetWidget<UIWidgetGenericFraction>(widgetRef, contextRef);
        }

        private static UIWidgetMissionText GetTrialMissionTextWidget(UIDataProvider uiDataProvider, PrototypeId widgetRef, PrototypeId contextRef)
        {
            if (uiDataProvider == null || widgetRef == PrototypeId.Invalid)
                return null;

            if (GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef) is not UIWidgetMissionTextPrototype)
                return null;

            return uiDataProvider.GetWidget<UIWidgetMissionText>(widgetRef, contextRef);
        }

        private static PrototypeId GetOmegaTrialLevelWidgetPrototypeRef()
        {
            PrototypeId widgetRef = GetPrototypeRefByName(OmegaTrialLevelWidgetPrototypeName);
            return widgetRef != PrototypeId.Invalid ? widgetRef : OmegaTrialLevelWidgetPrototypeRef;
        }

        private static PrototypeId GetOmegaTrialQuotaWidgetPrototypeRef()
        {
            PrototypeId widgetRef = GetPrototypeRefByName(OmegaTrialQuotaWidgetPrototypeName);
            return widgetRef != PrototypeId.Invalid ? widgetRef : OmegaTrialQuotaWidgetPrototypeRef;
        }

        private static PrototypeId GetOmegaTrialTimerWidgetPrototypeRef()
        {
            PrototypeId widgetRef = GetPrototypeRefByName(OmegaTrialTimerWidgetPrototypeName);
            return widgetRef != PrototypeId.Invalid ? widgetRef : OmegaTrialTimerWidgetPrototypeRef;
        }

        private static PrototypeId GetOmegaTrialWidgetContextRef()
        {
            return GetSurturTrialRegionRef();
        }

        private static bool TryMoveAvatarToFinalArena(Player player, Region region, OmegaTrialState state)
        {
            Avatar avatar = player?.CurrentAvatar;
            PrototypeId hotspotRef = GetPrototypeRefByName(SurturFinalEncounterHotspotPrototypeName);
            if (avatar == null || avatar.Locomotor == null || region == null || state == null || hotspotRef == PrototypeId.Invalid)
                return false;

            WorldEntity hotspot = FindEntityByPrototype(region, hotspotRef);
            if (hotspot == null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=arena-marker-missing playerDbId=0x{player.DatabaseUniqueId:X} marker={SurturFinalEncounterHotspotPrototypeName} regionId=0x{region.Id:X}");
                return false;
            }

            Vector3 hotspotPosition = hotspot.RegionLocation.Position;
            Orientation hotspotOrientation = hotspot.RegionLocation.Orientation;
            Cell hotspotCell = hotspot.Cell ?? region.GetCellAtPosition(hotspotPosition);
            hotspot.Destroy();
            // Logger.Info($"[OmegaTrialTrace] stage=arena-marker-found playerDbId=0x{player.DatabaseUniqueId:X} markerId=0x{hotspot.Id:X} markerPosition={hotspotPosition}");

            state.FinalEncounterMarkerPosition = hotspotPosition;
            state.HasFinalEncounterMarker = true;

            if (hotspotCell == null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=arena-marker-invalid-cell playerDbId=0x{player.DatabaseUniqueId:X} markerPosition={hotspotPosition}");
                return false;
            }

            Bounds moveBounds = avatar.Bounds;
            moveBounds.Center = hotspotPosition;
            if (region.ChoosePositionAtOrNearPoint(
                ref moveBounds,
                avatar.Locomotor.PathFlags,
                PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                BlockingCheckFlags.None,
                1200f,
                out Vector3 resolvedPosition,
                maxPositionTests: 64) == false)
            {
                return false;
            }

            Cell resolvedCell = region.GetCellAtPosition(resolvedPosition) ?? hotspotCell;
            resolvedPosition = RegionLocation.ProjectToFloor(region, resolvedCell, resolvedPosition);
            resolvedPosition.Z += avatar.Bounds.HalfHeight;
            ChangePositionResult result = avatar.ChangeRegionPosition(resolvedPosition, hotspotOrientation, ChangePositionFlags.Teleport);

            state.ArenaAnchorPosition = resolvedPosition;
            state.ArenaAnchorOrientation = hotspotOrientation;
            state.ArenaAnchorCell = resolvedCell;
            state.HasArenaAnchor = result != ChangePositionResult.InvalidPosition;
            // Logger.Info($"[OmegaTrialTrace] stage=arena-anchor playerDbId=0x{player.DatabaseUniqueId:X} result={result} anchor={state.ArenaAnchorPosition} cell={(state.ArenaAnchorCell?.Id.ToString("X") ?? "unknown")}");
            return state.HasArenaAnchor;
        }

        private static bool ShouldDestroyNativeTrialAgent(WorldEntity entity, OmegaTrialState state)
        {
            if (entity is not Agent agent || state == null)
                return false;

            if (agent is Avatar || agent.IsTeamUpAgent)
                return false;

            if (agent.IsDestroyed || agent.IsDead || agent.IsInWorld == false)
                return false;

            if (agent.Id == state.LokiEntityId || agent.Id == state.SurturEntityId)
                return false;

            if (IsTrialBossPrototype(agent))
                return false;

            if (IsOwnedByTrialBoss(agent, state))
                return false;

            return agent.IsHostileToPlayers();
        }

        private static bool IsOwnedByTrialBoss(WorldEntity entity, OmegaTrialState state)
        {
            if (entity == null || state == null || entity.HasPowerUserOverride == false)
                return false;

            ulong powerUserOverrideId = entity.PowerUserOverrideId;
            return powerUserOverrideId != Entity.InvalidId &&
                   (powerUserOverrideId == state.LokiEntityId || powerUserOverrideId == state.SurturEntityId);
        }

        private static bool IsTrialBossPrototype(WorldEntity entity)
        {
            if (entity == null)
                return false;

            PrototypeId lokiPhase1Ref = GetPrototypeRefByName(LokiPhase1PrototypeName);
            if (lokiPhase1Ref != PrototypeId.Invalid && entity.PrototypeDataRef == lokiPhase1Ref)
                return true;

            PrototypeId lokiPhase2Ref = GetPrototypeRefByName(LokiPhase2PrototypeName);
            if (lokiPhase2Ref != PrototypeId.Invalid && entity.PrototypeDataRef == lokiPhase2Ref)
                return true;

            PrototypeId surturRef = GetPrototypeRefByName(SurturBossPrototypeName);
            return surturRef != PrototypeId.Invalid && entity.PrototypeDataRef == surturRef;
        }

        private static OmegaTrialState GetActiveStateForRegion(ulong regionId)
        {
            lock (SyncRoot)
            {
                foreach (OmegaTrialState state in TrialStates.Values)
                {
                    if (state.RegionId == regionId)
                        return state;
                }
            }

            return null;
        }

        private static WorldEntity FindEntityByPrototype(Region region, PrototypeId prototypeRef)
        {
            if (region == null || prototypeRef == PrototypeId.Invalid)
                return null;

            foreach (Entity entity in region.Entities)
            {
                if (entity is WorldEntity worldEntity && worldEntity.PrototypeDataRef == prototypeRef && worldEntity.IsDestroyed == false)
                    return worldEntity;
            }

            return null;
        }

        private static bool TrySpawnBossForTrial(Player player, Region region, OmegaTrialState state, PrototypeId bossRef, float spawnDistance, out Agent boss)
        {
            boss = null;

            Avatar avatar = player?.CurrentAvatar;
            AgentPrototype bossProto = bossRef.As<AgentPrototype>();
            if (avatar == null || region == null || state == null || bossProto == null)
                return false;

            if (TryResolveBossSpawnLocation(avatar, region, state, bossProto, spawnDistance, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell) == false)
                return false;

            return TryCreateTrialBoss(region, bossProto, spawnPosition, spawnOrientation, spawnCell, out boss);
        }

        private static bool TrySpawnBossAtNativeSpawner(Player player, Region region, OmegaTrialState state, PrototypeId bossRef, string spawnerPrototypeName, out Agent boss)
        {
            boss = null;

            AgentPrototype bossProto = bossRef.As<AgentPrototype>();
            if (player == null || region == null || state == null || bossProto == null || string.IsNullOrWhiteSpace(spawnerPrototypeName))
                return false;

            PrototypeId spawnerRef = GetPrototypeRefByName(spawnerPrototypeName);
            if (spawnerRef == PrototypeId.Invalid)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=native-spawner-missing playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} spawner={spawnerPrototypeName} reason=prototype-invalid");
                return false;
            }

            WorldEntity spawner = FindEntityByPrototype(region, spawnerRef);
            if (spawner == null || spawner.IsInWorld == false)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=native-spawner-missing playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} spawner={spawnerRef.GetNameFormatted()} reason=entity-not-found");
                return false;
            }

            Cell spawnCell = spawner.Cell ?? region.GetCellAtPosition(spawner.RegionLocation.Position);
            if (spawnCell == null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=native-spawner-missing playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} spawner={spawnerRef.GetNameFormatted()} reason=no-cell position={spawner.RegionLocation.Position}");
                return false;
            }

            Vector3 spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawner.RegionLocation.Position);
            if (bossProto.Bounds != null)
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();

            // Logger.Info($"[OmegaTrialTrace] stage=native-spawner-found playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} spawner={spawner} position={spawnPosition} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
            return TryCreateTrialBoss(region, bossProto, spawnPosition, spawner.RegionLocation.Orientation, spawnCell, out boss);
        }

        private static bool TrySpawnSurturAtNativeFinalEncounterMarker(Player player, Region region, OmegaTrialState state, PrototypeId bossRef, out Agent boss)
        {
            boss = null;

            AgentPrototype bossProto = bossRef.As<AgentPrototype>();
            if (player == null || region == null || state == null || bossProto == null || state.HasFinalEncounterMarker == false)
                return false;

            Vector3 markerOffset = SurturFinalBossEncounterExportPosition - SurturFinalEncounterHotspotExportPosition;
            Vector3 markerPosition = state.FinalEncounterMarkerPosition + markerOffset;
            Cell spawnCell = region.GetCellAtPosition(markerPosition) ?? state.ArenaAnchorCell;
            if (spawnCell == null)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=native-final-marker-missing playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} reason=no-cell markerPosition={markerPosition}");
                return false;
            }

            Vector3 spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, markerPosition);
            if (bossProto.Bounds != null)
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();

            Vector3 facingTarget = state.HasArenaAnchor ? state.ArenaAnchorPosition : state.FinalEncounterMarkerPosition;
            Orientation orientation = Orientation.FromDeltaVector2D(facingTarget - spawnPosition);
            // Logger.Info($"[OmegaTrialTrace] stage=native-final-marker playerDbId=0x{state.PlayerDbId:X} boss={bossProto.DataRef.GetNameFormatted()} markerPosition={markerPosition} resolved={spawnPosition} facingTarget={facingTarget} orientation={orientation} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
            return TryCreateTrialBoss(region, bossProto, spawnPosition, orientation, spawnCell, out boss);
        }

        private static bool TryCreateTrialBoss(Region region, AgentPrototype bossProto, Vector3 spawnPosition, Orientation spawnOrientation, Cell spawnCell, out Agent boss)
        {
            boss = null;
            if (region == null || bossProto == null || spawnCell == null)
                return false;

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = bossProto.DataRef;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.RegionId = region.Id;
            settings.Cell = spawnCell;
            settings.IsPopulation = true;

            using var settingsPropertiesHandle = PropertyCollectionPool.Get(out PropertyCollection settingsProperties);
            int level = spawnCell.Area.GetCharacterLevel(bossProto);
            settingsProperties[PropertyEnum.CharacterLevel] = level;
            settingsProperties[PropertyEnum.CombatLevel] = level;
            settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
            settingsProperties[PropertyEnum.Rank] = bossProto.Rank?.DataRef ?? PrototypeId.Invalid;
            settingsProperties[PropertyEnum.NoLootDrop] = true;
            settingsProperties[PropertyEnum.MissionXEncounterHostilityOk] = true;
            settings.Properties = settingsProperties;

            boss = region.Game.EntityManager.CreateEntity(settings) as Agent;
            if (boss == null)
                return false;

            if (bossProto.ModifiersGuaranteed.HasValue())
                foreach (PrototypeId boost in bossProto.ModifiersGuaranteed)
                    boss.Properties[PropertyEnum.EnemyBoost, boost] = true;

            return true;
        }

        private static bool TryResolveBossSpawnLocation(Avatar avatar, Region region, OmegaTrialState state, AgentPrototype bossProto, float spawnDistance, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell)
        {
            spawnPosition = Vector3.Zero;
            spawnOrientation = state.HasArenaAnchor ? state.ArenaAnchorOrientation : avatar.RegionLocation.Orientation;
            spawnCell = null;

            Vector3 anchorPosition = state.HasArenaAnchor ? state.ArenaAnchorPosition : avatar.RegionLocation.Position;
            Cell anchorCell = state.HasArenaAnchor ? state.ArenaAnchorCell : avatar.Cell ?? region.GetCellAtPosition(anchorPosition);
            if (anchorCell == null)
            {
                Player owner = avatar.GetOwnerOfType<Player>();
                string playerDbIdText = owner != null ? $"0x{owner.DatabaseUniqueId:X}" : "unknown";
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-location-failed playerDbId={playerDbIdText} boss={bossProto.DataRef.GetNameFormatted()} reason=no-anchor-cell anchor={anchorPosition}");
                return false;
            }

            if (bossProto.Bounds == null)
            {
                spawnPosition = anchorPosition;
                spawnCell = anchorCell;
                return true;
            }

            PathFlags pathFlags = Region.GetPathFlagsForEntity(bossProto);
            Vector3 forward = Vector3.SafeNormalize2D(avatar.Forward, Vector3.XAxis);
            Vector3 right = Vector3.Perp2D(forward);
            Vector3[] directions =
            {
                forward,
                -forward,
                right,
                -right,
                Vector3.SafeNormalize2D(forward + right, forward),
                Vector3.SafeNormalize2D(forward - right, forward),
                Vector3.SafeNormalize2D(-forward + right, -forward),
                Vector3.SafeNormalize2D(-forward - right, -forward)
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 preferredPosition = anchorPosition + (directions[i] * spawnDistance);
                if (TryChooseBossSpawnPosition(region, bossProto, preferredPosition, pathFlags, out spawnPosition, out spawnCell))
                {
                    spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawnPosition);
                    spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();
                    // Logger.Info($"[OmegaTrialTrace] stage=spawn-location-resolved boss={bossProto.DataRef.GetNameFormatted()} anchor={anchorPosition} preferred={preferredPosition} resolved={spawnPosition} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
                    return true;
                }
            }

            if (TryChooseBossSpawnPosition(region, bossProto, anchorPosition, pathFlags, out spawnPosition, out spawnCell))
            {
                spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawnPosition);
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-location-resolved boss={bossProto.DataRef.GetNameFormatted()} anchor={anchorPosition} preferred={anchorPosition} resolved={spawnPosition} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
                return true;
            }

            if (state.HasArenaAnchor)
            {
                spawnPosition = RegionLocation.ProjectToFloor(region, anchorCell, anchorPosition);
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();
                spawnCell = anchorCell;
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-location-forced boss={bossProto.DataRef.GetNameFormatted()} anchor={anchorPosition} resolved={spawnPosition} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
                return true;
            }

            // Logger.Info($"[OmegaTrialTrace] stage=spawn-location-failed boss={bossProto.DataRef.GetNameFormatted()} reason=no-valid-position anchor={anchorPosition} spawnDistance={spawnDistance}");
            return false;
        }

        private static bool TryChooseBossSpawnPosition(Region region, AgentPrototype bossProto, Vector3 preferredPosition, PathFlags pathFlags, out Vector3 spawnPosition, out Cell spawnCell)
        {
            spawnPosition = Vector3.Zero;
            spawnCell = null;

            Bounds spawnBounds = new(bossProto.Bounds, preferredPosition);
            if (region.ChoosePositionAtOrNearPoint(
                ref spawnBounds,
                pathFlags,
                PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                BlockingCheckFlags.None,
                BossSpawnSearchDistance,
                out Vector3 resolvedPosition,
                maxPositionTests: 64) == false)
            {
                return false;
            }

            Cell resolvedCell = region.GetCellAtPosition(resolvedPosition);
            if (resolvedCell == null)
                return false;

            spawnPosition = resolvedPosition;
            spawnCell = resolvedCell;
            return true;
        }

        private static bool IsTrackedDeath(WorldEntity defender, ulong entityId, string fallbackPrototypeName)
        {
            if (defender == null)
                return false;

            if (entityId != Entity.InvalidId && defender.Id == entityId)
                return true;

            PrototypeId fallbackRef = GetPrototypeRefByName(fallbackPrototypeName);
            return fallbackRef != PrototypeId.Invalid && defender.PrototypeDataRef == fallbackRef;
        }

        private static PrototypeId GetSurturTrialRegionRef()
        {
            RegionConnectionTargetPrototype targetProto = GetPrototypeRefByName(SurturTrialEntryTargetPrototypeName).As<RegionConnectionTargetPrototype>();
            return targetProto?.Region ?? PrototypeId.Invalid;
        }

        private static PrototypeId GetPrototypeRefByName(string prototypeName)
        {
            return GameDatabase.GetPrototypeRefByName(prototypeName);
        }

        private static void ApplyTrialRegionScaling(OmegaTrialState state, Region region)
        {
            if (state.RegionScalingApplied || region == null)
                return;

            state.RegionPlayerToMobDamageBeforeScaling = region.Properties[PropertyEnum.DamageRegionPlayerToMob];
            state.RegionMobToPlayerDamageBeforeScaling = region.Properties[PropertyEnum.DamageRegionMobToPlayer];
            state.RegionScalingApplied = true;

            region.Properties[PropertyEnum.DamageRegionPlayerToMob] = state.RegionPlayerToMobDamageBeforeScaling * TrialPlayerToMobDamageMultiplier;
            region.Properties[PropertyEnum.DamageRegionMobToPlayer] = state.RegionMobToPlayerDamageBeforeScaling * TrialMobToPlayerDamageMultiplier;
        }

        private static void RestoreTrialRegionScaling(OmegaTrialState state)
        {
            if (state == null || state.RegionScalingApplied == false || state.RegionId == 0)
                return;

            lock (SyncRoot)
            {
                foreach (OmegaTrialState activeState in TrialStates.Values)
                {
                    if (activeState.PlayerDbId == state.PlayerDbId)
                        continue;

                    if (activeState.RegionId == state.RegionId)
                        return;
                }
            }

            Region region = state.CachedRegion;
            if (region == null)
                return;

            region.Properties[PropertyEnum.DamageRegionPlayerToMob] = state.RegionPlayerToMobDamageBeforeScaling;
            region.Properties[PropertyEnum.DamageRegionMobToPlayer] = state.RegionMobToPlayerDamageBeforeScaling;
            // Logger.Info($"[OmegaTrialTrace] stage=scaling-restored playerDbId=0x{state.PlayerDbId:X} regionId=0x{state.RegionId:X} playerToMob={state.RegionPlayerToMobDamageBeforeScaling} mobToPlayer={state.RegionMobToPlayerDamageBeforeScaling}");
        }

        private static void EndTrial(ulong playerDbId, bool clearWidgets = true)
        {
            OmegaTrialState state;
            lock (SyncRoot)
            {
                if (TrialStates.TryGetValue(playerDbId, out state) == false)
                    return;

                TrialStates.Remove(playerDbId);
            }

            RestoreTrialRegionScaling(state);
            if (clearWidgets)
                ClearTrialWidgets(state.CachedRegion?.UIDataProvider);
            TryRemoveRegionListeners(state.RegionId, state.CachedRegion);
        }

        private static void TryRemoveRegionListeners(ulong regionId, Region region)
        {
            if (regionId == 0)
                return;

            Event<EntityDeadGameEvent>.Action deadAction;
            lock (SyncRoot)
            {
                foreach (OmegaTrialState state in TrialStates.Values)
                {
                    if (state.RegionId == regionId)
                        return;
                }

                RegionEntityDeadActions.TryGetValue(regionId, out deadAction);
                RegionEntityDeadActions.Remove(regionId);
            }

            if (deadAction != null)
                region?.EntityDeadEvent.RemoveAction(deadAction);
        }

        private sealed class OmegaTrialState
        {
            public ulong PlayerDbId;
            public OmegaTrialStage Stage;
            public ulong RegionId;
            public ulong LokiEntityId;
            public ulong SurturEntityId;
            public ulong TimerMetaGameId;
            public ulong ReturnPortalEntityId;
            public TimeSpan PhaseStartedAt;
            public TimeSpan PhaseExpiresAt;
            public bool PostEntryNativeSuppressionPending;
            public TimeSpan PostEntryNativeSuppressionAt;
            public bool HasArenaAnchor;
            public Vector3 ArenaAnchorPosition;
            public Orientation ArenaAnchorOrientation;
            public Cell ArenaAnchorCell;
            public bool HasFinalEncounterMarker;
            public Vector3 FinalEncounterMarkerPosition;
            public bool RegionScalingApplied;
            public float RegionPlayerToMobDamageBeforeScaling;
            public float RegionMobToPlayerDamageBeforeScaling;
            public Region CachedRegion;
        }
    }
}
