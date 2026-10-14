using Gazillion;
using MHServerEmu.Core.Extensions;
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
        private const string SilverSableOmegaTrialGuidePrototypeName = "Entity/Characters/NPCs/SilverSableHelicarrier.prototype";
        private const string SurturTrialEntryTargetPrototypeName = "Regions/RAIDS/MuspelheimRaid/ConnectionNodes/SurturRaidReduxEntryTargetBase.prototype";
        private const string SurturFinalEncounterHotspotPrototypeName = "Entity/Missions/Hotspots/SurturRaidFinalEncTriggerHotspot.prototype";
        private const string LokiPhase1PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase1.prototype";
        private const string LokiPhase2PrototypeName = "Entity/Characters/Bosses/Story/LokiPhase2.prototype";
        private const string LokiEnergyRingOuterEffectPrototypeName = "Powers/EnemyPowers/Boss/Loki/LokiEnergyRingOuterHSEffect.prototype";
        private const string WingedDemonMentalBlastEffectPrototypeName = "Powers/EnemyPowers/MobPowers/FireDemons/WingedDemonMentalBlastEnc3MEffec.prototype";
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
        private const string SurturSlagMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/SlagMiniBoss.prototype";
        private const string SurturHellfireMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/HellfireMiniBoss.prototype";
        private const string SurturBrimstoneMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/BrimstoneMiniBoss.prototype";
        private const string SurturMistressMiniBossPrototypeName = "Entity/Characters/Bosses/SurturRaid/FiveMan/SurturFightMinibosses/MistressOfMagmaMiniBoss.prototype";
        private const string SurturCoverRockAPrototypeName = "Entity/Props/MultiStageDestructibles/DBMuspelheimSurturBlocker.prototype";
        private const string SurturCoverRockBPrototypeName = "Entity/Props/MultiStageDestructibles/DBMuspelheimSurturBlockerB.prototype";
        private const string SurturSafeZoneMarkerPrototypeName = "Entity/Characters/Bosses/SurturRaid/SurturFightMiniBosses/SurturMarkedForDeathSafeZoneMark.prototype";
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
        private const float LokiEnergyRingOuterDamageMultiplier = 0.25f;
        private const float MistressWaveWingedDemonMentalBlastDamageMultiplier = 0.5f;
        private const float LokiSpawnDistance = 600f;
        private const float SurturSpawnDistance = 1200f;
        private const float BossSpawnSearchDistance = 3200f;
        private const float SurturBanishArenaMaxSpawnDistance = 1250f;
        private const long SurturTargetHealth = 6000000L;
        private const long MistressOfMagmaTargetHealth = 1500000L;
        private static readonly TimeSpan LokiPhase1SpawnDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan LokiFightTimeLimit = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SurturFightTimeLimit = TimeSpan.FromMinutes(5);
        private const int PlayerDeathLimit = 3;
        private static readonly TimeSpan SurturHealthThresholdCheckInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan PostEntryNativeSuppressionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan TrialReturnPortalLifespan = TimeSpan.FromMinutes(10);
        private static readonly Vector3 SurturFinalEncounterHotspotExportPosition = new(11024f, 13072f, 176f);
        private static readonly Vector3 SurturFinalBossEncounterExportPosition = new(12193.019f, 14205.562f, 255.99998f);
        private static readonly Vector3 ProblematicNativeSurturCoverPosition = new(30128f, 12592f, 230f);
        private const float ProblematicNativeSurturCoverPositionToleranceSquared = 64f;
        private const float SurturSafeZoneMarkerOffset = 180f;
        private static readonly float[] SurturBanishHealthThresholds = { 75f, 50f, 25f };
        private static readonly (string PrototypeName, Vector3 Position, Orientation Orientation)[] SurturCoverRockPlacements =
        {
            (SurturCoverRockBPrototypeName, new Vector3(31040f, 12416f, 176f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(30350f, 13179f, 176f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(31800f, 12670f, 176f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(30409f, 14000f, 176f), new Orientation(1.57082f, 0f, 0f)),
            (SurturCoverRockBPrototypeName, new Vector3(31040f, 13336f, 176f), new Orientation(1.57082f, 0f, 0f))
        };
        private static readonly object SyncRoot = new();
        private static readonly object PrototypeCacheLock = new();
        private static readonly Dictionary<string, PrototypeId> PrototypeRefCache = new();
        private static readonly Dictionary<ulong, OmegaTrialState> TrialStates = new();
        private static readonly Dictionary<ulong, OmegaTrialState> RegionTrialStates = new();
        private static readonly Dictionary<ulong, Event<EntityDeadGameEvent>.Action> RegionEntityDeadActions = new();
        private static readonly Dictionary<ulong, Event<PlayerDeathRecordedEvent>.Action> RegionPlayerDeathActions = new();

        private enum OmegaTrialStage
        {
            Traveling,
            LokiPhase1Active,
            LokiActive,
            SurturActive
        }

        public static bool TryAdjustPlayerDamage(WorldEntity target, PowerResults powerResults)
        {
            if (target is not Avatar targetAvatar || powerResults?.PowerPrototype == null || powerResults.TestFlag(PowerResultFlags.Hostile) == false)
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

            if (state == null || target.Region == null || state.RegionId != target.Region.Id)
                return false;

            PrototypeId powerRef = powerResults.PowerPrototype.DataRef;
            float damageMultiplier;
            if (state.Stage == OmegaTrialStage.LokiActive && powerRef == GetPrototypeRefByName(LokiEnergyRingOuterEffectPrototypeName))
            {
                damageMultiplier = LokiEnergyRingOuterDamageMultiplier;
            }
            else
            {
                bool isMistressWave =
                    state.Stage == OmegaTrialStage.SurturActive &&
                    state.SurturBanishInProgress &&
                    state.NextSurturBanishWaveIndex == SurturBanishHealthThresholds.Length &&
                    state.SurturMistressDefeated == false;
                if (isMistressWave == false || powerRef != GetPrototypeRefByName(WingedDemonMentalBlastEffectPrototypeName))
                    return false;

                damageMultiplier = MistressWaveWingedDemonMentalBlastDamageMultiplier;
            }

            for (DamageType damageType = DamageType.Physical; damageType < DamageType.NumDamageTypes; damageType++)
            {
                powerResults.Properties[PropertyEnum.Damage, damageType] *= damageMultiplier;
                powerResults.SetDamageForClient(damageType, powerResults.GetDamageForClient(damageType) * damageMultiplier);
            }

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
                EndTrial(player.DatabaseUniqueId);
                return;
            }

            if (state.Stage != OmegaTrialStage.Traveling)
            {
                return;
            }

            state.RegionId = region.Id;
            state.CachedRegion = region;
            ApplyTrialRegionScaling(state, region);
            RegisterRegionTrialState(region, state);
            SuppressNativeTrialContent(player, region, state);
            SchedulePostEntryNativeSuppression(player, state);
            bool movedToArena = TryMoveAvatarToFinalArena(player, region, state);
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
                TrySpawnReturnPortal(player, region, state);
                EndTrial(player.DatabaseUniqueId);
                return;
            }

            state.LokiEntityId = loki.Id;
            StartPhaseTimer(player, state, LokiFightTimeLimit);
            SendTrialChat(player, "Defeat Loki. Time limit: 5 minutes.");
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

            void OnSilverSableOmegaTrialGuideDialogResponse(ulong playerGuid, DialogResponse response)
            {
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
                player.SendBannerMessage(GameDatabase.UIGlobalsPrototype.MessageRegionRestricted);
                return;
            }

            PrototypeId entryTargetRef = GetPrototypeRefByName(SurturTrialEntryTargetPrototypeName);
            if (entryTargetRef == PrototypeId.Invalid)
            {
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
            teleporter.ForcePrivateRegion = true;

            if (teleporter.TeleportToTarget(entryTargetRef) == false)
            {
                EndTrial(player.DatabaseUniqueId);
            }
        }

        private static void RegisterRegionTrialState(Region region, OmegaTrialState state)
        {
            if (region == null || state == null)
                return;

            Event<EntityDeadGameEvent>.Action deadAction = (in EntityDeadGameEvent evt) => OnRegionEntityDead(region.Game, region.Id, evt);
            Event<PlayerDeathRecordedEvent>.Action playerDeathAction = (in PlayerDeathRecordedEvent evt) => OnPlayerDeathRecorded(region.Id, evt);
            lock (SyncRoot)
            {
                RegionTrialStates[region.Id] = state;

                if (RegionEntityDeadActions.ContainsKey(region.Id) == false)
                {
                    region.EntityDeadEvent.AddActionBack(deadAction);
                    RegionEntityDeadActions[region.Id] = deadAction;
                }

                if (RegionPlayerDeathActions.ContainsKey(region.Id) == false)
                {
                    region.PlayerDeathRecordedEvent.AddActionBack(playerDeathAction);
                    RegionPlayerDeathActions[region.Id] = playerDeathAction;
                }
            }
        }

        private static void OnPlayerDeathRecorded(ulong regionId, in PlayerDeathRecordedEvent evt)
        {
            Player player = evt.Player;
            if (player == null)
                return;

            OmegaTrialState state;
            lock (SyncRoot)
            {
                RegionTrialStates.TryGetValue(regionId, out state);
            }

            if (state == null || player.DatabaseUniqueId != state.PlayerDbId || player.CurrentAvatar?.Region?.Id != regionId)
                return;

            state.PlayerDeaths++;
            int deathsRemaining = Math.Max(PlayerDeathLimit - state.PlayerDeaths, 0);
            if (deathsRemaining > 0)
            {
                SendTrialChat(player, $"Omega Trial deaths remaining: {deathsRemaining}.");
                return;
            }

            FailTrial(player, state, "Death limit reached. Omega Trial failed.");
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
                EndTrial(state.PlayerDbId);
                return;
            }

            if (state.Stage == OmegaTrialStage.LokiPhase1Active && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase1PrototypeName))
            {
                SpawnLokiPhase2(player, player.CurrentAvatar.Region, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.LokiActive && IsTrackedDeath(evt.Defender, state.LokiEntityId, LokiPhase2PrototypeName))
            {
                SpawnSurturPhase(player, player.CurrentAvatar.Region, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.SurturActive && IsTrackedDeath(evt.Defender, state.SurturEntityId, SurturBossPrototypeName))
            {
                CompleteTrial(player, state);
                return;
            }

            if (state.Stage == OmegaTrialStage.SurturActive)
            {
                bool handledBanishDeath = TryHandleSurturBanishMiniBossDeath(player, player.CurrentAvatar.Region, state, evt.Defender);
            }
        }

        private static void SpawnLokiPhase2(Player player, Region region, OmegaTrialState state)
        {
            PrototypeId lokiRef = GetPrototypeRefByName(LokiPhase2PrototypeName);
            if (TrySpawnBossForTrial(player, region, state, lokiRef, LokiSpawnDistance, out Agent loki) == false)
            {
                string regionIdText = region != null ? $"0x{region.Id:X}" : "unknown";
                SendStopTrialTimer(player, state);
                TrySpawnReturnPortal(player, region, state);
                EndTrial(state.PlayerDbId);
                return;
            }

            state.Stage = OmegaTrialStage.LokiActive;
            state.LokiEntityId = loki.Id;
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
            RefreshTrialWidgets(region.UIDataProvider, state, player.Game.CurrentTime);
        }

        private static void CompleteTrial(Player player, OmegaTrialState state)
        {
            bool grantedAccess = RiftAccessTeleportService.GrantOmegaPatrolAccessForCurrentAvatar(player);
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

            FailTrial(player, state, "Time expired. Omega Trial failed.");
        }

        private static void FailTrial(Player player, OmegaTrialState state, string message)
        {
            if (player == null || state == null)
                return;

            SendTrialChat(player, message);
            SendStopTrialTimer(player, state);
            TrySpawnReturnPortal(player, state.CachedRegion, state);
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

            SetSurturBanishInvulnerable(state, surtur, true);
            ClampSurturHealthToThreshold(surtur, thresholdPct);
            state.SurturBanishInProgress = true;
            int triggered = TriggerSurturBanishWave(player, region, state, waveIndex);
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
                MarkSurturBanishMiniBossUnavailable(state, label);
                return 0;
            }

            if (HasLivingEntityPrototype(region, miniBossRef))
            {
                return 0;
            }

            if (TrySpawnSurturBanishMiniBoss(player, region, state, miniBossRef, label, out Agent miniBoss) == false)
            {
                MarkSurturBanishMiniBossUnavailable(state, label);
                return 0;
            }

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
                TryResumeSurturAfterArenaBosses(player, region, state);
                return true;
            }

            if (IsPrototype(defender, SurturHellfireMiniBossPrototypeName))
            {
                state.SurturHellfireDefeated = true;
                if (state.SurturBrimstoneDefeated)
                    TryResumeSurturAfterArenaBosses(player, region, state);

                return true;
            }

            if (IsPrototype(defender, SurturBrimstoneMiniBossPrototypeName))
            {
                state.SurturBrimstoneDefeated = true;
                if (state.SurturHellfireDefeated)
                    TryResumeSurturAfterArenaBosses(player, region, state);

                return true;
            }

            if (IsPrototype(defender, SurturMistressMiniBossPrototypeName))
            {
                state.SurturMistressDefeated = true;
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

        private static void ResumeSurturAfterBanish(Player player, Region region, OmegaTrialState state)
        {
            if (state == null)
                return;

            Agent surtur = region?.Game?.EntityManager.GetEntity<Agent>(state.SurturEntityId);
            SetSurturBanishInvulnerable(state, surtur, false);
            state.SurturBanishInProgress = false;
            ScheduleSurturHealthThresholdCheck(player, state);
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
                return;
            }

            surtur.Properties.RemoveProperty(PropertyEnum.Invulnerable);
            state.SurturWasInvulnerableBeforeBanish = false;

        }

        private static void EnsureBanishMiniBossDamageable(OmegaTrialState state, Agent miniBoss, string label)
        {
            if (miniBoss == null)
                return;

            miniBoss.Properties.RemoveProperty(PropertyEnum.Invulnerable);
            miniBoss.Properties.RemoveProperty(PropertyEnum.Untargetable);
            miniBoss.Properties.RemoveProperty(PropertyEnum.ImmuneToPower);
            miniBoss.Properties.RemoveProperty(PropertyEnum.TutorialInvulnerable);

        }

        private static void ApplySurturBanishMiniBossHealthMultiplier(OmegaTrialState state, Agent miniBoss, string label)
        {
            if (miniBoss == null)
                return;

            float multiplier = GetSurturBanishMiniBossHealthMultiplier(label);
            long oldHealthMax = miniBoss.Properties[PropertyEnum.HealthMax];
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

        }

        private static float GetSurturBanishMiniBossHealthMultiplier(string label)
        {
            return 1f;
        }

        private static long GetSurturBanishMiniBossTargetHealth(string label)
        {
            return label == "Mistress" ? MistressOfMagmaTargetHealth : 0L;
        }

        private static void ApplySurturHealthMultiplier(OmegaTrialState state, Agent surtur)
        {
            if (surtur == null)
                return;

            long oldHealthMax = surtur.Properties[PropertyEnum.HealthMax];
            float oldHealthPctBonus = surtur.Properties[PropertyEnum.HealthPctBonus];
            if (oldHealthMax <= 0)
                return;

            float healthMultiplier = Math.Max(1f / oldHealthMax, SurturTargetHealth / (float)oldHealthMax);
            surtur.Properties[PropertyEnum.HealthPctBonus] = Math.Max(-0.995f, oldHealthPctBonus + healthMultiplier - 1f);
            surtur.Properties[PropertyEnum.Health] = surtur.Properties[PropertyEnum.HealthMax];

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
            }
            catch (Exception)
            {
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
                return false;
            }

            if (TryResolveReturnPortalLocation(player.CurrentAvatar, region, state, out Vector3 position, out Orientation orientation, out Cell cell) == false)
            {
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
                return false;
            }

            if (portal.ConfigureDirectTarget(targetRef) == false)
            {
                portal.Destroy();
                return false;
            }

            state.ReturnPortalEntityId = portal.Id;
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
                return false;
            }

            Vector3 hotspotPosition = hotspot.RegionLocation.Position;
            Orientation hotspotOrientation = hotspot.RegionLocation.Orientation;
            Cell hotspotCell = hotspot.Cell ?? region.GetCellAtPosition(hotspotPosition);
            hotspot.Destroy();

            state.FinalEncounterMarkerPosition = hotspotPosition;
            state.HasFinalEncounterMarker = true;

            if (hotspotCell == null)
            {
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
                return false;
            }

            WorldEntity spawner = FindEntityByPrototype(region, spawnerRef);
            if (spawner == null || spawner.IsInWorld == false)
            {
                return false;
            }

            Cell spawnCell = spawner.Cell ?? region.GetCellAtPosition(spawner.RegionLocation.Position);
            if (spawnCell == null)
            {
                return false;
            }

            Vector3 spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawner.RegionLocation.Position);
            if (bossProto.Bounds != null)
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();

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
                return false;
            }

            Vector3 spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, markerPosition);
            if (bossProto.Bounds != null)
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();

            Vector3 facingTarget = state.HasArenaAnchor ? state.ArenaAnchorPosition : state.FinalEncounterMarkerPosition;
            Orientation orientation = Orientation.FromDeltaVector2D(facingTarget - spawnPosition);
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

            RevealExistingSurturCover(region, state);
            SpawnSurturCoverFromRaidMarkers(region, state);
        }

        private static int RevealExistingSurturCover(Region region, OmegaTrialState state)
        {
            using var coverEntitiesHandle = ListPool<WorldEntity>.Get(out List<WorldEntity> coverEntities);
            FindEntitiesByPrototype(region, GetPrototypeRefByName(SurturCoverRockAPrototypeName), coverEntities);
            PrototypeId coverRockBRef = GetPrototypeRefByName(SurturCoverRockBPrototypeName);
            FindEntitiesByPrototype(region, coverRockBRef, coverEntities);

            int count = 0;
            foreach (WorldEntity coverEntity in coverEntities)
            {
                if (coverEntity.PrototypeDataRef == coverRockBRef &&
                    Vector3.DistanceSquared(coverEntity.RegionLocation.Position, ProblematicNativeSurturCoverPosition) <= ProblematicNativeSurturCoverPositionToleranceSquared)
                {
                    coverEntity.Destroy();
                    continue;
                }

                coverEntity.SetVisible(true);
                if (state.SurturCoverEntityIds.Contains(coverEntity.Id) == false)
                    state.SurturCoverEntityIds.Add(coverEntity.Id);

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
                TrySpawnSurturSafeZoneMarker(region, state, spawnPosition, spawnCell);
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

        private static bool TrySpawnSurturSafeZoneMarker(Region region, OmegaTrialState state, Vector3 coverPosition, Cell fallbackCell)
        {
            PrototypeId markerRef = GetPrototypeRefByName(SurturSafeZoneMarkerPrototypeName);
            WorldEntityPrototype markerProto = markerRef.As<WorldEntityPrototype>();
            Agent surtur = region?.Game?.EntityManager.GetEntity<Agent>(state?.SurturEntityId ?? 0);
            if (markerProto == null || surtur == null)
                return false;

            Vector3 awayFromSurtur = coverPosition - surtur.RegionLocation.Position;
            awayFromSurtur.Z = 0f;
            awayFromSurtur = Vector3.SafeNormalize(awayFromSurtur);

            Vector3 markerPosition = coverPosition + awayFromSurtur * SurturSafeZoneMarkerOffset;
            markerPosition.Z = state.ArenaAnchorPosition.Z;
            Cell markerCell = region.GetCellAtPosition(markerPosition) ?? fallbackCell;
            if (markerCell == null || TryCreateTrialWorldEntity(region, markerProto, markerPosition, Orientation.Zero, markerCell, out WorldEntity markerEntity) == false)
                return false;

            state.SurturSafeZoneMarkerEntityIds.Add(markerEntity.Id);
            return true;
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
            if (region == null)
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

            state.SurturCoverEntityIds.Clear();
            state.SurturSpawnedCoverEntityIds.Clear();

            foreach (ulong markerEntityId in state.SurturSafeZoneMarkerEntityIds)
            {
                WorldEntity markerEntity = region.Game.EntityManager.GetEntity<WorldEntity>(markerEntityId);
                if (markerEntity != null && markerEntity.IsDestroyed == false)
                    markerEntity.Destroy();
            }

            state.SurturSafeZoneMarkerEntityIds.Clear();
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
                    return true;
                }
            }

            if (TryChooseBossSpawnPosition(region, bossProto, anchorPosition, pathFlags, out spawnPosition, out spawnCell))
            {
                spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawnPosition);
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();
                return true;
            }

            if (state.HasArenaAnchor)
            {
                spawnPosition = RegionLocation.ProjectToFloor(region, anchorCell, anchorPosition);
                spawnPosition.Z += bossProto.Bounds.GetBoundHalfHeight();
                spawnCell = anchorCell;
                return true;
            }

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
                    continue;
                }

                spawnPosition = RegionLocation.ProjectToFloor(region, spawnCell, spawnPosition);
                float arenaDistance = Vector3.Distance2D(anchorPosition, spawnPosition);
                if (arenaDistance > SurturBanishArenaMaxSpawnDistance)
                {
                    continue;
                }

                if (miniBossProto.Bounds != null)
                    spawnPosition.Z += miniBossProto.Bounds.GetBoundHalfHeight();

                spawnOrientation = Orientation.FromDeltaVector2D(anchorPosition - spawnPosition);
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
            Event<PlayerDeathRecordedEvent>.Action playerDeathAction;
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
                RegionPlayerDeathActions.TryGetValue(regionId, out playerDeathAction);
                RegionPlayerDeathActions.Remove(regionId);
            }

            if (deadAction != null)
                region?.EntityDeadEvent.RemoveAction(deadAction);
            if (playerDeathAction != null)
                region?.PlayerDeathRecordedEvent.RemoveAction(playerDeathAction);
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
            public int PlayerDeaths;
            public readonly EventPointer<OmegaTrialPostEntryNativeSuppressionEvent> PostEntryNativeSuppressionEvent = new();
            public readonly EventPointer<OmegaTrialLokiPhase1SpawnEvent> LokiPhase1SpawnEvent = new();
            public readonly EventPointer<OmegaTrialPhaseTimeoutEvent> PhaseTimeoutEvent = new();
            public readonly EventPointer<OmegaTrialSurturBanishWaveEvent> SurturBanishWaveEvent = new();
            public int NextSurturBanishWaveIndex;
            public bool SurturSlagDefeated;
            public bool SurturHellfireDefeated;
            public bool SurturBrimstoneDefeated;
            public bool SurturMistressDefeated;
            public bool SurturBanishInProgress;
            public bool SurturWasInvulnerableBeforeBanish;
            public readonly List<ulong> SurturCoverEntityIds = new();
            public readonly List<ulong> SurturSpawnedCoverEntityIds = new();
            public readonly List<ulong> SurturSafeZoneMarkerEntityIds = new();
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
