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
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.MythicRifts
{
    public static class OmegaTrialService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private const string SilverSableOmegaTrialGuidePrototypeName = "Entity/Characters/NPCs/SilverSableHelicarrier.prototype";
        private const string SurturTrialEntryTargetPrototypeName = "Regions/RAIDS/MuspelheimRaid/ConnectionNodes/SurturRaidReduxEntryTargetBase.prototype";
        private const string SurturFinalEncounterHotspotPrototypeName = "Entity/Missions/Hotspots/SurturRaidFinalEncTriggerHotspot.prototype";
        private const string LokiPhase1PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase1.prototype";
        private const string LokiPhase2PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase2.prototype";
        private const string SurturBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/SurturBoss.prototype";
        private const string SurturBossSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/SurturBossSpawner.prototype";
        private const string SurturBanishPortalVisualSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/BanishPortals/SurturRaidBanishPortalVisualSpawner.prototype";
        private const string SurturBanishPortalSlagSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/BanishPortals/SurturRaidBanishPortalSlagSpawner.prototype";
        private const string SurturBanishPortalTwinsSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/BanishPortals/SurturRaidBanishPortalTwinsSpawner.prototype";
        private const string SurturBanishPortalMoMSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/BanishPortals/SurturRaidBanishPortalMoMSpawner.prototype";
        private const string SurturBanishSlagSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/Minibosses/SurturFightSlagSpawner.prototype";
        private const string SurturBanishHellfireSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/Minibosses/SurturFightHellfireSpawner.prototype";
        private const string SurturBanishBrimstoneSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/Minibosses/SurturFightBrimstoneSpawner.prototype";
        private const string SurturBanishMistressSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/FiveMan/Minibosses/SurturFightMistressSpawner.prototype";
        private const string SurturReturnFromSlagSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/SurturRaidReturnFromSlagSpawner.prototype";
        private const string SurturReturnFromTwinsSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/SurturRaidReturnFromTwinsSpawner.prototype";
        private const string SurturReturnFromMistressSpawnerPrototypeName = "Entity/Spawners/Raids/Surtur/SurturBossFight/SurturRaidReturnFromMistrSpawner.prototype";
        private const string SurturSlagMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/SlagMiniBoss.prototype";
        private const string SurturHellfireMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/HellfireMiniBoss.prototype";
        private const string SurturBrimstoneMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/BrimstoneMiniBoss.prototype";
        private const string SurturMistressMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/MistressOfMagmaMiniBoss.prototype";
        private const string SurturCoverRockAPrototypeName = "Entity/Props/MultiStageDestructibles/DBMuspelheimSurturBlocker.prototype";
        private const string SurturCoverRockBPrototypeName = "Entity/Props/MultiStageDestructibles/DBMuspelheimSurturBlockerB.prototype";
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
        private const float SurturBanishArenaMaxSpawnDistance = 1250f;
        private const long SurturTargetHealth = 6000000L;
        private const long MistressOfMagmaTargetHealth = 1500000L;
        private static readonly TimeSpan LokiPhase1SpawnDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan LokiFightTimeLimit = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SurturFightTimeLimit = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SurturHealthThresholdCheckInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan PostEntryNativeSuppressionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan TrialReturnPortalLifespan = TimeSpan.FromMinutes(10);
        private static readonly Vector3 SurturFinalEncounterHotspotExportPosition = new(11024f, 13072f, 176f);
        private static readonly Vector3 SurturFinalBossEncounterExportPosition = new(12193.019f, 14205.562f, 255.99998f);
        private static readonly float[] SurturBanishHealthThresholds = { 75f, 50f, 25f };
        private static readonly (string PrototypeName, Vector3 Position, Orientation Orientation)[] SurturCoverRockPlacements =
        {
            (SurturCoverRockBPrototypeName, new Vector3(30128f, 12592f, 176f), new Orientation(-0.490881f, 0f, 0f)),
            (SurturCoverRockAPrototypeName, new Vector3(30360f, 13336f, 176f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockAPrototypeName, new Vector3(31320f, 13832f, 184f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(31040f, 12416f, 176f), new Orientation(1.178115f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(30624f, 13808f, 176f), new Orientation(0.981763f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(31008f, 14368f, 176f), new Orientation(1.472644f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(30384f, 14288f, 176f), new Orientation(0.490881f, 0f, 0f))
        };
        private static readonly object SyncRoot = new();
        private static readonly object PrototypeCacheLock = new();
        private static readonly Dictionary<string, PrototypeId> PrototypeRefCache = new();
        private static readonly Dictionary<ulong, OmegaTrialState> TrialStates = new();
        private static readonly Dictionary<ulong, OmegaTrialState> RegionTrialStates = new();
        private static readonly Dictionary<ulong, Event<EntityDeadGameEvent>.Action> RegionEntityDeadActions = new();

        private enum OmegaTrialStage
        {
            Traveling,
            LokiPhase1Active,
            LokiActive,
            SurturActive
        }

        public static bool TryLogPlayerDamage(WorldEntity target, PowerResults powerResults, WorldEntity ultimateOwner, WorldEntity powerUser, long startHealth, long endHealth, long adjustHealth)
        {
            if (adjustHealth >= 0 || powerResults == null || target is not Avatar targetAvatar)
                return false;

            Player player = targetAvatar.GetOwnerOfType<Player>();
            if (player == null)
                return false;

            OmegaTrialState state;
            lock (SyncRoot)
            {
                if (TrialStates.TryGetValue(player.DatabaseUniqueId, out state) == false)
                    return false;
            }

            Region region = target.Region;
            if (state == null || region == null || state.RegionId != region.Id)
                return false;

            float rawPhysical = powerResults.Properties[PropertyEnum.Damage, DamageType.Physical];
            float rawEnergy = powerResults.Properties[PropertyEnum.Damage, DamageType.Energy];
            float rawMental = powerResults.Properties[PropertyEnum.Damage, DamageType.Mental];
            float rawTotal = rawPhysical + rawEnergy + rawMental;
            float clientPhysical = powerResults.GetDamageForClient(DamageType.Physical);
            float clientEnergy = powerResults.GetDamageForClient(DamageType.Energy);
            float clientMental = powerResults.GetDamageForClient(DamageType.Mental);
            float clientTotal = clientPhysical + clientEnergy + clientMental;

            string powerName = powerResults.PowerPrototype?.DataRef.GetNameFormatted() ?? "unknown";
            string ultimateOwnerName = ultimateOwner?.PrototypeDataRef.GetNameFormatted() ?? "unknown";
            string powerUserName = powerUser?.PrototypeDataRef.GetNameFormatted() ?? "unknown";
            Vector3 ownerPosition = ultimateOwner?.RegionLocation.Position ?? Vector3.Zero;
            Vector3 targetPosition = target.RegionLocation.Position;
            float ownerDistance = ultimateOwner != null ? Vector3.Distance2D(ownerPosition, targetPosition) : -1f;

            Logger.Info(
                $"[OmegaTrialDamageTrace] playerDbId=0x{player.DatabaseUniqueId:X} stage={state.Stage} " +
                $"target={target.PrototypeDataRef.GetNameFormatted()} targetId=0x{target.Id:X} targetPos={targetPosition} " +
                $"source={ultimateOwnerName} sourceId=0x{ultimateOwner?.Id ?? 0UL:X} sourcePos={ownerPosition} sourceDistance={ownerDistance} " +
                $"powerUser={powerUserName} powerUserId=0x{powerUser?.Id ?? 0UL:X} power={powerName} " +
                $"health={startHealth}->{endHealth} delta={adjustHealth} rawDamage={rawTotal} rawPhysical={rawPhysical} rawEnergy={rawEnergy} rawMental={rawMental} " +
                $"clientDamage={clientTotal} clientPhysical={clientPhysical} clientEnergy={clientEnergy} clientMental={clientMental} " +
                $"flags={powerResults.Flags} hostile={powerResults.TestFlag(PowerResultFlags.Hostile)} critical={powerResults.TestFlag(PowerResultFlags.Critical)} " +
                $"superCritical={powerResults.TestFlag(PowerResultFlags.SuperCritical)} blocked={powerResults.TestFlag(PowerResultFlags.Blocked)} " +
                $"dodged={powerResults.TestFlag(PowerResultFlags.Dodged)} resisted={powerResults.TestFlag(PowerResultFlags.Resisted)} " +
                $"unaffected={powerResults.TestFlag(PowerResultFlags.Unaffected)} overTime={powerResults.TestFlag(PowerResultFlags.OverTime)} " +
                $"instantKill={powerResults.TestFlag(PowerResultFlags.InstantKill)}");

            return true;
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

        public static bool TryUseOmegaTrialPortal(Player player, Transition transition)
        {
            if (player?.CurrentAvatar == null || transition == null)
                return false;

            OmegaTrialState state;
            lock (SyncRoot)
            {
                if (TrialStates.TryGetValue(player.DatabaseUniqueId, out state) == false)
                    return false;
            }

            if (state == null || state.RegionId == 0 || player.CurrentAvatar.Region?.Id != state.RegionId)
                return false;

            if (transition.Id == state.BanishPortalEntityId)
                return TryUseOmegaTrialPortal(player, transition, state, state.BanishPortalDestination, state.BanishPortalDestinationOrientation, "banish");

            if (transition.Id == state.BanishReturnPortalEntityId)
            {
                Region region = player.CurrentAvatar.Region;
                bool handled = TryUseOmegaTrialPortal(player, transition, state, state.BanishReturnPortalDestination, state.BanishReturnPortalDestinationOrientation, "banish-return");
                if (handled)
                    ResumeSurturAfterBanish(player, region, state);

                return handled;
            }

            return false;
        }

        private static bool TryUseOmegaTrialPortal(Player player, Transition transition, OmegaTrialState state, Vector3 destination, Orientation orientation, string portalType)
        {
            Region region = player.CurrentAvatar.Region;
            if (region == null)
                return false;

            Cell destinationCell = region.GetCellAtPosition(destination);
            if (destinationCell == null)
            {
                Logger.Info($"[OmegaTrialTrace] stage=portal-use-failed playerDbId=0x{state.PlayerDbId:X} portalType={portalType} portalId=0x{transition.Id:X} reason=no-cell destination={destination}");
                return true;
            }

            Vector3 resolvedPosition = RegionLocation.ProjectToFloor(region, destinationCell, destination);
            resolvedPosition.Z = destination.Z;
            ChangePositionResult result = player.CurrentAvatar.ChangeRegionPosition(resolvedPosition, orientation, ChangePositionFlags.Teleport);
            Logger.Info($"[OmegaTrialTrace] stage=portal-used playerDbId=0x{state.PlayerDbId:X} portalType={portalType} portalId=0x{transition.Id:X} result={result} destination={resolvedPosition}");

            if (result != ChangePositionResult.InvalidPosition)
                transition.Destroy();

            return true;
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
            RegisterRegionTrialState(region, state);
            SuppressNativeTrialContent(player, region, state);
            SchedulePostEntryNativeSuppression(player, state);
            bool movedToArena = TryMoveAvatarToFinalArena(player, region, state);
            // Logger.Info($"[OmegaTrialTrace] stage=arena-move playerDbId=0x{player.DatabaseUniqueId:X} regionId=0x{region.Id:X} moved={movedToArena} avatarPosition={player.CurrentAvatar?.RegionLocation.Position}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);

            state.Stage = OmegaTrialStage.LokiPhase1Active;
            ScheduleLokiPhase1Spawn(player, state);
            SendTrialChat(player, "Loki approaches. Prepare yourself.");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static void ScheduleLokiPhase1Spawn(Player player, OmegaTrialState state)
        {
            if (player?.Game == null || state == null)
                return;

            player.Game.GameEventScheduler.CancelEvent(state.LokiPhase1SpawnEvent);
            player.Game.GameEventScheduler.ScheduleEvent(state.LokiPhase1SpawnEvent, LokiPhase1SpawnDelay);

            OmegaTrialLokiPhase1SpawnEvent spawnEvent = state.LokiPhase1SpawnEvent.Get();
            spawnEvent.PlayerDbId = state.PlayerDbId;
        }

        private static void HandleLokiPhase1Spawn(ulong playerDbId)
        {
            OmegaTrialState state;
            lock (SyncRoot)
            {
                TrialStates.TryGetValue(playerDbId, out state);
            }

            if (state == null || state.Stage != OmegaTrialStage.LokiPhase1Active || state.LokiEntityId != Entity.InvalidId)
                return;

            Region region = state.CachedRegion;
            Player player = region?.Game?.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (player?.CurrentAvatar == null || region == null || region.ShutdownRequested)
                return;

            if (TrySpawnBossForTrial(player, region, state, GetPrototypeRefByName(LokiPhase1PrototypeName), LokiSpawnDistance, out Agent loki) == false)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=spawn-failed playerDbId=0x{player.DatabaseUniqueId:X} boss={LokiPhase1PrototypeName} regionId=0x{region.Id:X}");
                TrySpawnReturnPortal(player, region, state);
                EndTrial(player.DatabaseUniqueId);
                return;
            }

            state.LokiEntityId = loki.Id;
            StartPhaseTimer(player, state, LokiFightTimeLimit);
            SendTrialChat(player, "Defeat Loki. Time limit: 5 minutes.");
            Logger.Info($"[OmegaTrialTrace] stage=spawned-loki-phase1 playerDbId=0x{player.DatabaseUniqueId:X} entityId=0x{loki.Id:X} position={loki.RegionLocation.Position} level={loki.Properties[PropertyEnum.CharacterLevel]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static bool IsSilverSableOmegaTrialGuide(WorldEntity interactableObject)
        {
            PrototypeId silverSableRef = GetPrototypeRefByName(SilverSableOmegaTrialGuidePrototypeName);
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

        private static void RegisterRegionTrialState(Region region, OmegaTrialState state)
        {
            if (region == null || state == null)
                return;

            Event<EntityDeadGameEvent>.Action deadAction = (in EntityDeadGameEvent evt) => OnRegionEntityDead(region.Game, region.Id, evt);
            lock (SyncRoot)
            {
                RegionTrialStates[region.Id] = state;

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

            OmegaTrialState state;
            lock (SyncRoot)
            {
                RegionTrialStates.TryGetValue(regionId, out state);
            }

            if (state == null)
                return;

            Player player = game.EntityManager.GetEntityByDbGuid<Player>(state.PlayerDbId);
            if (player?.CurrentAvatar == null || player.CurrentAvatar.Region?.Id != regionId)
            {
                // Logger.Info($"[OmegaTrialTrace] stage=ending-missing-player playerDbId=0x{state.PlayerDbId:X} regionId=0x{regionId:X}");
                EndTrial(state.PlayerDbId);
                return;
            }

            if (state.Stage == OmegaTrialStage.LokiPhase1Active && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase1PrototypeName))
            {
                // Logger.Info($"[OmegaTrialTrace] stage=loki-phase1-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                SpawnLokiPhase2(player, player.CurrentAvatar.Region, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.LokiActive && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase2PrototypeName))
            {
                // Logger.Info($"[OmegaTrialTrace] stage=loki-phase2-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                SpawnSurturPhase(player, player.CurrentAvatar.Region, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.SurturActive && IsTrackedDeath(evt.Defender, state.SurturEntityId, SurturBossPrototypeName))
            {
                Logger.Info($"[OmegaTrialTrace] stage=surtur-dead playerDbId=0x{state.PlayerDbId:X} entityId=0x{evt.Defender.Id:X} trackedEntityId=0x{state.SurturEntityId:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()}");
                CompleteTrial(player, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.SurturActive)
            {
                bool handledBanishDeath = TryHandleSurturBanishMiniBossDeath(player, player.CurrentAvatar.Region, state, evt.Defender);
                if (handledBanishDeath == false && IsTrialBossPrototype(evt.Defender))
                    Logger.Info($"[OmegaTrialTrace] stage=unhandled-trial-boss-death playerDbId=0x{state.PlayerDbId:X} defenderId=0x{evt.Defender.Id:X} trackedSurturId=0x{state.SurturEntityId:X} prototype={evt.Defender.PrototypeDataRef.GetNameFormatted()} isDead={evt.Defender.IsDead} isDestroyed={evt.Defender.IsDestroyed}");
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
            Logger.Info($"[OmegaTrialTrace] stage=spawned-loki-phase2 playerDbId=0x{state.PlayerDbId:X} entityId=0x{loki.Id:X} position={loki.RegionLocation.Position} level={(int)loki.Properties[PropertyEnum.CharacterLevel]} health={(long)loki.Properties[PropertyEnum.Health]} healthMax={(long)loki.Properties[PropertyEnum.HealthMax]} healthMaxOther={(long)loki.Properties[PropertyEnum.HealthMaxOther]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
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
            ResetSurturBanishState(state);
            ApplySurturHealthMultiplier(state, surtur);
            EnsureSurturCover(region, state);
            StartPhaseTimer(player, state, SurturFightTimeLimit);
            ScheduleSurturHealthThresholdCheck(player, state);
            SendTrialChat(player, "Defeat Surtur. Time limit: 5 minutes.");
            Logger.Info($"[OmegaTrialTrace] stage=spawned-surtur playerDbId=0x{state.PlayerDbId:X} entityId=0x{surtur.Id:X} prototype={surtur.PrototypeDataRef.GetNameFormatted()} position={surtur.RegionLocation.Position} level={(int)surtur.Properties[PropertyEnum.CharacterLevel]} health={(long)surtur.Properties[PropertyEnum.Health]} healthMax={(long)surtur.Properties[PropertyEnum.HealthMax]} healthMaxOther={(long)surtur.Properties[PropertyEnum.HealthMaxOther]} difficulty={region.DifficultyTierRef.GetNameFormatted()}");
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static void CompleteTrial(Player player, OmegaTrialState state)
        {
            bool grantedAccess = RiftAccessTeleportService.GrantOmegaPatrolAccessForCurrentAvatar(player);
            Logger.Info($"[OmegaTrialTrace] stage=complete playerDbId=0x{state.PlayerDbId:X} grantedOmegaAccess={grantedAccess}");
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

            player.Game.GameEventScheduler.CancelEvent(state.PostEntryNativeSuppressionEvent);
            player.Game.GameEventScheduler.ScheduleEvent(state.PostEntryNativeSuppressionEvent, PostEntryNativeSuppressionDelay);
            state.PostEntryNativeSuppressionEvent.Get().PlayerDbId = state.PlayerDbId;
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
            ReschedulePhaseTimeout(player, state, duration);
            SendStartTrialTimer(player, state, duration);
        }

        private static void ReschedulePhaseTimeout(Player player, OmegaTrialState state, TimeSpan duration)
        {
            if (player?.Game == null || state == null || duration <= TimeSpan.Zero || IsTimedStage(state.Stage) == false)
                return;

            player.Game.GameEventScheduler.CancelEvent(state.PhaseTimeoutEvent);
            player.Game.GameEventScheduler.ScheduleEvent(state.PhaseTimeoutEvent, duration);

            OmegaTrialPhaseTimeoutEvent timeoutEvent = state.PhaseTimeoutEvent.Get();
            timeoutEvent.PlayerDbId = state.PlayerDbId;
            timeoutEvent.Stage = state.Stage;
        }

        private static void HandlePostEntryNativeSuppression(ulong playerDbId)
        {
            OmegaTrialState state;
            lock (SyncRoot)
            {
                TrialStates.TryGetValue(playerDbId, out state);
            }

            Region region = state?.CachedRegion;
            Player player = region?.Game?.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (player?.CurrentAvatar == null || region == null || region.ShutdownRequested)
                return;

            SuppressNativeTrialContent(player, region, state);
        }

        private static void HandlePhaseTimeout(ulong playerDbId, OmegaTrialStage stage)
        {
            OmegaTrialState state;
            lock (SyncRoot)
            {
                TrialStates.TryGetValue(playerDbId, out state);
            }

            if (state == null || IsTimedStage(state.Stage) == false)
                return;

            if (state.Stage != stage && (stage != OmegaTrialStage.LokiPhase1Active || state.Stage != OmegaTrialStage.LokiActive))
                return;

            Region region = state.CachedRegion;
            Player player = region?.Game?.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (player?.CurrentAvatar == null || region == null || region.ShutdownRequested)
                return;

            // Logger.Info($"[OmegaTrialTrace] stage=phase-timeout playerDbId=0x{state.PlayerDbId:X} trialStage={state.Stage} regionId=0x{region.Id:X}");
            SendStopTrialTimer(player, state);
            TrySpawnReturnPortal(player, region, state);
            EndTrial(state.PlayerDbId);
        }

        private static void ResetSurturBanishState(OmegaTrialState state)
        {
            if (state == null)
                return;

            state.NextSurturBanishWaveIndex = 0;
            state.SurturSlagDefeated = false;
            state.SurturHellfireDefeated = false;
            state.SurturBrimstoneDefeated = false;
            state.SurturMistressDefeated = false;
            state.SurturSlagReturnTriggered = false;
            state.SurturTwinsReturnTriggered = false;
            state.SurturMistressReturnTriggered = false;
            state.SurturBanishInProgress = false;
            state.SurturWasInvulnerableBeforeBanish = false;
        }

        private static void ScheduleSurturHealthThresholdCheck(Player player, OmegaTrialState state)
        {
            if (player?.Game == null || state == null || state.Stage != OmegaTrialStage.SurturActive)
                return;

            if (state.SurturBanishInProgress || state.NextSurturBanishWaveIndex < 0 || state.NextSurturBanishWaveIndex >= SurturBanishHealthThresholds.Length)
                return;

            player.Game.GameEventScheduler.CancelEvent(state.SurturBanishWaveEvent);
            player.Game.GameEventScheduler.ScheduleEvent(state.SurturBanishWaveEvent, SurturHealthThresholdCheckInterval);

            OmegaTrialSurturBanishWaveEvent banishEvent = state.SurturBanishWaveEvent.Get();
            banishEvent.PlayerDbId = state.PlayerDbId;
            banishEvent.WaveIndex = state.NextSurturBanishWaveIndex;
        }

        private static void HandleSurturHealthThresholdCheck(ulong playerDbId, int waveIndex)
        {
            OmegaTrialState state;
            lock (SyncRoot)
            {
                TrialStates.TryGetValue(playerDbId, out state);
            }

            if (state == null || state.Stage != OmegaTrialStage.SurturActive || state.NextSurturBanishWaveIndex != waveIndex)
                return;

            Region region = state.CachedRegion;
            Player player = region?.Game?.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (player?.CurrentAvatar == null || region == null || region.ShutdownRequested)
                return;

            Agent surtur = region.Game.EntityManager.GetEntity<Agent>(state.SurturEntityId);
            if (surtur == null || surtur.IsDestroyed || surtur.IsDead)
                return;

            if (state.SurturBanishInProgress)
                return;

            float healthPct = GetHealthPct(surtur);
            float thresholdPct = SurturBanishHealthThresholds[waveIndex];
            if (healthPct > thresholdPct)
            {
                ScheduleSurturHealthThresholdCheck(player, state);
                return;
            }

            ClearSurturBanishPortals(region, state);
            SetSurturBanishInvulnerable(state, surtur, true);
            ClampSurturHealthToThreshold(surtur, thresholdPct);
            state.SurturBanishInProgress = true;
            int triggered = TriggerSurturBanishWave(player, region, state, waveIndex);
            Logger.Info($"[OmegaTrialTrace] stage=banish-wave-fired playerDbId=0x{playerDbId:X} wave={waveIndex + 1}/{SurturBanishHealthThresholds.Length} thresholdPct={thresholdPct} healthPct={healthPct} triggered={triggered}");
            if (triggered > 0)
            {
                SendTrialChat(player, "Surtur becomes invulnerable. Defeat the summoned champion.");
            }
            else
            {
                state.SurturBanishInProgress = false;
                SetSurturBanishInvulnerable(state, surtur, false);
            }

            state.NextSurturBanishWaveIndex = waveIndex + 1;
            ScheduleSurturHealthThresholdCheck(player, state);
        }

        private static int TriggerSurturBanishWave(Player player, Region region, OmegaTrialState state, int waveIndex)
        {
            if (player == null || region == null || state == null || waveIndex < 0 || waveIndex >= SurturBanishHealthThresholds.Length)
                return 0;

            return EnsureSurturBanishMiniBossWave(player, region, state, waveIndex);
        }

        private static int EnsureSurturBanishMiniBossWave(Player player, Region region, OmegaTrialState state, int waveIndex)
        {
            return waveIndex switch
            {
                0 => EnsureSurturBanishMiniBoss(player, region, state, SurturSlagMiniBossPrototypeName, "Slag"),
                1 => EnsureSurturBanishMiniBoss(player, region, state, SurturHellfireMiniBossPrototypeName, "Hellfire") +
                     EnsureSurturBanishMiniBoss(player, region, state, SurturBrimstoneMiniBossPrototypeName, "Brimstone"),
                2 => EnsureSurturBanishMiniBoss(player, region, state, SurturMistressMiniBossPrototypeName, "Mistress"),
                _ => 0
            };
        }

        private static int EnsureSurturBanishMiniBoss(Player player, Region region, OmegaTrialState state, string miniBossPrototypeName, string label)
        {
            PrototypeId miniBossRef = GetPrototypeRefByName(miniBossPrototypeName);
            if (miniBossRef == PrototypeId.Invalid)
            {
                Logger.Info($"[OmegaTrialTrace] stage=banish-fallback-missing-prototype playerDbId=0x{state.PlayerDbId:X} miniboss={label} prototype={miniBossPrototypeName}");
                MarkSurturBanishMiniBossUnavailable(state, label);
                return 0;
            }

            if (HasLivingEntityPrototype(region, miniBossRef))
            {
                Logger.Info($"[OmegaTrialTrace] stage=banish-fallback-skipped-existing playerDbId=0x{state.PlayerDbId:X} miniboss={label} prototype={miniBossRef.GetNameFormatted()}");
                return 0;
            }

            if (TrySpawnSurturBanishMiniBoss(player, region, state, miniBossRef, label, out Agent miniBoss) == false)
            {
                Logger.Info($"[OmegaTrialTrace] stage=banish-fallback-failed playerDbId=0x{state.PlayerDbId:X} miniboss={label} prototype={miniBossRef.GetNameFormatted()}");
                MarkSurturBanishMiniBossUnavailable(state, label);
                return 0;
            }

            Logger.Info($"[OmegaTrialTrace] stage=banish-arena-boss-spawned playerDbId=0x{state.PlayerDbId:X} miniboss={label} entityId=0x{miniBoss.Id:X} prototype={miniBoss.PrototypeDataRef.GetNameFormatted()} position={miniBoss.RegionLocation.Position} health={(long)miniBoss.Properties[PropertyEnum.Health]} healthMax={(long)miniBoss.Properties[PropertyEnum.HealthMax]}");
            return 1;
        }

        private static void MarkSurturBanishMiniBossUnavailable(OmegaTrialState state, string label)
        {
            if (state == null)
                return;

            switch (label)
            {
                case "Slag":
                    state.SurturSlagDefeated = true;
                    break;
                case "Hellfire":
                    state.SurturHellfireDefeated = true;
                    break;
                case "Brimstone":
                    state.SurturBrimstoneDefeated = true;
                    break;
                case "Mistress":
                    state.SurturMistressDefeated = true;
                    break;
            }

            Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-unavailable-counted playerDbId=0x{state.PlayerDbId:X} miniboss={label}");
        }

        private static bool TrySpawnSurturBanishMiniBoss(Player player, Region region, OmegaTrialState state, PrototypeId miniBossRef, string label, out Agent miniBoss)
        {
            miniBoss = null;

            Avatar avatar = player?.CurrentAvatar;
            AgentPrototype miniBossProto = miniBossRef.As<AgentPrototype>();
            if (avatar == null || region == null || state == null || miniBossProto == null)
                return false;

            if (TryResolveSurturBanishSpawnLocation(avatar, region, state, miniBossProto, label, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell) == false)
            {
                Logger.Info($"[OmegaTrialTrace] stage=banish-fallback-location-failed playerDbId=0x{state.PlayerDbId:X} miniboss={label} reason=no-banish-room-position");
                return false;
            }

            if (TryCreateTrialBoss(region, miniBossProto, spawnPosition, spawnOrientation, spawnCell, out miniBoss) == false)
                return false;

            EnsureBanishMiniBossDamageable(state, miniBoss, label);
            ApplySurturBanishMiniBossHealthMultiplier(state, miniBoss, label);
            return true;
        }

        private static bool TryHandleSurturBanishMiniBossDeath(Player player, Region region, OmegaTrialState state, WorldEntity defender)
        {
            if (player == null || region == null || state == null || defender == null)
                return false;

            if (IsPrototype(defender, SurturSlagMiniBossPrototypeName))
            {
                state.SurturSlagDefeated = true;
                Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-dead playerDbId=0x{state.PlayerDbId:X} miniboss=Slag entityId=0x{defender.Id:X}");
                TryResumeSurturAfterArenaBosses(player, region, state);
                return true;
            }

            if (IsPrototype(defender, SurturHellfireMiniBossPrototypeName))
            {
                state.SurturHellfireDefeated = true;
                Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-dead playerDbId=0x{state.PlayerDbId:X} miniboss=Hellfire entityId=0x{defender.Id:X} brimstoneDefeated={state.SurturBrimstoneDefeated}");
                if (state.SurturBrimstoneDefeated)
                    TryResumeSurturAfterArenaBosses(player, region, state);

                return true;
            }

            if (IsPrototype(defender, SurturBrimstoneMiniBossPrototypeName))
            {
                state.SurturBrimstoneDefeated = true;
                Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-dead playerDbId=0x{state.PlayerDbId:X} miniboss=Brimstone entityId=0x{defender.Id:X} hellfireDefeated={state.SurturHellfireDefeated}");
                if (state.SurturHellfireDefeated)
                    TryResumeSurturAfterArenaBosses(player, region, state);

                return true;
            }

            if (IsPrototype(defender, SurturMistressMiniBossPrototypeName))
            {
                state.SurturMistressDefeated = true;
                Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-dead playerDbId=0x{state.PlayerDbId:X} miniboss=Mistress entityId=0x{defender.Id:X}");
                TryResumeSurturAfterArenaBosses(player, region, state);
                return true;
            }

            return false;
        }

        private static void TryResumeSurturAfterArenaBosses(Player player, Region region, OmegaTrialState state)
        {
            if (state == null || state.SurturBanishInProgress == false)
                return;

            if (IsCurrentSurturBanishWaveDefeated(state) == false)
                return;

            Logger.Info($"[OmegaTrialTrace] stage=banish-wave-cleared playerDbId=0x{state.PlayerDbId:X} activeWave={state.NextSurturBanishWaveIndex}");
            ResumeSurturAfterBanish(player, region, state);
            SendTrialChat(player, "Surtur is vulnerable again.");
        }

        private static bool IsCurrentSurturBanishWaveDefeated(OmegaTrialState state)
        {
            if (state == null)
                return false;

            int activeWaveIndex = state.NextSurturBanishWaveIndex - 1;
            return activeWaveIndex switch
            {
                0 => state.SurturSlagDefeated,
                1 => state.SurturHellfireDefeated && state.SurturBrimstoneDefeated,
                2 => state.SurturMistressDefeated,
                _ => false
            };
        }

        private static bool TriggerSurturReturnSpawnerOnce(Player player, Region region, OmegaTrialState state, string spawnerPrototypeName, ref bool alreadyTriggered)
        {
            if (alreadyTriggered)
                return true;

            alreadyTriggered = true;
            int triggered = TriggerSpawnerPrototype(region, spawnerPrototypeName, EntityTriggerEnum.Enabled);
            Logger.Info($"[OmegaTrialTrace] stage=banish-return-triggered playerDbId=0x{state?.PlayerDbId ?? 0:X} spawner={spawnerPrototypeName} triggered={triggered}");
            bool servicePortalSpawned = triggered <= 0 && TrySpawnSurturBanishReturnPortal(player, region, state);

            if (triggered > 0)
                SendTrialChat(player, "A return portal has opened.");

            return triggered > 0 || servicePortalSpawned;
        }

        private static void ResumeSurturAfterBanish(Player player, Region region, OmegaTrialState state)
        {
            if (state == null)
                return;

            Agent surtur = region?.Game?.EntityManager.GetEntity<Agent>(state.SurturEntityId);
            SetSurturBanishInvulnerable(state, surtur, false);
            state.SurturBanishInProgress = false;
            ScheduleSurturHealthThresholdCheck(player, state);
        }

        private static bool TrySpawnSurturBanishPortal(Player player, Region region, OmegaTrialState state, Agent miniBoss)
        {
            if (player?.CurrentAvatar == null || region == null || state == null || miniBoss == null)
                return false;

            if (state.BanishPortalEntityId != Entity.InvalidId && region.Game.EntityManager.GetEntity<Transition>(state.BanishPortalEntityId) != null)
                return true;

            if (TryResolveReturnPortalLocation(player.CurrentAvatar, region, state, out Vector3 portalPosition, out Orientation portalOrientation, out Cell portalCell) == false)
                return false;

            Transition portal = SpawnOmegaTrialPortal(player, region, portalPosition, portalOrientation, portalCell, "banish");
            if (portal == null)
                return false;

            state.BanishPortalEntityId = portal.Id;
            state.BanishPortalDestination = miniBoss.RegionLocation.Position;
            state.BanishPortalDestinationOrientation = miniBoss.RegionLocation.Orientation;
            Logger.Info($"[OmegaTrialTrace] stage=banish-portal-spawned playerDbId=0x{state.PlayerDbId:X} portalId=0x{portal.Id:X} position={portalPosition} destination={state.BanishPortalDestination}");
            return true;
        }

        private static bool TrySpawnSurturBanishReturnPortal(Player player, Region region, OmegaTrialState state)
        {
            if (player?.CurrentAvatar == null || region == null || state == null)
                return false;

            if (state.BanishReturnPortalEntityId != Entity.InvalidId && region.Game.EntityManager.GetEntity<Transition>(state.BanishReturnPortalEntityId) != null)
                return true;

            if (TryResolvePortalLocationNearAvatar(player.CurrentAvatar, region, out Vector3 portalPosition, out Orientation portalOrientation, out Cell portalCell) == false)
                return false;

            Transition portal = SpawnOmegaTrialPortal(player, region, portalPosition, portalOrientation, portalCell, "banish-return");
            if (portal == null)
                return false;

            state.BanishReturnPortalEntityId = portal.Id;
            state.BanishReturnPortalDestination = state.HasArenaAnchor ? state.ArenaAnchorPosition : player.CurrentAvatar.RegionLocation.Position;
            state.BanishReturnPortalDestinationOrientation = state.HasArenaAnchor ? state.ArenaAnchorOrientation : player.CurrentAvatar.RegionLocation.Orientation;
            SendTrialChat(player, "A return portal has opened.");
            Logger.Info($"[OmegaTrialTrace] stage=banish-return-portal-spawned playerDbId=0x{state.PlayerDbId:X} portalId=0x{portal.Id:X} position={portalPosition} destination={state.BanishReturnPortalDestination}");
            return true;
        }

        private static Transition SpawnOmegaTrialPortal(Player player, Region region, Vector3 position, Orientation orientation, Cell cell, string portalType)
        {
            PrototypeId portalRef = GetPrototypeRefByName(ReturnPortalPrototypeName);
            if (portalRef == PrototypeId.Invalid || player?.CurrentAvatar == null || region?.Game?.EntityManager == null || cell == null)
                return null;

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
            Logger.Info($"[OmegaTrialTrace] stage=service-portal-spawned playerDbId=0x{player.DatabaseUniqueId:X} portalType={portalType} portalId=0x{portal?.Id ?? 0UL:X} position={position}");
            return portal;
        }

        private static void ClearSurturBanishPortals(Region region, OmegaTrialState state)
        {
            if (region?.Game?.EntityManager == null || state == null)
                return;

            DestroyPortalIfPresent(region, state.BanishPortalEntityId);
            DestroyPortalIfPresent(region, state.BanishReturnPortalEntityId);
            state.BanishPortalEntityId = Entity.InvalidId;
            state.BanishReturnPortalEntityId = Entity.InvalidId;
            state.BanishPortalDestination = Vector3.Zero;
            state.BanishPortalDestinationOrientation = Orientation.Zero;
            state.BanishReturnPortalDestination = Vector3.Zero;
            state.BanishReturnPortalDestinationOrientation = Orientation.Zero;
        }

        private static void DestroyPortalIfPresent(Region region, ulong portalEntityId)
        {
            if (portalEntityId == Entity.InvalidId)
                return;

            region?.Game?.EntityManager.GetEntity<Transition>(portalEntityId)?.Destroy();
        }

        private static float GetHealthPct(WorldEntity entity)
        {
            if (entity == null)
                return 0f;

            long healthMax = Math.Max((long)entity.Properties[PropertyEnum.HealthMax], 1L);
            long health = Math.Max((long)entity.Properties[PropertyEnum.Health], 0L);
            return (health * 100f) / healthMax;
        }

        private static void ClampSurturHealthToThreshold(Agent surtur, float thresholdPct)
        {
            if (surtur == null)
                return;

            long healthMax = Math.Max((long)surtur.Properties[PropertyEnum.HealthMax], 1L);
            long thresholdHealth = Math.Max(1L, (long)Math.Ceiling(healthMax * (thresholdPct / 100f)));
            long currentHealth = surtur.Properties[PropertyEnum.Health];
            if (currentHealth < thresholdHealth)
                surtur.Properties[PropertyEnum.Health] = thresholdHealth;
        }

        private static void SetSurturBanishInvulnerable(OmegaTrialState state, Agent surtur, bool invulnerable)
        {
            if (state == null || surtur == null)
                return;

            if (invulnerable)
            {
                state.SurturWasInvulnerableBeforeBanish = surtur.Properties[PropertyEnum.Invulnerable];
                surtur.Properties[PropertyEnum.Invulnerable] = true;
                Logger.Info($"[OmegaTrialTrace] stage=surtur-banish-invulnerable playerDbId=0x{state.PlayerDbId:X} entityId=0x{surtur.Id:X} enabled=True health={(long)surtur.Properties[PropertyEnum.Health]} healthMax={(long)surtur.Properties[PropertyEnum.HealthMax]}");
                return;
            }

            surtur.Properties.RemoveProperty(PropertyEnum.Invulnerable);
            state.SurturWasInvulnerableBeforeBanish = false;

            Logger.Info($"[OmegaTrialTrace] stage=surtur-banish-invulnerable playerDbId=0x{state.PlayerDbId:X} entityId=0x{surtur.Id:X} enabled=False health={(long)surtur.Properties[PropertyEnum.Health]} healthMax={(long)surtur.Properties[PropertyEnum.HealthMax]}");
        }

        private static void EnsureBanishMiniBossDamageable(OmegaTrialState state, Agent miniBoss, string label)
        {
            if (miniBoss == null)
                return;

            bool wasInvulnerable = miniBoss.Properties[PropertyEnum.Invulnerable];
            bool wasUntargetable = miniBoss.Properties[PropertyEnum.Untargetable];
            bool wasImmuneToPower = miniBoss.Properties[PropertyEnum.ImmuneToPower];
            bool wasTutorialInvulnerable = miniBoss.Properties[PropertyEnum.TutorialInvulnerable];

            miniBoss.Properties.RemoveProperty(PropertyEnum.Invulnerable);
            miniBoss.Properties.RemoveProperty(PropertyEnum.Untargetable);
            miniBoss.Properties.RemoveProperty(PropertyEnum.ImmuneToPower);
            miniBoss.Properties.RemoveProperty(PropertyEnum.TutorialInvulnerable);

            Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-damageable playerDbId=0x{state?.PlayerDbId ?? 0:X} miniboss={label} entityId=0x{miniBoss.Id:X} wasInvulnerable={wasInvulnerable} wasUntargetable={wasUntargetable} wasImmuneToPower={wasImmuneToPower} wasTutorialInvulnerable={wasTutorialInvulnerable}");
        }

        private static void ApplySurturBanishMiniBossHealthMultiplier(OmegaTrialState state, Agent miniBoss, string label)
        {
            if (miniBoss == null)
                return;

            float multiplier = GetSurturBanishMiniBossHealthMultiplier(label);
            long oldHealth = miniBoss.Properties[PropertyEnum.Health];
            long oldHealthMax = miniBoss.Properties[PropertyEnum.HealthMax];
            long oldHealthMaxOther = miniBoss.Properties[PropertyEnum.HealthMaxOther];
            float oldHealthPctBonus = miniBoss.Properties[PropertyEnum.HealthPctBonus];
            if (oldHealthMax <= 0)
                return;

            long targetHealth = GetSurturBanishMiniBossTargetHealth(label);
            if (targetHealth > 0)
                multiplier = Math.Min(multiplier, Math.Max(1f / oldHealthMax, targetHealth / (float)oldHealthMax));

            if (Math.Abs(multiplier - 1f) > 0.001f)
            {
                miniBoss.Properties[PropertyEnum.HealthPctBonus] = Math.Max(-0.995f, oldHealthPctBonus + multiplier - 1f);
                miniBoss.Properties[PropertyEnum.Health] = miniBoss.Properties[PropertyEnum.HealthMax];
            }

            Logger.Info($"[OmegaTrialTrace] stage=banish-miniboss-health-tuned playerDbId=0x{state?.PlayerDbId ?? 0:X} miniboss={label} entityId=0x{miniBoss.Id:X} multiplier={multiplier} targetHealth={targetHealth} oldHealth={oldHealth} oldHealthMax={oldHealthMax} oldHealthMaxOther={oldHealthMaxOther} oldHealthPctBonus={oldHealthPctBonus} newHealthPctBonus={(float)miniBoss.Properties[PropertyEnum.HealthPctBonus]} newHealth={(long)miniBoss.Properties[PropertyEnum.Health]} newHealthMax={(long)miniBoss.Properties[PropertyEnum.HealthMax]} newHealthMaxOther={(long)miniBoss.Properties[PropertyEnum.HealthMaxOther]}");
        }

        private static float GetSurturBanishMiniBossHealthMultiplier(string label)
        {
            return 1f;
        }

        private static long GetSurturBanishMiniBossTargetHealth(string label)
        {
            return label == "Mistress" ? MistressOfMagmaTargetHealth : 0L;
        }

        private static bool TryTeleportPlayerToSurturBanish(Player player, Region region, OmegaTrialState state)
        {
            if (player?.CurrentAvatar == null || region == null || state == null || state.BanishPortalDestination == Vector3.Zero)
                return false;

            Cell destinationCell = region.GetCellAtPosition(state.BanishPortalDestination);
            if (destinationCell == null)
            {
                Logger.Info($"[OmegaTrialTrace] stage=banish-auto-teleport-failed playerDbId=0x{state.PlayerDbId:X} reason=no-cell destination={state.BanishPortalDestination}");
                return false;
            }

            Vector3 resolvedPosition = RegionLocation.ProjectToFloor(region, destinationCell, state.BanishPortalDestination);
            resolvedPosition.Z = state.BanishPortalDestination.Z;
            ChangePositionResult result = player.CurrentAvatar.ChangeRegionPosition(resolvedPosition, state.BanishPortalDestinationOrientation, ChangePositionFlags.Teleport);
            DestroyPortalIfPresent(region, state.BanishPortalEntityId);
            state.BanishPortalEntityId = Entity.InvalidId;
            Logger.Info($"[OmegaTrialTrace] stage=banish-auto-teleport playerDbId=0x{state.PlayerDbId:X} result={result} destination={resolvedPosition}");
            return result != ChangePositionResult.InvalidPosition;
        }

        private static void ApplySurturHealthMultiplier(OmegaTrialState state, Agent surtur)
        {
            if (surtur == null)
                return;

            long oldHealth = surtur.Properties[PropertyEnum.Health];
            long oldHealthMax = surtur.Properties[PropertyEnum.HealthMax];
            long oldHealthMaxOther = surtur.Properties[PropertyEnum.HealthMaxOther];
            float oldHealthPctBonus = surtur.Properties[PropertyEnum.HealthPctBonus];
            if (oldHealthMax <= 0)
                return;

            float healthMultiplier = Math.Max(1f / oldHealthMax, SurturTargetHealth / (float)oldHealthMax);
            surtur.Properties[PropertyEnum.HealthPctBonus] = Math.Max(-0.995f, oldHealthPctBonus + healthMultiplier - 1f);
            surtur.Properties[PropertyEnum.Health] = surtur.Properties[PropertyEnum.HealthMax];

            Logger.Info($"[OmegaTrialTrace] stage=surtur-health-tuned playerDbId=0x{state?.PlayerDbId ?? 0:X} entityId=0x{surtur.Id:X} targetHealth={SurturTargetHealth} multiplier={healthMultiplier} oldHealth={oldHealth} oldHealthMax={oldHealthMax} oldHealthMaxOther={oldHealthMaxOther} oldHealthPctBonus={oldHealthPctBonus} newHealthPctBonus={(float)surtur.Properties[PropertyEnum.HealthPctBonus]} newHealth={(long)surtur.Properties[PropertyEnum.Health]} newHealthMax={(long)surtur.Properties[PropertyEnum.HealthMax]} newHealthMaxOther={(long)surtur.Properties[PropertyEnum.HealthMaxOther]}");
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
                player.Game?.GameEventScheduler?.CancelEvent(state.PhaseTimeoutEvent);
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

        private static bool TryResolvePortalLocationNearAvatar(Avatar avatar, Region region, out Vector3 position, out Orientation orientation, out Cell cell)
        {
            position = Vector3.Zero;
            orientation = Orientation.Zero;
            cell = null;

            if (avatar == null || region == null)
                return false;

            position = avatar.RegionLocation.Position + (avatar.Forward * 250f);
            orientation = avatar.RegionLocation.Orientation;
            cell = avatar.Cell ?? region.GetCellAtPosition(position);
            if (cell == null)
                return false;

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

        private static int FindEntitiesByPrototype(Region region, PrototypeId prototypeRef, List<WorldEntity> results)
        {
            if (region == null || prototypeRef == PrototypeId.Invalid || results == null)
                return 0;

            int count = 0;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not WorldEntity worldEntity ||
                    worldEntity.PrototypeDataRef != prototypeRef ||
                    worldEntity.IsDestroyed)
                {
                    continue;
                }

                results.Add(worldEntity);
                count++;
            }

            return count;
        }

        private static bool HasLivingEntityPrototype(Region region, PrototypeId prototypeRef)
        {
            if (region == null || prototypeRef == PrototypeId.Invalid)
                return false;

            foreach (Entity entity in region.Entities)
            {
                if (entity is WorldEntity worldEntity &&
                    worldEntity.PrototypeDataRef == prototypeRef &&
                    worldEntity.IsDestroyed == false &&
                    worldEntity.IsDead == false)
                {
                    return true;
                }
            }

            return false;
        }

        private static int TriggerSpawnerPrototype(Region region, string spawnerPrototypeName, EntityTriggerEnum trigger)
        {
            if (region == null || string.IsNullOrWhiteSpace(spawnerPrototypeName))
                return 0;

            PrototypeId spawnerRef = GetPrototypeRefByName(spawnerPrototypeName);
            if (spawnerRef == PrototypeId.Invalid)
            {
                Logger.Info($"[OmegaTrialTrace] stage=spawner-trigger-missing-prototype spawner={spawnerPrototypeName} trigger={trigger}");
                return 0;
            }

            List<Spawner> spawnersToTrigger = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not Spawner spawner || spawner.IsDestroyed || spawner.PrototypeDataRef != spawnerRef)
                    continue;

                spawnersToTrigger ??= new();
                spawnersToTrigger.Add(spawner);
            }

            if (spawnersToTrigger == null)
            {
                Logger.Info($"[OmegaTrialTrace] stage=spawner-trigger-no-entities regionId=0x{region.Id:X} spawner={spawnerRef.GetNameFormatted()} trigger={trigger}");
                return 0;
            }

            int triggered = 0;
            foreach (Spawner spawner in spawnersToTrigger)
            {
                if (spawner.IsDestroyed)
                    continue;

                Logger.Info($"[OmegaTrialTrace] stage=spawner-trigger-before regionId=0x{region.Id:X} spawnerEntityId=0x{spawner.Id:X} spawner={spawnerRef.GetNameFormatted()} trigger={trigger} position={spawner.RegionLocation.Position}");
                spawner.Trigger(trigger);
                triggered++;
            }

            Logger.Info($"[OmegaTrialTrace] stage=spawner-trigger-after regionId=0x{region.Id:X} spawner={spawnerRef.GetNameFormatted()} trigger={trigger} triggered={triggered}");
            return triggered;
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

        private static void EnsureSurturCover(Region region, OmegaTrialState state)
        {
            if (region == null || state == null)
                return;

            int existingCount = RevealExistingSurturCover(region, state);
            int spawnedCount = SpawnSurturCoverFromRaidMarkers(region, state);
            int totalCount = existingCount + spawnedCount;
            Logger.Info($"[OmegaTrialTrace] stage=surtur-cover-ready playerDbId=0x{state.PlayerDbId:X} existing={existingCount} spawned={spawnedCount} total={totalCount}");
        }

        private static int RevealExistingSurturCover(Region region, OmegaTrialState state)
        {
            using var coverEntitiesHandle = ListPool<WorldEntity>.Get(out List<WorldEntity> coverEntities);
            FindEntitiesByPrototype(region, GetPrototypeRefByName(SurturCoverRockAPrototypeName), coverEntities);
            FindEntitiesByPrototype(region, GetPrototypeRefByName(SurturCoverRockBPrototypeName), coverEntities);

            int count = 0;
            foreach (WorldEntity coverEntity in coverEntities)
            {
                coverEntity.SetVisible(true);
                if (state.SurturCoverEntityIds.Contains(coverEntity.Id) == false)
                    state.SurturCoverEntityIds.Add(coverEntity.Id);

                Logger.Info($"[OmegaTrialTrace] stage=surtur-cover-revealed playerDbId=0x{state.PlayerDbId:X} entityId=0x{coverEntity.Id:X} position={coverEntity.RegionLocation.Position} prototype={coverEntity.PrototypeDataRef.GetNameFormatted()}");
                count++;
            }

            return count;
        }

        private static int SpawnSurturCoverFromRaidMarkers(Region region, OmegaTrialState state)
        {
            if (state.HasArenaAnchor == false || state.ArenaAnchorCell == null)
                return 0;

            int spawned = 0;
            foreach ((string prototypeName, Vector3 markerPosition, Orientation markerOrientation) in SurturCoverRockPlacements)
            {
                PrototypeId coverRef = GetPrototypeRefByName(prototypeName);
                WorldEntityPrototype coverProto = coverRef.As<WorldEntityPrototype>();
                if (coverProto == null)
                {
                    Logger.Info($"[OmegaTrialTrace] stage=surtur-cover-missing-prototype playerDbId=0x{state.PlayerDbId:X} prototype={prototypeName}");
                    continue;
                }

                Vector3 spawnPosition = GetSurturCoverSpawnPosition(state, markerPosition);
                Cell spawnCell = region.GetCellAtPosition(spawnPosition) ?? state.ArenaAnchorCell;
                if (spawnCell == null)
                    continue;

                if (TryCreateTrialWorldEntity(region, coverProto, spawnPosition, markerOrientation, spawnCell, out WorldEntity coverEntity) == false)
                    continue;

                coverEntity.SetVisible(true);
                state.SurturCoverEntityIds.Add(coverEntity.Id);
                state.SurturSpawnedCoverEntityIds.Add(coverEntity.Id);
                Logger.Info($"[OmegaTrialTrace] stage=surtur-cover-spawned playerDbId=0x{state.PlayerDbId:X} entityId=0x{coverEntity.Id:X} position={coverEntity.RegionLocation.Position} markerPosition={markerPosition} spawnPosition={spawnPosition} markerOrientation={markerOrientation} cell={(spawnCell?.Id.ToString("X") ?? "unknown")} prototype={coverEntity.PrototypeDataRef.GetNameFormatted()}");
                spawned++;
            }

            return spawned;
        }

        private static Vector3 GetSurturCoverSpawnPosition(OmegaTrialState state, Vector3 markerPosition)
        {
            if (state?.HasArenaAnchor == true)
                markerPosition.Z = state.ArenaAnchorPosition.Z;

            return markerPosition;
        }

        private static bool TryCreateTrialWorldEntity(Region region, WorldEntityPrototype entityProto, Vector3 spawnPosition, Orientation spawnOrientation, Cell spawnCell, out WorldEntity worldEntity)
        {
            worldEntity = null;
            if (region == null || entityProto == null || spawnCell == null)
                return false;

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = entityProto.DataRef;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.RegionId = region.Id;
            settings.Cell = spawnCell;
            settings.IsPopulation = true;

            using var settingsPropertiesHandle = PropertyCollectionPool.Get(out PropertyCollection settingsProperties);
            int level = spawnCell.Area.GetCharacterLevel(entityProto);
            settingsProperties[PropertyEnum.CharacterLevel] = level;
            settingsProperties[PropertyEnum.CombatLevel] = level;
            settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
            settingsProperties[PropertyEnum.NoLootDrop] = true;
            settingsProperties[PropertyEnum.MissionXEncounterHostilityOk] = true;
            settings.Properties = settingsProperties;

            worldEntity = region.Game.EntityManager.CreateEntity(settings) as WorldEntity;
            return worldEntity != null;
        }

        private static void ClearSurturCover(OmegaTrialState state)
        {
            Region region = state?.CachedRegion;
            if (region == null || state.SurturCoverEntityIds.Count == 0)
                return;

            foreach (ulong coverEntityId in state.SurturCoverEntityIds)
            {
                WorldEntity coverEntity = region.Game.EntityManager.GetEntity<WorldEntity>(coverEntityId);
                if (coverEntity == null || coverEntity.IsDestroyed)
                    continue;

                if (state.SurturSpawnedCoverEntityIds.Contains(coverEntityId))
                    coverEntity.Destroy();
                else
                    coverEntity.SetVisible(false);
            }

            Logger.Info($"[OmegaTrialTrace] stage=surtur-cover-cleared playerDbId=0x{state.PlayerDbId:X} tracked={state.SurturCoverEntityIds.Count} spawned={state.SurturSpawnedCoverEntityIds.Count}");
            state.SurturCoverEntityIds.Clear();
            state.SurturSpawnedCoverEntityIds.Clear();
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

        private static bool TryResolveSurturBanishSpawnLocation(Avatar avatar, Region region, OmegaTrialState state, AgentPrototype miniBossProto, string label, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell)
        {
            spawnPosition = Vector3.Zero;
            spawnOrientation = state.HasArenaAnchor ? state.ArenaAnchorOrientation : avatar.RegionLocation.Orientation;
            spawnCell = null;

            if (avatar == null || region == null || state == null || miniBossProto == null)
                return false;

            Vector3 anchorPosition = state.HasArenaAnchor ? state.ArenaAnchorPosition : avatar.RegionLocation.Position;
            Vector3[] offsets = GetSurturBanishFallbackOffsets(label);
            PathFlags pathFlags = Region.GetPathFlagsForEntity(miniBossProto);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 preferredPosition = anchorPosition + offsets[i];
                bool resolved = miniBossProto.Bounds == null
                    ? TryResolveBanishPointWithoutBounds(region, preferredPosition, out spawnPosition, out spawnCell)
                    : TryChooseBossSpawnPosition(region, miniBossProto, preferredPosition, pathFlags, out spawnPosition, out spawnCell);

                if (resolved == false)
                {
                    Logger.Info($"[OmegaTrialTrace] stage=banish-fallback-location-miss playerDbId=0x{state.PlayerDbId:X} miniboss={label} preferred={preferredPosition} reason=no-position");
                    continue;
                }

                spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawnPosition);
                float arenaDistance = Vector3.Distance2D(anchorPosition, spawnPosition);
                if (arenaDistance > SurturBanishArenaMaxSpawnDistance)
                {
                    Logger.Info($"[OmegaTrialTrace] stage=banish-arena-location-rejected playerDbId=0x{state.PlayerDbId:X} miniboss={label} preferred={preferredPosition} resolved={spawnPosition} arenaDistance={arenaDistance} maxDistance={SurturBanishArenaMaxSpawnDistance} reason=too-far-from-arena");
                    continue;
                }

                if (miniBossProto.Bounds != null)
                    spawnPosition.Z += miniBossProto.Bounds.GetBoundHalfHeight();

                spawnOrientation = Orientation.FromDeltaVector2D(anchorPosition - spawnPosition);
                Logger.Info($"[OmegaTrialTrace] stage=banish-arena-location-resolved playerDbId=0x{state.PlayerDbId:X} miniboss={label} preferred={preferredPosition} resolved={spawnPosition} arenaDistance={arenaDistance} cell={(spawnCell?.Id.ToString("X") ?? "unknown")}");
                return true;
            }

            return false;
        }

        private static Vector3[] GetSurturBanishFallbackOffsets(string label)
        {
            return label switch
            {
                "Slag" => new[]
                {
                    new Vector3(650f, 0f, 0f),
                    new Vector3(500f, 300f, 0f),
                    new Vector3(500f, -300f, 0f)
                },
                "Hellfire" => new[]
                {
                    new Vector3(450f, -350f, 0f),
                    new Vector3(650f, -250f, 0f),
                    new Vector3(300f, -500f, 0f),
                    new Vector3(0f, -650f, 0f)
                },
                "Brimstone" => new[]
                {
                    new Vector3(450f, 350f, 0f),
                    new Vector3(650f, 250f, 0f),
                    new Vector3(300f, 500f, 0f),
                    new Vector3(0f, 650f, 0f)
                },
                "Mistress" => new[]
                {
                    new Vector3(-650f, 0f, 0f),
                    new Vector3(-500f, 300f, 0f),
                    new Vector3(-500f, -300f, 0f)
                },
                _ => new[]
                {
                    new Vector3(650f, 0f, 0f),
                    new Vector3(-650f, 0f, 0f),
                    new Vector3(0f, 650f, 0f),
                    new Vector3(0f, -650f, 0f)
                }
            };
        }

        private static bool TryResolveBanishPointWithoutBounds(Region region, Vector3 preferredPosition, out Vector3 spawnPosition, out Cell spawnCell)
        {
            spawnPosition = preferredPosition;
            spawnCell = region?.GetCellAtPosition(preferredPosition);
            if (region == null || spawnCell == null)
                return false;

            spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, preferredPosition);
            spawnCell = region.GetCellAtPosition(spawnPosition) ?? spawnCell;
            return spawnCell != null;
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

        private static bool IsPrototype(WorldEntity entity, string prototypeName)
        {
            if (entity == null || string.IsNullOrWhiteSpace(prototypeName))
                return false;

            PrototypeId prototypeRef = GetPrototypeRefByName(prototypeName);
            return prototypeRef != PrototypeId.Invalid && entity.PrototypeDataRef == prototypeRef;
        }

        private static PrototypeId GetSurturTrialRegionRef()
        {
            RegionConnectionTargetPrototype targetProto = GetPrototypeRefByName(SurturTrialEntryTargetPrototypeName).As<RegionConnectionTargetPrototype>();
            return targetProto?.Region ?? PrototypeId.Invalid;
        }

        private static PrototypeId GetPrototypeRefByName(string prototypeName)
        {
            if (string.IsNullOrWhiteSpace(prototypeName))
                return PrototypeId.Invalid;

            lock (PrototypeCacheLock)
            {
                if (PrototypeRefCache.TryGetValue(prototypeName, out PrototypeId prototypeRef))
                    return prototypeRef;

                prototypeRef = GameDatabase.GetPrototypeRefByName(prototypeName);
                PrototypeRefCache[prototypeName] = prototypeRef;
                return prototypeRef;
            }
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
            if (state.SurturBanishInProgress)
                SetSurturBanishInvulnerable(state, state.CachedRegion?.Game?.EntityManager.GetEntity<Agent>(state.SurturEntityId), false);

            ClearSurturCover(state);
            ClearSurturBanishPortals(state.CachedRegion, state);
            state.CachedRegion?.Game?.GameEventScheduler?.CancelEvent(state.PostEntryNativeSuppressionEvent);
            state.CachedRegion?.Game?.GameEventScheduler?.CancelEvent(state.LokiPhase1SpawnEvent);
            state.CachedRegion?.Game?.GameEventScheduler?.CancelEvent(state.PhaseTimeoutEvent);
            state.CachedRegion?.Game?.GameEventScheduler?.CancelEvent(state.SurturBanishWaveEvent);
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

                RegionTrialStates.Remove(regionId);
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
            public ulong BanishPortalEntityId;
            public ulong BanishReturnPortalEntityId;
            public Vector3 BanishPortalDestination;
            public Orientation BanishPortalDestinationOrientation;
            public Vector3 BanishReturnPortalDestination;
            public Orientation BanishReturnPortalDestinationOrientation;
            public TimeSpan PhaseStartedAt;
            public TimeSpan PhaseExpiresAt;
            public readonly EventPointer<OmegaTrialPostEntryNativeSuppressionEvent> PostEntryNativeSuppressionEvent = new();
            public readonly EventPointer<OmegaTrialLokiPhase1SpawnEvent> LokiPhase1SpawnEvent = new();
            public readonly EventPointer<OmegaTrialPhaseTimeoutEvent> PhaseTimeoutEvent = new();
            public readonly EventPointer<OmegaTrialSurturBanishWaveEvent> SurturBanishWaveEvent = new();
            public int NextSurturBanishWaveIndex;
            public bool SurturSlagDefeated;
            public bool SurturHellfireDefeated;
            public bool SurturBrimstoneDefeated;
            public bool SurturMistressDefeated;
            public bool SurturSlagReturnTriggered;
            public bool SurturTwinsReturnTriggered;
            public bool SurturMistressReturnTriggered;
            public bool SurturBanishInProgress;
            public bool SurturWasInvulnerableBeforeBanish;
            public readonly List<ulong> SurturCoverEntityIds = new();
            public readonly List<ulong> SurturSpawnedCoverEntityIds = new();
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

        private sealed class OmegaTrialPostEntryNativeSuppressionEvent : ScheduledEvent
        {
            public ulong PlayerDbId;

            public override void OnTriggered()
            {
                HandlePostEntryNativeSuppression(PlayerDbId);
            }

            public override void Clear()
            {
                PlayerDbId = 0;
            }
        }

        private sealed class OmegaTrialPhaseTimeoutEvent : ScheduledEvent
        {
            public ulong PlayerDbId;
            public OmegaTrialStage Stage;

            public override void OnTriggered()
            {
                HandlePhaseTimeout(PlayerDbId, Stage);
            }

            public override void Clear()
            {
                PlayerDbId = 0;
                Stage = default;
            }
        }

        private sealed class OmegaTrialLokiPhase1SpawnEvent : ScheduledEvent
        {
            public ulong PlayerDbId;

            public override void OnTriggered()
            {
                HandleLokiPhase1Spawn(PlayerDbId);
            }

            public override void Clear()
            {
                PlayerDbId = 0;
            }
        }

        private sealed class OmegaTrialSurturBanishWaveEvent : ScheduledEvent
        {
            public ulong PlayerDbId;
            public int WaveIndex;

            public override void OnTriggered()
            {
                HandleSurturHealthThresholdCheck(PlayerDbId, WaveIndex);
            }

            public override void Clear()
            {
                PlayerDbId = 0;
                WaveIndex = 0;
            }
        }

        private readonly struct OmegaTrialSpawnerTrigger
        {
            public readonly string PrototypeName;
            public readonly EntityTriggerEnum Trigger;

            public OmegaTrialSpawnerTrigger(string prototypeName, EntityTriggerEnum trigger)
            {
                PrototypeName = prototypeName;
                Trigger = trigger;
            }
        }
    }
}
