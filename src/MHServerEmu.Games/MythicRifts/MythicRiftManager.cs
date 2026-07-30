using Gazillion;
using MHServerEmu.Core.Collections;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Dialog;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;
using MHServerEmu.Games.Missions;
using MHServerEmu.Games.Navi;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.Social.Parties;
using MHServerEmu.Games.UI;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly bool SuspendNativeTerminalMissionsDuringRifts = true;
        private static readonly bool SuspendNativeRegionEventMissionsDuringRifts = true;
        private static readonly int[] TimeWarningThresholdSeconds = { 540, 480, 420, 360, 300, 240, 180, 120, 60, 30 };
        private static readonly int[] KillProgressMilestonePercents = { 25, 50, 75 };
        private static readonly TimeSpan ParticipantDisconnectAbortGracePeriod = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PendingRunBindGracePeriod = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CompletedRunRetention = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan NativeBossSuppressionScanInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RiftObjectiveWidgetRefreshInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan RiftReadyCheckDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RiftHazardSpawnInterval = TimeSpan.FromSeconds(14);
        private static readonly TimeSpan RiftHazardDuration = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan FailedRunEvacuationDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan FailedRunEvacuationRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan BossGauntletFailureRecoveryDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan PlayerDeathTimePenalty = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan CustomRiftPopulationSpawnInterval = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan CheckpointBossSpawnRetryInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan BossGauntletWaveRestInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan BossGauntletInterBossRestInterval = TimeSpan.FromSeconds(1);
        private const int CustomRiftPopulationBaseTargetAlive = 18;
        private const int CustomRiftPopulationTargetAlivePerExtraPlayer = 4;
        private const int CustomRiftPopulationBaseMaxAlive = 30;
        private const int CustomRiftPopulationMaxAlivePerExtraPlayer = 6;
        private const int CustomRiftPopulationBaseSpawnBatch = 8;
        private const int CustomRiftPopulationSpawnBatchPerExtraPlayer = 2;
        private const float CustomRiftPopulationSpawnMinDistance = 450f;
        private const float CustomRiftPopulationSpawnMaxDistance = 1500f;
        private const float CustomRiftPopulationFallbackSpawnDistance = 700f;
        private const int CheckpointRiftLevelInterval = 5;
        private const int CheckpointBossHealthTierInterval = 10;
        private const float CheckpointBossBaseHealthMultiplier = 2.0f;
        private const float CheckpointBossHealthMultiplierPerTier = 0.15f;
        private const float CheckpointBossMaxHealthMultiplier = 4.0f;
        private const float CheckpointBossSpawnDistance = 650f;
        private const float CheckpointBossSpawnSearchDistance = 260f;
        private const float BossGauntletBossSpawnDistance = 360f;
        private const float BossGauntletBossSpawnSearchDistance = 180f;
        private const int MilestoneMiniBossKillCredit = 10;
        private const int RiftPopulationRespawnDelayMS = 20000;
        private const int RecentRandomMapHistoryLimit = 4;
        private const int RecentRandomBossFamilyHistoryLimit = 4;
        private const int RiftSecondRegionAffixStartLevel = 30;
        private const int RiftThirdRegionAffixStartLevel = 70;
        private const int BossGauntletSecondBossAffixStartWave = 30;
        private const int BossGauntletThirdBossAffixStartWave = 70;
        private const ulong RiftEntryBannerLocaleStringBase = 18000000000000000000UL;
        private const int RiftEntryBannerLocalizedLevelLimit = 10000;
        private const int RiftEntryBannerTimeToLiveMS = 5000;
        private const ulong RiftClearedBannerLocaleStringBase = 18000000000000030000UL;
        private const int RiftClearedBannerLocaleStringCount = 20;
        private const int RiftClearedBannerTimeToLiveMS = 2000;
        private const string RiftDangerRoomLevelWidgetPrototypeName = "UI/MetaGame/MissionName.prototype";
        private const string RiftDangerRoomQuotaWidgetPrototypeName = "UI/MetaGame/DangerRoom/DangerRoomCounterBarBASE.prototype";
        private const string RiftDangerRoomTimerWidgetPrototypeName = "UI/MetaGame/DangerRoom/DangerRoomTimer.prototype";
        private const ulong RiftStatusLocaleReadyCheck = 18000000000000040001UL;
        private const ulong RiftStatusLocaleRegionModifiers = 18000000000000040002UL;
        private const ulong RiftStatusLocaleBossModifiers = 18000000000000040003UL;
        private const ulong RiftStatusLocaleBossWave = 18000000000000040004UL;
        private const ulong RiftStatusLocaleHazards = 18000000000000040005UL;
        private const ulong RiftDangerRoomLevelLocaleStringBase = 18000000000000010000UL;
        private const int RiftDangerRoomLevelLocalizedLevelLimit = 10000;
        private static readonly PrototypeId RiftDangerRoomLevelWidgetPrototypeRef = (PrototypeId)7164846210465729875UL;
        private static readonly PrototypeId RiftDangerRoomQuotaWidgetPrototypeRef = (PrototypeId)1488507445230442250UL;
        private static readonly PrototypeId RiftDangerRoomTimerWidgetPrototypeRef = (PrototypeId)15369535438503023451UL;
        private const string RiftExitPortalPrototypeName = "Entity/Transitions/ReturnToLastBaseDR.prototype";
        private const string RiftRewardChestPrototypeName = "Entity/Props/Chests/DangerRoomChestTutorialRewardEntity.prototype";
        private const string DefaultRiftAffixTablePrototypeName = "Regions/Affixes/RegionAffixTable.defaults";
        private const string RiftCompletionVendorPrototypeName = "Entity/Characters/Vendors/Prototypes/Endgame/DangerRoomRewardsVendor.prototype";
        private const string RiftCompletionCrafterTypePrototypeName = "Entity/Characters/Vendors/VendorTypes/TestVendorCrafter.prototype";
        private static readonly PrototypeId RiftCompletionCrafterRecipePrototypeRef = (PrototypeId)9691334961261451315UL;
        private const string RiftCompletionCrafterCosmicRecipePrototypeName = "Entity/Items/Crafting/Recipes/Tab3Gear/RerollCosmicReplacement.prototype";
        private const int RiftCompletionCrafterAttemptsPerRun = 3;
        private const float RiftCompletionCrafterUpgradeChance = 0.30f;
        private const int RiftCompletionCrafterMinimumItemLevel = 69;
        private const int RiftCompletionCrafterCosmicMinimumItemLevel = 63;
        private const int RiftCompletionCrafterMaximumItemLevel = 75;
        private const string RiftCompletionCrafterCurrencyPrototypeName = "Entity/Items/CurrencyItems/CurrencyPrototypes/GenoshaRaidCurrency.prototype";
        private const float RiftCompletionCrafterSpawnOffset = 390f;
        private const string BossGauntletArenaContentId = "boss-gauntlet-tutorial-arena";
        private const float SpecialRandomMapChance = 0.05f;
        private const int MaxActiveRiftHazards = 4;
        private const float RiftHazardSpawnDistance = 420f;
        private const float RiftHazardSpawnSearchDistance = 260f;
        private static readonly string[] RiftHazardNameKeywords =
        {
            "hazard",
            "hotspot",
            "lava",
            "fire",
            "poison",
            "acid",
            "laser",
            "bomb",
            "explosion",
            "lightning"
        };
        private static readonly string[] RiftBossIconWidgetNameKeywords =
        {
            "boss",
            "entityicons",
            "health"
        };
        private static readonly HashSet<string> RandomCheckpointContentExclusions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] CustomRiftPopulationMobPrototypeNames =
        {
            "Entity/Characters/Mobs/EndGameRandoms01/ThugEG06.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/MaggiaGoonEG06.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/MaggiaBruiserEG06.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/HydraGunnerEG13.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/HydraPowerBrawlerEG13.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/HydraPlasmaCasterEG13.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/HandNinjaEG11.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/HandAssassinEG11.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/PurifierAcolyteEG10.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/PurifierGrenadierEG10.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/DoombotInfernoEG12.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/ServoGuardRangedEG12.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/BroodSoldierEG08.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/BroodFlyerEG08.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/RaptorEG04.prototype",
            "Entity/Characters/Mobs/EndGameRandoms01/MoloidEG01.prototype"
        };
        private static readonly MythicRiftContentDefinition[] DefaultContentDefinitions =
        {
            new(
                "shocker",
                "Shocker Terminal",
                45,
                "Regions/EndGame/Terminals/Green/ShockerSubway/AltRegions/DailyGShockerSubwayRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G01ShockerSubwayDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD01GShocker.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AbandonedSubway/ShockerTerminalLoot.prototype"),
            new(
                "doctor-octopus",
                "Doctor Octopus Terminal",
                50,
                "Regions/EndGame/Terminals/Green/KingpinsWarehouse/AltRegions/DailyGKPWarehouseRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G02DoctorOctopusDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD02GDoctorOctopus.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/KingpinsWarehouse/DrOctopusTerminalLoot.prototype"),
            new(
                "taskmaster",
                "Taskmaster Terminal",
                50,
                "Regions/EndGame/Terminals/Green/Taskmaster/AltRegions/DailyGTaskmasterRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G03TaskmasterDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD03GTaskmaster.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/TaskmasterInstitute/TaskmasterTerminalLoot.prototype"),
            new(
                "hood",
                "Hood Terminal",
                55,
                "Regions/EndGame/Terminals/Green/HoodsShip/AltRegions/DailyGHoodsShipRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G04HoodDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD04GHood.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HoodsHideout/HoodTerminalLoot.prototype"),
            new(
                "magneto",
                "Magneto Terminal",
                60,
                "Regions/EndGame/Terminals/Green/MagnetoBunker/AltRegions/DailyGStrykerBunkerRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G05MagnetoDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD05GMagneto.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "sinister",
                "Mister Sinister Terminal",
                60,
                "Regions/EndGame/Terminals/Green/SinistersLab/AltRegions/DailyGSinisterLabRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G06MisterSinisterDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD06GMrSinister.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/SinisterLab/MisterSinisterTerminalLoot.prototype"),
            new(
                "modok",
                "MODOK Terminal",
                60,
                "Regions/EndGame/Terminals/Green/AIMFacility/AltRegions/DailyGAIMFacilityRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G07MODOKDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD07GMODOK.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: true,
                RandomBossEligible: false),
            new(
                "mandarin",
                "Mandarin Terminal",
                65,
                "Regions/EndGame/Terminals/Green/HYDRAIsland/AltRegions/DailyGHYDRAIslandRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G08MandarinDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD08GMandarin.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype"),
            new(
                "kingpin",
                "Kingpin Terminal",
                65,
                "Regions/EndGame/Terminals/Green/FiskTower/AltRegions/DailyGFiskTowerRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G10FiskTowerDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD10GKingpin.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/FiskTower/KingpinLTerminalLoot.prototype"),
            new(
                "ultron",
                "Ultron Terminal",
                70,
                "Regions/EndGame/Terminals/Green/TimesSquare/AltRegions/DailyGTimesSquareRegionL60.prototype",
                "Missions/Prototypes/PVEEndgame/Dailies/Green/G14UltronDailyEndgame.prototype",
                "Entity/Characters/Bosses/PVEDailies/Green/EGD14GUltronTerminal.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/TimesSquare/UltronTerminalLoot.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "boss-pyro",
                "Pyro",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGD05GPyro.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-aim-doctor-octopus",
                "A.I.M. Doctor Octopus",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGD07GSBDoctorOctopus.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-wizard",
                "Wizard",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGD07GSBWizard.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-bullseye",
                "Bullseye",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGD10GSBBullseye.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/FiskTower/KingpinLTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-elektra",
                "Elektra",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGD10GSBElektra.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/FiskTower/KingpinLTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-black-cat",
                "Black Cat",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGDGSBBlackCat.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/KingpinsWarehouse/DrOctopusTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-blob",
                "Blob",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGDGSBBlob.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-green-goblin",
                "Green Goblin",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGDGSBGreenGoblin.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/KingpinsWarehouse/DrOctopusTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-rhino",
                "Rhino",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGDGSBRhino.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AbandonedSubway/ShockerTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-venom",
                "Venom",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PVEDailies/Green/EGDGSBVenom.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/TaskmasterInstitute/TaskmasterTerminalLoot.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "boss-midtown-doctor-doom",
                "Doctor Doom",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventDoctorDoom.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-electro",
                "Electro",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventElectro.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-gorgon",
                "Gorgon",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventGorgon.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-magneto",
                "Magneto",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventMagneto.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-malekith",
                "Malekith",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventMalekith.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-sentinel",
                "Sentinel",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/MidtownEventSentinel.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-black-cat",
                "Black Cat",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/SideBosses/MidtownEventBlackCat.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-midtown-mole-man",
                "Mole Man",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolMidtown/SideBosses/MidtownEventMoleMan.prototype",
                "Loot/Tables/Mob/Bosses/PatrolMidtown/Subtable/SharedPatrolMidtownBossesCosmic.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "boss-brooklyn-batroc",
                "Batroc",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolBrooklyn/SideBosses/BrooklynEventBatroc.prototype",
                "Loot/Tables/Mob/Bosses/PatrolBrooklyn/Subtable/SharedPatrolBrooklynBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-brooklyn-grim-reaper",
                "Grim Reaper",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolBrooklyn/BrooklynEventGrimReaper.prototype",
                "Loot/Tables/Mob/Bosses/PatrolBrooklyn/Subtable/SharedPatrolBrooklynBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-brooklyn-loki",
                "Loki",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolBrooklyn/BrooklynEventLoki.prototype",
                "Loot/Tables/Mob/Bosses/PatrolBrooklyn/Subtable/SharedPatrolBrooklynBossesCosmic.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "boss-brooklyn-mr-hyde",
                "Mister Hyde",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolBrooklyn/SideBosses/BrooklynEventMrHyde.prototype",
                "Loot/Tables/Mob/Bosses/PatrolBrooklyn/Subtable/SharedPatrolBrooklynBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-hightown-war-skrull",
                "War Skrull",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolHightown/HightownEventAvengersWarskrull.prototype",
                "Loot/Tables/Mob/Bosses/PatrolHightown/Subtable/SharedPatrolHightownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-hightown-cosmic-war-skrull",
                "Cosmic War Skrull",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolHightown/HightownEventCosmicWarskrull.prototype",
                "Loot/Tables/Mob/Bosses/PatrolHightown/Subtable/SharedPatrolHightownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-hightown-mindless-titan",
                "Mindless Titan",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolHightown/HightownEventIncursionMindlessTitan.prototype",
                "Loot/Tables/Mob/Bosses/PatrolHightown/Subtable/SharedPatrolHightownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-hightown-skrull-kingpin",
                "Skrull Kingpin",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolHightown/HightownEventIncursionSkrullKingpin.prototype",
                "Loot/Tables/Mob/Bosses/PatrolHightown/Subtable/SharedPatrolHightownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-hightown-skrull-thor",
                "Skrull Thor",
                1,
                null,
                null,
                "Entity/Characters/Bosses/PatrolHightown/HightownEventSkrullThor.prototype",
                "Loot/Tables/Mob/Bosses/PatrolHightown/Subtable/SharedPatrolHightownBossesCosmic.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-electro",
                "Electro",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomElectro.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-gorgon",
                "Gorgon",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomGorgon.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-juggernaut",
                "Juggernaut",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomJuggernaut.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-kraven",
                "Kraven",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomKraven.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/TaskmasterInstitute/TaskmasterTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-kurse",
                "Kurse",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomKurse.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-living-laser",
                "Living Laser",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomLivingLaser.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-lizard",
                "Lizard",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomLizard.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/KingpinsWarehouse/DrOctopusTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-madame-hydra",
                "Madame Hydra",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomMadameHydra.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-magneto",
                "Magneto",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomMagneto.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-malekith",
                "Malekith",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomMalekith.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype",
                RandomMapEligible: false,
                RandomBossEligible: false),
            new(
                "boss-dr-man-ape",
                "Man-Ape",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomManApe.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/HydraIsland/MandarinTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-mega-sentinel",
                "Mega-Sentinel",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomMegaSentinel.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/AIMWeaponFacility/ModokTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-mister-hyde",
                "Mister Hyde",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomMrHyde.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/KingpinsWarehouse/DrOctopusTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-sabretooth",
                "Sabretooth",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomSabertooth.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-sauron",
                "Sauron",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomSauron.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/StrykerCommandBunker/MagnetoTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "boss-dr-tombstone",
                "Tombstone",
                1,
                null,
                null,
                "Entity/Characters/Bosses/DangerRoom/DangerRoomTombstone.prototype",
                "Loot/Tables/Mob/Bosses/EndgameDailies/Terminals/FiskTower/KingpinLTerminalLoot.prototype",
                RandomMapEligible: false),
            new(
                "bronx-zoo",
                "Bronx Zoo",
                80,
                "Regions/EndGame/OneShotMissions/NonChapterBound/BronxZoo/AltRegions/BronxZooRegionL60.prototype",
                null,
                null,
                null,
                RandomBossEligible: false),
            new(
                "wakanda-jungle",
                "Wakanda Jungle",
                65,
                "Regions/EndGame/OneShotMissions/NonChapterBound/WakandaPart1/AltRegions/WakandaP1RegionL60.prototype",
                null,
                null,
                null,
                RandomBossEligible: false),
            new(
                "hydra-island-one-shot",
                "HYDRA Island One-Shot",
                65,
                "Regions/EndGame/OneShotMissions/NonChapterBound/HydraIslandPartDeux/AltRegions/HYDRAIslandPartDeuxRegionL60.prototype",
                null,
                null,
                null,
                RandomBossEligible: false),
            new(
                "midtown-patrol",
                "Midtown Manhattan Patrol",
                75,
                "Regions/EndGame/TierX/PatrolMidtown/AltRegions/XManhattanRegion60Cosmic.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "brooklyn-patrol",
                "Industry City Patrol",
                75,
                "Regions/EndGame/TierX/PatrolBrooklyn/AltRegions/BrooklynPatrolRegionL60Cosmic.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "hightown-patrol",
                "Hightown Patrol",
                75,
                "Regions/EndGame/TierX/PatrolHightown/AltRegions/UpperMadripoorRegionL60Cosmic.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "savage-land-patrol",
                "Savage Land Patrol",
                75,
                "Regions/EndGame/TierX/PatrolSavage/PatrolSavageRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "brooklyn-docks-story",
                "Brooklyn Docks",
                60,
                "Regions/Story/CH02JerseyDocks/BrooklynRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "brooklyn-shipping-yard",
                "Brooklyn Shipping Yard",
                55,
                "Regions/StoryRevamp/CH02JerseyDocks/CH0201ShippingYardRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "brooklyn-construction",
                "Brooklyn Construction Site",
                55,
                "Regions/StoryRevamp/CH02JerseyDocks/CH0205ConstructionRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "brooklyn-cannery",
                "Brooklyn Cannery",
                55,
                "Regions/StoryRevamp/CH02JerseyDocks/CH0208CanneryRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "upper-madripoor-story",
                "Upper Madripoor",
                75,
                "Regions/Story/CH10SecretInvasion/UpperMadripoor/UpperMadripoorRegionL60.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                UseCustomPopulation: true),
            new(
                "holiday-midtown",
                "Holiday Midtown",
                75,
                "Regions/EndGame/TierX/Seasonal/Christmas/Midtown/AVeryXManhattanXMasRegion01.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseCustomPopulation: true),
            new(
                "holiday-industry-city",
                "Holiday Industry City",
                75,
                "Regions/EndGame/TierX/Seasonal/Christmas/IndustryCity/BrooklynPatrolRegionWinterTest.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseCustomPopulation: true),
            new(
                "dr-strange-times-square",
                "Doctor Strange Times Square",
                45,
                "Regions/EndGame/StaticScenarios/DrStrangeEvent/Cosmic/DrStrangeTimesSquareRegionCosmic.prototype",
                null,
                null,
                null,
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseCustomPopulation: true),
            new(
                "march-to-axis",
                "March to Axis",
                75,
                "Regions/RAIDS/AxisRaid/AxisRaidRegionGreen.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseCustomPopulation: true,
                MinRandomRiftLevel: 15),
            new(
                "muspelheim-raid",
                "Muspelheim Raid",
                75,
                "Regions/RAIDS/MuspelheimRaid/SurturRaidRegionGreen.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseCustomPopulation: true,
                MinRandomRiftLevel: 15),
            new(
                "cosmic-doop-sector",
                "Cosmic Doop Sector",
                100,
                "Regions/EndGame/Special/CosmicDoopSectorSpace/CosmicDoopSectorSpaceRegion.prototype",
                "Missions/Prototypes/BonusMissions/OMDoopZone.prototype",
                "Entity/Characters/Mobs/DoopAllChapters/CosmicDoop/CosmicDoopOverlord.prototype",
                "Loot/Tables/Mob/NormalMobs/CosmicDoopOverlordTable.prototype",
                RandomBossEligible: false,
                IsSpecialRandomMap: true,
                UseOwnBossSourceWhenSelected: true,
                MinRandomRiftLevel: 25),
            new(
                BossGauntletArenaContentId,
                "Danger Room Tutorial Arena",
                1,
                "Regions/EndGame/DangerRoomMode/UniqueScenarios/Tutorial/DRRegionUniqueTutorialFight.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sabretooth-showdown",
                "Sabretooth Showdown",
                45,
                "Regions/StoryRevamp/CH07SavageLand/CH0705SabretoothShowdownRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "supervillain-rec-center",
                "Supervillain Rec Center",
                45,
                "Regions/StoryRevamp/CH05MutantTown/CH0503SupervillainRecCenterRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sc-kill-house",
                "Stryker Kill House",
                50,
                "Regions/StoryRevamp/CH06FortStryker/TreasureRooms/TRKillHouse/SCKillHouseRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sc-missile-silo",
                "Stryker Missile Silo",
                50,
                "Regions/StoryRevamp/CH06FortStryker/TreasureRooms/TRMissileSilo/SCMissileSiloRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sc-mineshaft",
                "Stryker Mineshaft",
                45,
                "Regions/StoryRevamp/CH06FortStryker/TreasureRooms/TRMineshaft/SCMineshaftRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sc-dino-graveyard",
                "Dino Graveyard",
                55,
                "Regions/StoryRevamp/CH07SavageLand/TreasureRooms/TRDinoGraveyard/SCDinoGraveyardRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "sc-fire-swamp",
                "Fire Swamp",
                55,
                "Regions/StoryRevamp/CH07SavageLand/TreasureRooms/TRFireSwamp/SCFireSwampRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "tr-asgard-estate",
                "Asgard Estate",
                45,
                "Regions/StoryRevamp/CH09Asgard/TreasureRooms/TRAsgardEstate/TREstateRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "tr-norway-tomb",
                "Norway Tomb",
                45,
                "Regions/StoryRevamp/CH09Asgard/TreasureRooms/TRNorwayTomb/TRTombRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
            new(
                "tr-sacred-dojo",
                "Sacred Dojo",
                40,
                "Regions/StoryRevamp/CH03Madripoor/TreasureRooms/BambooForest/SacredDojo/TRSacredDojoRegion.prototype",
                null,
                null,
                null,
                RandomMapEligible: false,
                RandomBossEligible: false,
                BossOnlyCheckpointEligible: true),
        };

        private readonly List<MythicRiftContentEntry> _contentPool = new();
        private readonly Dictionary<ulong, MythicRiftRunState> _activeRuns = new();
        private readonly Dictionary<ulong, Event<EntityDeadGameEvent>.Action> _regionEntityDeadActions = new();
        private readonly Dictionary<ulong, int> _highestUnlockedRiftLevelByPlayer = new();
        private readonly Dictionary<ulong, int> _highestUnlockedEndlessRiftLevelByPlayer = new();
        private readonly Dictionary<ulong, int> _preferredLaunchRiftLevelByPlayer = new();
        private readonly Dictionary<ulong, int> _preferredLaunchEndlessRiftLevelByPlayer = new();
        private readonly Dictionary<ulong, string> _lastCompletedMapContentIdByPlayer = new();
        private readonly Dictionary<ulong, List<string>> _recentRandomMapContentIdsByPlayer = new();
        private readonly Dictionary<ulong, List<string>> _recentRandomBossFamiliesByPlayer = new();
        private readonly Dictionary<ulong, TimeSpan> _nextNativeBossSuppressionScanAt = new();
        private readonly Dictionary<ulong, TimeSpan> _nextRiftObjectiveWidgetRefreshAt = new();
        private readonly Dictionary<ulong, TimeSpan> _pendingFailedRunEvacuationsAt = new();
        private readonly Dictionary<ulong, TimeSpan> _pendingBossGauntletFailureRecoveriesAt = new();
        private readonly Dictionary<ulong, TimeSpan> _nextCheckpointBossSpawnRetryAt = new();
        private readonly Dictionary<ulong, HashSet<Mission>> _serverSuspendedNativeObjectiveMissionsByRun = new();
        private readonly Dictionary<string, IReadOnlyList<PrototypeId>> _rewardItemPoolsByDirectory = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ulong, CompletionCrafterOpportunity> _completionCrafterOpportunitiesByPlayer = new();
        private readonly Dictionary<ulong, PendingRewardChest> _pendingRewardChestsByEntityId = new();
        private readonly Dictionary<ulong, Event<PlayerInteractGameEvent>.Action> _regionPlayerInteractActions = new();
        private static PrototypeId _cachedRiftDangerRoomLevelWidgetPrototypeRef = PrototypeId.Invalid;
        private static PrototypeId _cachedRiftDangerRoomQuotaWidgetPrototypeRef = PrototypeId.Invalid;
        private static PrototypeId _cachedRiftDangerRoomTimerWidgetPrototypeRef = PrototypeId.Invalid;
        private static PrototypeId _cachedRiftReadyCheckWidgetPrototypeRef = PrototypeId.Invalid;
        private static PrototypeId _cachedRiftBossIconsWidgetPrototypeRef = PrototypeId.Invalid;
        private static PrototypeId[] _cachedCustomRiftPopulationMobPrototypeRefs;
        private static IReadOnlyList<PrototypeId> _cachedRiftHazardPrototypeRefs;
        private MythicRiftRewardTuning _rewardTuning = MythicRiftRewardTuning.CreateDefault();
        private string _rewardTuningLastLoadMessage = "Using built-in default Mythic Rift reward tuning.";
        private ulong _nextRunId = 1;

        private sealed class CompletionCrafterOpportunity
        {
            public ulong RunId { get; init; }
            public int AttemptsRemaining { get; set; }
            public bool LockedOut { get; set; }
        }

        public Game Game { get; }

        public MythicRiftManager(Game game)
        {
            Game = game;
            RegisterDefaultContent();
            TryReloadRewardTuning(out _rewardTuningLastLoadMessage);
            Logger.Info($"Mythic Rift reward tuning: {_rewardTuningLastLoadMessage}");
        }

        public IReadOnlyList<MythicRiftContentEntry> ContentPool => _contentPool;
        public IReadOnlyList<MythicRiftContentEntry> RandomEligibleContentPool => RandomMapEligibleContentPool;
        public IReadOnlyList<MythicRiftContentEntry> RandomMapEligibleContentPool => _contentPool.Where(entry => entry.RandomMapEligible).ToList();
        public IReadOnlyList<MythicRiftContentEntry> RandomBossEligibleContentPool => _contentPool.Where(entry => entry.RandomBossEligible && entry.HasValidBossSource).ToList();
        public IReadOnlyCollection<MythicRiftRunState> ActiveRuns => _activeRuns.Values;
        public MythicRiftRewardTuning RewardTuning => _rewardTuning;
        public string RewardTuningLastLoadMessage => _rewardTuningLastLoadMessage;
        public MythicRiftDifficultySnapshot GetDifficultySnapshot(
            int riftLevel,
            int requestedPlayerCount,
            MythicRiftMode mode = MythicRiftMode.Standard)
        {
            return MythicRiftScaling.BuildSnapshot(riftLevel, requestedPlayerCount, mode);
        }

        public int GetHighestUnlockedRiftLevel(ulong playerDbId, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return 1;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            Dictionary<ulong, int> unlockedCache = GetHighestUnlockedCache(mode);
            Player onlinePlayer = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (onlinePlayer != null)
            {
                int persistentLevel = NormalizeStoredRiftLevel(GetPersistentUnlockedRiftLevel(onlinePlayer, mode), mode);
                if (unlockedCache.TryGetValue(playerDbId, out int cachedUnlockedLevel))
                    return Math.Max(NormalizeStoredRiftLevel(cachedUnlockedLevel, mode), persistentLevel);

                return persistentLevel;
            }

            return unlockedCache.TryGetValue(playerDbId, out int unlockedLevel)
                ? NormalizeStoredRiftLevel(unlockedLevel, mode)
                : 1;
        }

        public bool CanAccessRiftLevel(ulong playerDbId, int riftLevel, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (riftLevel <= 0)
                return false;

            if (mode == MythicRiftMode.Endless && riftLevel > MythicRiftProgression.EndlessCycleLength)
                return false;

            if (mode == MythicRiftMode.BossGauntlet)
                return riftLevel == 1;

            return riftLevel <= GetHighestUnlockedRiftLevel(playerDbId, mode);
        }

        public int GetPreferredLaunchRiftLevel(ulong playerDbId, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            int highestUnlockedLevel = GetHighestUnlockedRiftLevel(playerDbId, mode);
            if (playerDbId == 0)
                return highestUnlockedLevel;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            Dictionary<ulong, int> preferredCache = GetPreferredLaunchCache(mode);
            if (preferredCache.TryGetValue(playerDbId, out int preferredLevel) == false || preferredLevel <= 0)
                return highestUnlockedLevel;

            return Math.Min(Math.Max(preferredLevel, 1), highestUnlockedLevel);
        }

        public bool TrySetPreferredLaunchRiftLevel(
            ulong playerDbId,
            int riftLevel,
            out int appliedLevel,
            out string errorMessage,
            MythicRiftMode mode = MythicRiftMode.Standard)
        {
            appliedLevel = GetPreferredLaunchRiftLevel(playerDbId, mode);
            errorMessage = string.Empty;

            if (playerDbId == 0)
            {
                errorMessage = "Player not found.";
                return false;
            }

            if (mode == MythicRiftMode.BossGauntlet)
            {
                appliedLevel = 1;
                return true;
            }

            if (riftLevel <= 0)
            {
                errorMessage = "Rift level must be greater than zero.";
                return false;
            }

            if (mode == MythicRiftMode.Endless && riftLevel > MythicRiftProgression.EndlessCycleLength)
            {
                errorMessage = $"{GetModeDisplayName(mode)} loops after level {MythicRiftProgression.EndlessCycleLength}. Requested level: {riftLevel}.";
                return false;
            }

            int highestUnlockedLevel = GetHighestUnlockedRiftLevel(playerDbId, mode);
            if (riftLevel > highestUnlockedLevel)
            {
                errorMessage = $"Requested {GetModeDisplayName(mode)} level {riftLevel} is locked. Highest unlocked level: {highestUnlockedLevel}.";
                return false;
            }

            GetPreferredLaunchCache(mode)[playerDbId] = riftLevel;
            appliedLevel = riftLevel;
            return true;
        }

        public int UseHighestUnlockedLaunchRiftLevel(ulong playerDbId, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return 1;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            GetPreferredLaunchCache(mode).Remove(playerDbId);
            return GetHighestUnlockedRiftLevel(playerDbId, mode);
        }

        public bool ConsumePreferredLaunchRiftLevel(ulong playerDbId, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return false;

            if (mode == MythicRiftMode.BossGauntlet)
                return false;

            return GetPreferredLaunchCache(mode).Remove(playerDbId);
        }

        public int SetHighestUnlockedRiftLevel(
            ulong playerDbId,
            int unlockedLevel,
            bool allowDecrease = false,
            MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return 1;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            int normalizedLevel = NormalizeStoredRiftLevel(unlockedLevel, mode);
            if (allowDecrease == false)
                normalizedLevel = Math.Max(normalizedLevel, GetHighestUnlockedRiftLevel(playerDbId, mode));

            GetHighestUnlockedCache(mode)[playerDbId] = normalizedLevel;
            SyncOnlinePlayerRiftLevel(playerDbId, normalizedLevel, mode);
            return normalizedLevel;
        }

        public int ResetRiftProgress(ulong playerDbId, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return 1;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            GetPreferredLaunchCache(mode).Remove(playerDbId);
            return SetHighestUnlockedRiftLevel(playerDbId, 1, allowDecrease: true, mode: mode);
        }

        public int GrantNextRiftLevel(ulong playerDbId, int completedLevel, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (playerDbId == 0)
                return 1;

            if (mode == MythicRiftMode.BossGauntlet)
                return 1;

            int currentUnlockedLevel = GetHighestUnlockedRiftLevel(playerDbId, mode);
            int nextUnlockedLevel = mode == MythicRiftMode.Endless
                ? MythicRiftProgression.ResolveNextEndlessCycleLevel(currentUnlockedLevel, completedLevel)
                : MythicRiftProgression.ResolveNextUnlockedLevel(currentUnlockedLevel, completedLevel);

            if (nextUnlockedLevel == currentUnlockedLevel)
                return currentUnlockedLevel;

            GetHighestUnlockedCache(mode)[playerDbId] = nextUnlockedLevel;
            GetPreferredLaunchCache(mode).Remove(playerDbId);
            SyncOnlinePlayerRiftLevel(playerDbId, nextUnlockedLevel, mode);
            return nextUnlockedLevel;
        }

        private static int NormalizeStoredRiftLevel(int riftLevel, MythicRiftMode mode)
        {
            return mode == MythicRiftMode.Endless
                ? MythicRiftProgression.NormalizeEndlessCycleLevel(riftLevel)
                : Math.Max(riftLevel, 1);
        }

        private Dictionary<ulong, int> GetHighestUnlockedCache(MythicRiftMode mode)
        {
            return mode == MythicRiftMode.Endless
                ? _highestUnlockedEndlessRiftLevelByPlayer
                : _highestUnlockedRiftLevelByPlayer;
        }

        private Dictionary<ulong, int> GetPreferredLaunchCache(MythicRiftMode mode)
        {
            return mode == MythicRiftMode.Endless
                ? _preferredLaunchEndlessRiftLevelByPlayer
                : _preferredLaunchRiftLevelByPlayer;
        }

        private static int GetPersistentUnlockedRiftLevel(Player player, MythicRiftMode mode)
        {
            if (player == null)
                return 1;

            return mode switch
            {
                MythicRiftMode.Endless => player.EndlessRiftHighestUnlockedLevel,
                MythicRiftMode.BossGauntlet => 1,
                _ => player.MythicRiftHighestUnlockedLevel
            };
        }

        public static string GetModeDisplayName(MythicRiftMode mode)
        {
            return mode switch
            {
                MythicRiftMode.Endless => "Rift Gauntlet",
                MythicRiftMode.BossGauntlet => "Boss Gauntlet",
                _ => "Cosmic Rift"
            };
        }

        public MythicRiftRunConfig CreateDebugRunConfig(
            string contentId,
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftContentEntry content = GetContent(contentId);
            if (content == null)
                return null;

            MythicRiftContentEntry bossContent = SelectBossContentForFixedMap(content, excludedBossFamilies);

            return CreateRunConfig(content, bossContent, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);
        }

        public MythicRiftRunConfig CreateDebugRunConfig(
            string contentId,
            string bossContentId,
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftContentEntry content = GetContent(contentId);
            MythicRiftContentEntry bossContent = GetContent(bossContentId);
            if (content == null || bossContent == null)
                return null;

            if (bossContent.HasValidBossSource == false)
                return null;

            return CreateRunConfig(content, bossContent, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);
        }

        public MythicRiftRunConfig CreateRandomDebugRunConfig(
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            IReadOnlyCollection<string> excludedMapContentIds = null,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftContentEntry content = mode == MythicRiftMode.BossGauntlet
                ? SelectBossGauntletMapContent(requestedPlayerCount, excludedMapContentIds)
                : SelectRandomMapContent(riftLevel, requestedPlayerCount, excludedMapContentIds);
            MythicRiftContentEntry bossContent = SelectBossContentForRandomMap(content, excludedBossFamilies);
            if (content == null || bossContent == null)
                return null;

            return CreateRunConfig(content, bossContent, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);
        }

        private MythicRiftContentEntry SelectBossGauntletMapContent(int requestedPlayerCount, IReadOnlyCollection<string> excludedContentIds = null)
        {
            MythicRiftContentEntry arenaContent = GetContent(BossGauntletArenaContentId);
            if (arenaContent?.HasValidMap == true && arenaContent.SupportsPlayerCount(requestedPlayerCount))
                return arenaContent;

            List<MythicRiftContentEntry> eligibleContent = _contentPool
                .Where(entry => entry.BossOnlyCheckpointEligible &&
                                entry.SupportsPlayerCount(requestedPlayerCount) &&
                                RandomCheckpointContentExclusions.Contains(entry.Id) == false)
                .ToList();

            if (eligibleContent.Count == 0)
                eligibleContent = _contentPool
                    .Where(entry => entry.RandomMapEligible &&
                                    entry.SupportsPlayerCount(requestedPlayerCount))
                    .ToList();

            if (eligibleContent.Count == 0)
                return null;

            if (excludedContentIds != null && excludedContentIds.Count > 0 && eligibleContent.Count > excludedContentIds.Count)
            {
                List<MythicRiftContentEntry> filteredContent = eligibleContent
                    .Where(entry => excludedContentIds.Any(excludedId => string.Equals(excludedId, entry.Id, StringComparison.OrdinalIgnoreCase)) == false)
                    .ToList();

                if (filteredContent.Count > 0)
                    eligibleContent = filteredContent;
            }

            return PickRandomContent(eligibleContent);
        }

        public MythicRiftRunState CreateRunState(MythicRiftRunConfig config)
        {
            if (config == null || config.IsValid == false)
                return null;

            return new MythicRiftRunState(config);
        }

        public MythicRiftRunState CreateDebugRun(
            string contentId,
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftRunConfig config = CreateDebugRunConfig(contentId, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);
            return RegisterRun(config);
        }

        public MythicRiftRunState CreateDebugRun(
            string contentId,
            string bossContentId,
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftRunConfig config = CreateDebugRunConfig(contentId, bossContentId, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);
            return RegisterRun(config);
        }

        public MythicRiftRunState CreateRandomDebugRun(
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            IReadOnlyCollection<string> excludedMapContentIds = null,
            MythicRiftMode mode = MythicRiftMode.Standard,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            MythicRiftRunConfig config = CreateRandomDebugRunConfig(riftLevel, requestedPlayerCount, killQuota, timeLimit, excludedMapContentIds, mode, excludedBossFamilies);
            return RegisterRun(config);
        }

        // Skips the actual Rift region entirely and runs the real success-completion pipeline (rewards,
        // progression, completion crafter spawn) against the player's current region, so testers can reach
        // the completion crafter without playing a full run.
        public bool DebugCompleteRunForPlayer(Player player, out string errorMessage)
        {
            errorMessage = string.Empty;

            Region region = player?.CurrentAvatar?.Region;
            if (region == null)
            {
                errorMessage = "Player not found or not currently in a region.";
                return false;
            }

            MythicRiftRunState runState = CreateRandomDebugRun(riftLevel: 1, requestedPlayerCount: 1, killQuota: 1, timeLimit: TimeSpan.FromMinutes(30));
            if (runState == null)
            {
                errorMessage = "Failed to create a debug Mythic Rift run.";
                return false;
            }

            runState.AttachRegion(region.Id);
            runState.RegisterParticipant(player.DatabaseUniqueId);
            runState.Start(Game.CurrentTime);
            if (runState.Status != MythicRiftRunStatus.Active)
            {
                errorMessage = "Failed to start the debug run.";
                return false;
            }

            if (CompleteRunSuccess(runState, Game.CurrentTime) == false)
            {
                errorMessage = "Failed to complete the debug run.";
                return false;
            }

            Logger.Info($"Mythic Rift debug run {runState.Config.RunId} force-completed for playerDbId=0x{player.DatabaseUniqueId:X} in regionId=0x{region.Id:X}.");
            return true;
        }

        public MythicRiftRunState RequestRun(Player player, int riftLevel, int killQuota, TimeSpan timeLimit, out string errorMessage)
        {
            return RequestRun(player, riftLevel, killQuota, timeLimit, MythicRiftMode.Standard, out errorMessage);
        }

        public MythicRiftRunState RequestRun(
            Player player,
            int riftLevel,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode,
            out string errorMessage)
        {
            return RequestRunInternal(player, null, riftLevel, killQuota, timeLimit, mode, useRandomContent: true, out errorMessage);
        }

        public MythicRiftRunState RequestFixedRun(Player player, string contentId, int riftLevel, int killQuota, TimeSpan timeLimit, out string errorMessage)
        {
            return RequestFixedRun(player, contentId, riftLevel, killQuota, timeLimit, MythicRiftMode.Standard, out errorMessage);
        }

        public MythicRiftRunState RequestFixedRun(
            Player player,
            string contentId,
            int riftLevel,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode,
            out string errorMessage)
        {
            return RequestRunInternal(player, contentId, riftLevel, killQuota, timeLimit, mode, useRandomContent: false, out errorMessage);
        }

        public MythicRiftRunState GetRun(ulong runId)
        {
            if (runId == 0)
                return null;

            return _activeRuns.TryGetValue(runId, out MythicRiftRunState runState) ? runState : null;
        }

        public MythicRiftRunState GetInProgressRunForPlayer(ulong playerDbId)
        {
            if (playerDbId == 0)
                return null;

            return _activeRuns.Values.FirstOrDefault(runState =>
                runState.IsInProgress &&
                runState.ParticipantPlayerDbIds.Contains(playerDbId));
        }

        public bool CanLaunchFromCompletedRiftRegion(Player player)
        {
            if (player == null || player.DatabaseUniqueId == 0)
                return false;

            Region region = player.GetRegion();
            if (region == null)
                return false;

            return _activeRuns.Values.Any(runState =>
                runState.Status == MythicRiftRunStatus.Success &&
                runState.RegionId == region.Id &&
                runState.HasParticipantLeftEarly(player.DatabaseUniqueId) == false &&
                runState.ParticipantPlayerDbIds.Contains(player.DatabaseUniqueId));
        }

        public bool RemoveRun(ulong runId)
        {
            if (runId == 0)
                return false;

            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            ulong regionId = runState.RegionId;
            SendStopRiftTimer(runState);
            TryRestoreRegionDifficultyScaling(runState);
            RestoreSuspendedNativeObjectiveMissions(runState);
            RequestRunRegionShutdownWhenVacant(runState);
            bool removed = _activeRuns.Remove(runId);

            if (removed && regionId != 0)
                CleanupRegionListener(regionId);

            if (removed)
            {
                _nextNativeBossSuppressionScanAt.Remove(runId);
                _nextRiftObjectiveWidgetRefreshAt.Remove(runId);
                _nextCheckpointBossSpawnRetryAt.Remove(runId);
                _serverSuspendedNativeObjectiveMissionsByRun.Remove(runId);
                _pendingFailedRunEvacuationsAt.Remove(runId);
                _pendingBossGauntletFailureRecoveriesAt.Remove(runId);
                CleanupRunHazards(runState);
                CleanupRewardChests(runId);
                CleanupCompletionCrafterOpportunities(runId);
            }

            return removed;
        }

        public int CompletionCrafterMinimumItemLevel => RiftCompletionCrafterMinimumItemLevel;
        public int CompletionCrafterCosmicMinimumItemLevel => RiftCompletionCrafterCosmicMinimumItemLevel;
        public int CompletionCrafterMaximumItemLevel => RiftCompletionCrafterMaximumItemLevel;

        // The recipes' own native CraftingCost fields (CostEvalCredits/CostEvalCurrencies) are never read for
        // this flow - TryHandleMythicRiftCompletionCraft() in Player.Crafting.cs bypasses the whole native
        // GetCraftingCost()/CraftPayCost() pipeline entirely, so this cost has to be checked/charged directly
        // in that custom handler instead of via a data patch to the recipe prototypes.
        public PrototypeId CompletionCrafterCurrencyProtoRef => ResolvePrototype(RiftCompletionCrafterCurrencyPrototypeName);

        // Costs are editable per-recipe in CosmicRiftRewards.json (CompletionCrafterUniqueRecipeCost /
        // CompletionCrafterCosmicRecipeCost) so testers can retune them without a rebuild.
        public uint GetCompletionCrafterCurrencyCost(PrototypeId recipeProtoRef)
        {
            MythicRiftRewardTuning tuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
            return (uint)(IsCosmicCompletionCrafterRecipe(recipeProtoRef)
                ? tuning.CompletionCrafterCosmicRecipeCost
                : tuning.CompletionCrafterUniqueRecipeCost);
        }
        public IReadOnlyList<PrototypeId> CompletionCrafterRecipePrototypeRefs
        {
            get
            {
                List<PrototypeId> recipeProtoRefs = new() { RiftCompletionCrafterRecipePrototypeRef };
                PrototypeId cosmicRecipeProtoRef = ResolvePrototype(RiftCompletionCrafterCosmicRecipePrototypeName);
                if (cosmicRecipeProtoRef != PrototypeId.Invalid)
                    recipeProtoRefs.Add(cosmicRecipeProtoRef);

                return recipeProtoRefs;
            }
        }

        public bool IsCompletionCrafter(WorldEntity vendor)
        {
            if (vendor == null)
                return false;

            return _activeRuns.Values.Any(run =>
                run != null &&
                run.CompletionCrafterEntityId != 0 &&
                run.CompletionCrafterEntityId == vendor.Id &&
                run.Status == MythicRiftRunStatus.Success);
        }

        public bool IsCompletionCrafterType(PrototypeId vendorTypeProtoRef)
        {
            if (vendorTypeProtoRef == PrototypeId.Invalid)
                return false;

            PrototypeId completionCrafterTypeProtoRef = ResolvePrototype(RiftCompletionCrafterTypePrototypeName);
            return completionCrafterTypeProtoRef != PrototypeId.Invalid && vendorTypeProtoRef == completionCrafterTypeProtoRef;
        }

        public bool IsCompletionCrafterRecipe(PrototypeId recipeProtoRef)
        {
            if (recipeProtoRef == PrototypeId.Invalid)
                return false;

            if (recipeProtoRef == RiftCompletionCrafterRecipePrototypeRef)
                return true;

            PrototypeId cosmicRecipeProtoRef = ResolvePrototype(RiftCompletionCrafterCosmicRecipePrototypeName);
            return cosmicRecipeProtoRef != PrototypeId.Invalid && recipeProtoRef == cosmicRecipeProtoRef;
        }

        public bool IsCosmicCompletionCrafterRecipe(PrototypeId recipeProtoRef)
        {
            if (recipeProtoRef == PrototypeId.Invalid)
                return false;

            PrototypeId cosmicRecipeProtoRef = ResolvePrototype(RiftCompletionCrafterCosmicRecipePrototypeName);
            return cosmicRecipeProtoRef != PrototypeId.Invalid && recipeProtoRef == cosmicRecipeProtoRef;
        }

        public bool TryResolveCompletionCrafterRun(WorldEntity crafter, ulong playerDbId, out MythicRiftRunState runState)
        {
            runState = null;
            if (crafter == null || playerDbId == 0 || IsCompletionCrafter(crafter) == false)
                return false;

            foreach (MythicRiftRunState candidate in _activeRuns.Values)
            {
                if (candidate == null ||
                    candidate.CompletionCrafterEntityId != crafter.Id ||
                    candidate.Status != MythicRiftRunStatus.Success ||
                    candidate.IsRewardEligible(playerDbId) == false)
                {
                    continue;
                }

                runState = candidate;
                return true;
            }

            return false;
        }

        public int GetCompletionCrafterAttemptsRemaining(ulong playerDbId)
        {
            return TryGetCompletionCrafterOpportunity(playerDbId, out CompletionCrafterOpportunity opportunity)
                ? Math.Max(opportunity.AttemptsRemaining, 0)
                : 0;
        }

        public bool TrySpendCompletionCrafterAttempt(
            Player player,
            out bool upgraded,
            out int attemptsRemaining,
            out string failureReason)
        {
            upgraded = false;
            attemptsRemaining = 0;
            failureReason = null;

            if (player == null || player.DatabaseUniqueId == 0)
            {
                failureReason = "No eligible player was found for the completion crafter.";
                return false;
            }

            if (TryGetCompletionCrafterOpportunity(player.DatabaseUniqueId, out CompletionCrafterOpportunity opportunity) == false)
            {
                failureReason = "No active Rift completion crafter attempts are available. Clear a Rift to unlock three attempts.";
                return false;
            }

            if (opportunity.LockedOut)
            {
                failureReason = "This Rift completion crafter already succeeded for you. Clear another Rift for a new set of attempts.";
                return false;
            }

            if (opportunity.AttemptsRemaining <= 0)
            {
                failureReason = "No Rift completion crafter attempts remain from this run.";
                return false;
            }

            opportunity.AttemptsRemaining--;
            upgraded = Game.Random.NextFloat() < RiftCompletionCrafterUpgradeChance;
            if (upgraded)
            {
                opportunity.LockedOut = true;
                opportunity.AttemptsRemaining = 0;
            }

            attemptsRemaining = Math.Max(opportunity.AttemptsRemaining, 0);
            return true;
        }

        private bool TryGetCompletionCrafterOpportunity(ulong playerDbId, out CompletionCrafterOpportunity opportunity)
        {
            opportunity = null;
            if (playerDbId == 0 ||
                _completionCrafterOpportunitiesByPlayer.TryGetValue(playerDbId, out opportunity) == false ||
                opportunity == null)
            {
                return false;
            }

            MythicRiftRunState runState = GetRun(opportunity.RunId);
            if (runState == null || runState.Status != MythicRiftRunStatus.Success || runState.IsRewardEligible(playerDbId) == false)
            {
                _completionCrafterOpportunitiesByPlayer.Remove(playerDbId);
                opportunity = null;
                return false;
            }

            return true;
        }

        private void CleanupCompletionCrafterOpportunities(ulong runId)
        {
            if (runId == 0 || _completionCrafterOpportunitiesByPlayer.Count == 0)
                return;

            foreach (ulong playerDbId in _completionCrafterOpportunitiesByPlayer
                         .Where(kvp => kvp.Value?.RunId == runId)
                         .Select(kvp => kvp.Key)
                         .ToList())
            {
                _completionCrafterOpportunitiesByPlayer.Remove(playerDbId);
            }
        }

        public bool StartRun(ulong runId, TimeSpan currentTime)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            runState.Start(currentTime);
            if (runState.Status == MythicRiftRunStatus.Active)
            {
                if (runState.Config.UseBossGauntletMode || runState.Config.Content.BossOnlyCheckpointEligible)
                    QueueRiftReadyCheck(runState, currentTime, runState.Config.UseBossGauntletMode ? $"Boss Gauntlet wave {runState.Config.WaveNumber}" : "Checkpoint boss wave");

                SendStartRiftTimer(runState);
                SuppressNativeTerminalBosses(runState, currentTime, force: true);
                SuppressNativeCheckpointPopulation(runState);
                RefreshRiftObjectiveWidgets(runState, currentTime, force: true);
                NotifyRunStarted(runState);
                TryStartBossGauntletWave(runState, currentTime);
                TryStartBossOnlyCheckpoint(runState, currentTime);
            }

            return runState.Status == MythicRiftRunStatus.Active;
        }

        public bool AddKills(ulong runId, int killCount)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null || killCount <= 0)
                return false;

            runState.AddKills(killCount);
            RefreshRiftHudWidgets(runState, Game.CurrentTime);
            return true;
        }

        public bool MarkRunSuccess(ulong runId, TimeSpan currentTime)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            return CompleteRunSuccess(runState, currentTime);
        }

        public bool MarkRunFailed(ulong runId, TimeSpan currentTime)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            return CompleteRunFailure(runState, currentTime, "Time expired. No completion rewards.", returnParticipantsToHub: true);
        }

        public bool MarkRunAborted(ulong runId, TimeSpan currentTime)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            return AbortRun(runState, currentTime, "The Rift was abandoned. A new Beacon is required to start another run.");
        }

        public int ReturnRunParticipantsToDangerRoomHub(MythicRiftRunState runState, Player fallbackPlayer = null, bool includePlayersAlreadyOutsideRunRegion = true)
        {
            if (runState == null || TryResolveDangerRoomHubStartTarget(out PrototypeId dangerRoomHubStartTarget) == false)
                return 0;

            HashSet<ulong> playerDbIds = new(runState.ParticipantPlayerDbIds);
            if (runState.RegionId != 0)
            {
                Region region = Game.RegionManager.GetRegion(runState.RegionId);
                if (region != null)
                {
                    foreach (Player regionPlayer in new PlayerIterator(region))
                    {
                        if (regionPlayer?.DatabaseUniqueId != 0)
                            playerDbIds.Add(regionPlayer.DatabaseUniqueId);
                    }
                }
            }

            if (playerDbIds.Count == 0 && fallbackPlayer?.DatabaseUniqueId != 0)
                playerDbIds.Add(fallbackPlayer.DatabaseUniqueId);

            int teleportedPlayerCount = 0;
            foreach (ulong playerDbId in playerDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                if (player?.CurrentAvatar?.IsInWorld != true)
                    continue;

                if (includePlayersAlreadyOutsideRunRegion == false && IsPlayerInRunRegion(player, runState) == false)
                    continue;

                if (EnsurePlayerAvatarAliveForRiftExit(player, runState, "return-to-hub") == false)
                    continue;

                using Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>();
                teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Resurrect);
                teleporter.DifficultyTierRef = GameDatabase.GlobalsPrototype.DifficultyTierDefault;
                if (teleporter.TeleportToTarget(dangerRoomHubStartTarget))
                    teleportedPlayerCount++;
            }

            return teleportedPlayerCount;
        }

        private static bool EnsurePlayerAvatarAliveForRiftExit(Player player, MythicRiftRunState runState, string context)
        {
            Avatar avatar = player?.CurrentAvatar;
            if (avatar == null || avatar.IsDead == false)
                return true;

            bool resurrected = avatar.Resurrect();
            if (avatar.IsDead == false)
            {
                Logger.Info($"Mythic Rift run {runState?.Config?.RunId ?? 0} cleared dead avatar state before {context} for playerDbId=0x{player.DatabaseUniqueId:X}. resurrectResult={resurrected}");
                return true;
            }

            Logger.Warn($"Mythic Rift run {runState?.Config?.RunId ?? 0} failed to resurrect dead avatar before {context} for playerDbId=0x{player.DatabaseUniqueId:X}.");
            return false;
        }

        public bool AbortRunWithReason(ulong runId, TimeSpan currentTime, string reason)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null)
                return false;

            return AbortRun(runState, currentTime, reason);
        }

        public bool GrantRewardsToPlayer(ulong runId, Player player)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null || player == null)
                return false;

            if (runState.Status is not (MythicRiftRunStatus.Success or MythicRiftRunStatus.Failed))
                return false;

            if (runState.IsRewardEligible(player.DatabaseUniqueId) == false)
                return false;

            if (runState.HasRewardForPlayer(player.DatabaseUniqueId))
                return false;

            MythicRiftRewardOutcome rewardOutcome = runState.RewardOutcome ?? ResolveRewardOutcome(runState);
            if (rewardOutcome == null || rewardOutcome.HasAnyLoot == false)
                return false;

            Avatar avatar = player.CurrentAvatar;
            if (avatar == null)
                return false;

            PropertyId rarityPropertyId = new(PropertyEnum.LootBonusRarityPct);
            PropertyId specialPropertyId = new(PropertyEnum.LootBonusSpecialPct);

            float originalRarity = avatar.Properties[PropertyEnum.LootBonusRarityPct];
            float originalSpecial = avatar.Properties[PropertyEnum.LootBonusSpecialPct];
            bool hadRarityProperty = avatar.Properties.HasProperty(PropertyEnum.LootBonusRarityPct);
            bool hadSpecialProperty = avatar.Properties.HasProperty(PropertyEnum.LootBonusSpecialPct);

            try
            {
                if (rewardOutcome.BonusRarityPct > 0f)
                    avatar.Properties.AdjustProperty(rewardOutcome.BonusRarityPct, rarityPropertyId);

                if (rewardOutcome.BonusSpecialPct > 0f)
                    avatar.Properties.AdjustProperty(rewardOutcome.BonusSpecialPct, specialPropertyId);

                using LootInputSettings inputSettings = MHServerEmu.Core.Memory.ObjectPoolManager.Instance.Get<LootInputSettings>();
                inputSettings.Initialize(LootContext.Drop, player, avatar);

                int groundRecipientId = 1;
                List<PendingRewardDrop> chestRewards = new();
                if (rewardOutcome.HasBossLootTable)
                {
                    if (MythicRiftRewardTuning.IsChestDelivery(rewardOutcome.BossLootDelivery))
                    {
                        chestRewards.Add(PendingRewardDrop.CreateLootTable(
                            rewardOutcome.BossLootTableProtoRef,
                            itemLevel: 0,
                            id: rewardOutcome.BossLootTableSourceId ?? "boss-loot"));
                    }
                    else
                    {
                        GrantRewardLootTable(rewardOutcome.BossLootTableProtoRef, inputSettings, rewardOutcome.BossLootDelivery, ref groundRecipientId);
                    }
                }

                foreach (MythicRiftRewardExtraLootTable extraLootTable in rewardOutcome.ExtraLootTables)
                {
                    for (int i = 0; i < extraLootTable.Rolls; i++)
                    {
                        if (extraLootTable.ChancePercent <= 0f ||
                            (extraLootTable.ChancePercent < 100f && Game.Random.NextFloat() * 100f >= extraLootTable.ChancePercent))
                        {
                            continue;
                        }

                        if (MythicRiftRewardTuning.IsChestDelivery(extraLootTable.Delivery))
                        {
                            chestRewards.Add(PendingRewardDrop.CreateLootTable(
                                extraLootTable.LootTableProtoRef,
                                extraLootTable.ItemLevel,
                                extraLootTable.Id));
                        }
                        else
                        {
                            GrantRewardLootTable(extraLootTable.LootTableProtoRef, inputSettings, extraLootTable.Delivery, ref groundRecipientId, extraLootTable.ItemLevel);
                        }
                    }
                }

                foreach (MythicRiftRewardGuaranteedItem guaranteedItem in rewardOutcome.GuaranteedItems)
                {
                    for (int i = 0; i < guaranteedItem.Quantity; i++)
                    {
                        if (MythicRiftRewardTuning.IsChestDelivery(guaranteedItem.Delivery))
                            chestRewards.Add(PendingRewardDrop.CreateGuaranteedItem(guaranteedItem));
                        else
                            GrantRewardItem(guaranteedItem, player, avatar);
                    }
                }

                if (chestRewards.Count > 0 &&
                    TrySpawnRewardChest(runState, player, avatar, chestRewards, rewardOutcome.BonusRarityPct, rewardOutcome.BonusSpecialPct) == false)
                {
                    Logger.Warn($"Mythic Rift run {runState.Config.RunId} failed to spawn reward chest for player {player}; falling back to ground delivery.");
                    GrantPendingRewardDrops(player, avatar, sourceEntity: avatar, chestRewards, bonusRarityPct: 0f, bonusSpecialPct: 0f);
                }

                runState.MarkRewardGrantedToPlayer(player.DatabaseUniqueId);
                Logger.Info($"Mythic Rift run {runState.Config.RunId} granted rewards to player {player}. profile={rewardOutcome.RewardProfileName ?? "default"} bossLootSource={rewardOutcome.BossLootTableSourceId ?? "native-boss"} bossDelivery={rewardOutcome.BossLootDelivery ?? "inventory"} extraTables={rewardOutcome.ExtraLootTables.Count} guaranteedItems={rewardOutcome.GuaranteedItems.Count}");
                return true;
            }
            finally
            {
                if (hadRarityProperty)
                    avatar.Properties[PropertyEnum.LootBonusRarityPct] = originalRarity;
                else
                    avatar.Properties.RemoveProperty(rarityPropertyId);

                if (hadSpecialProperty)
                    avatar.Properties[PropertyEnum.LootBonusSpecialPct] = originalSpecial;
                else
                    avatar.Properties.RemoveProperty(specialPropertyId);
            }
        }

        private void GrantRewardLootTable(PrototypeId lootTableProtoRef, LootInputSettings inputSettings, string delivery, ref int groundRecipientId, int itemLevel = 0)
        {
            if (lootTableProtoRef == PrototypeId.Invalid || inputSettings == null)
                return;

            int originalLevel = inputSettings.LootRollSettings?.Level ?? 0;
            int originalRequirementLevel = inputSettings.LootRollSettings?.LevelForRequirementCheck ?? 0;
            int originalForcedItemLevel = inputSettings.LootRollSettings?.ForcedItemLevel ?? 0;
            bool overrideLevel = itemLevel > 1 && inputSettings.LootRollSettings != null;
            if (overrideLevel)
            {
                inputSettings.LootRollSettings.Level = itemLevel;
                inputSettings.LootRollSettings.LevelForRequirementCheck = itemLevel;
                inputSettings.LootRollSettings.ForcedItemLevel = itemLevel;
            }

            try
            {
                if (MythicRiftRewardTuning.IsGroundDelivery(delivery))
                {
                    Game.LootManager.SpawnLootFromTable(lootTableProtoRef, inputSettings, groundRecipientId);
                    return;
                }

                Game.LootManager.GiveLootFromTable(lootTableProtoRef, inputSettings);
            }
            finally
            {
                if (overrideLevel)
                {
                    inputSettings.LootRollSettings.Level = originalLevel;
                    inputSettings.LootRollSettings.LevelForRequirementCheck = originalRequirementLevel;
                    inputSettings.LootRollSettings.ForcedItemLevel = originalForcedItemLevel;
                }
            }
        }

        private void GrantRewardItem(
            MythicRiftRewardGuaranteedItem guaranteedItem,
            Player player,
            Avatar avatar,
            WorldEntity sourceEntity = null,
            Vector3? positionOverride = null,
            string deliveryOverride = null)
        {
            if (guaranteedItem == null)
                return;

            PrototypeId itemProtoRef = guaranteedItem.ItemProtoRef;
            if (itemProtoRef == PrototypeId.Invalid || player == null)
                return;

            string delivery = deliveryOverride ?? guaranteedItem.Delivery;
            int itemLevel = guaranteedItem.ItemLevel;
            WorldEntity rewardSourceEntity = sourceEntity ?? avatar;

            if (guaranteedItem.IsAgentReward)
            {
                using LootResultSummary lootResultSummary = ObjectPoolManager.Instance.Get<LootResultSummary>();
                lootResultSummary.Add(new LootResult(new AgentSpec(itemProtoRef, Math.Max(itemLevel, 1), 0)));

                if (MythicRiftRewardTuning.IsGroundDelivery(delivery))
                {
                    using LootInputSettings inputSettings = ObjectPoolManager.Instance.Get<LootInputSettings>();
                    inputSettings.Initialize(LootContext.Drop, player, rewardSourceEntity, Math.Max(itemLevel, 1), positionOverride);
                    Game.LootManager.SpawnLootFromSummary(lootResultSummary, inputSettings);
                    return;
                }

                Game.LootManager.GiveLootFromSummary(lootResultSummary, player, PrototypeId.Invalid);
                return;
            }

            if (itemLevel > 1)
            {
                ItemSpec itemSpec = Game.LootManager.CreateItemSpec(itemProtoRef, LootContext.Drop, player, itemLevel);
                if (itemSpec == null)
                {
                    Logger.Warn($"Mythic Rift failed to create reward item {itemProtoRef.GetNameFormatted()} at level {itemLevel}.");
                    return;
                }

                using LootResultSummary lootResultSummary = ObjectPoolManager.Instance.Get<LootResultSummary>();
                lootResultSummary.Add(new LootResult(itemSpec));

                if (MythicRiftRewardTuning.IsGroundDelivery(delivery))
                {
                    using LootInputSettings inputSettings = ObjectPoolManager.Instance.Get<LootInputSettings>();
                    inputSettings.Initialize(LootContext.Drop, player, rewardSourceEntity, itemLevel, positionOverride);
                    Game.LootManager.SpawnLootFromSummary(lootResultSummary, inputSettings);
                }
                else
                {
                    Game.LootManager.GiveLootFromSummary(lootResultSummary, player, PrototypeId.Invalid);
                }

                return;
            }

            if (MythicRiftRewardTuning.IsGroundDelivery(delivery))
            {
                Game.LootManager.SpawnItem(itemProtoRef, LootContext.Drop, player, rewardSourceEntity);
                return;
            }

            Game.LootManager.GiveItem(itemProtoRef, LootContext.Drop, player);
        }

        private bool TrySpawnRewardChest(
            MythicRiftRunState runState,
            Player player,
            Avatar avatar,
            IReadOnlyList<PendingRewardDrop> rewardDrops,
            float bonusRarityPct,
            float bonusSpecialPct)
        {
            if (runState?.Config == null || player == null || avatar?.IsInWorld != true || rewardDrops == null || rewardDrops.Count == 0)
                return false;

            Region region = avatar.Region;
            if (region == null)
                return false;

            PrototypeId chestProtoRef = ResolvePrototype(RiftRewardChestPrototypeName);
            WorldEntityPrototype chestProto = chestProtoRef.As<WorldEntityPrototype>();
            if (chestProtoRef == PrototypeId.Invalid || chestProto == null)
                return Logger.WarnReturn(false, $"TrySpawnRewardChest(): failed to resolve reward chest prototype {RiftRewardChestPrototypeName}.");

            Vector3 position;
            if (chestProto.Bounds == null ||
                EntityHelper.GetSpawnPositionNearAvatar(avatar, region, chestProto.Bounds, 250f, out position) == false)
            {
                position = avatar.RegionLocation.Position + avatar.Forward * 150f;
            }

            position = RegionLocation.ProjectToFloor(region, position);

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = chestProtoRef;
            settings.Position = position;
            settings.Orientation = avatar.RegionLocation.Orientation;
            settings.RegionId = region.Id;
            settings.Lifespan = CompletedRunRetention;

            using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            settingsProperties[PropertyEnum.Interactable] = (int)TriBool.True;
            settingsProperties[PropertyEnum.InteractableUsesLeft] = 1;
            settings.Properties = settingsProperties;

            WorldEntity chest = Game.EntityManager.CreateEntity(settings) as WorldEntity;
            if (chest == null)
                return Logger.WarnReturn(false, $"TrySpawnRewardChest(): failed to create reward chest for Mythic Rift run {runState.Config.RunId}.");

            _pendingRewardChestsByEntityId[chest.Id] = new PendingRewardChest(
                runState.Config.RunId,
                region.Id,
                player.DatabaseUniqueId,
                rewardDrops.ToList(),
                bonusRarityPct,
                bonusSpecialPct);

            RegisterRewardChestRegionListener(region);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} spawned reward chest 0x{chest.Id:X} for playerDbId=0x{player.DatabaseUniqueId:X} drops={rewardDrops.Count}.");
            return true;
        }

        private void OnRewardChestInteract(in PlayerInteractGameEvent evt)
        {
            if (evt.InteractableObject == null)
                return;

            if (_pendingRewardChestsByEntityId.TryGetValue(evt.InteractableObject.Id, out PendingRewardChest pendingChest) == false)
                return;

            Player player = evt.Player;
            if (player == null)
                return;

            if (player.DatabaseUniqueId != pendingChest.PlayerDbId)
            {
                Game.ChatManager.SendChatFromCustomSystem(player, "[Mythic Rift] This reward chest belongs to another player.", showSender: false);
                return;
            }

            WorldEntity chest = evt.InteractableObject;
            Avatar avatar = player.CurrentAvatar;
            if (avatar == null)
                return;

            _pendingRewardChestsByEntityId.Remove(chest.Id);

            try
            {
                GrantPendingRewardDrops(player, avatar, chest, pendingChest.RewardDrops, pendingChest.BonusRarityPct, pendingChest.BonusSpecialPct);
                Game.ChatManager.SendChatFromCustomSystem(player, "[Mythic Rift] Reward chest opened.", showSender: false);
                Logger.Info($"Mythic Rift run {pendingChest.RunId} reward chest 0x{chest.Id:X} opened by playerDbId=0x{player.DatabaseUniqueId:X} drops={pendingChest.RewardDrops.Count}.");
            }
            finally
            {
                ulong regionId = pendingChest.RegionId;
                if (chest.IsInWorld)
                    chest.ExitWorld();

                chest.Destroy();
                CleanupRewardChestRegionListener(regionId);
            }
        }

        private void GrantPendingRewardDrops(
            Player player,
            Avatar avatar,
            WorldEntity sourceEntity,
            IReadOnlyList<PendingRewardDrop> rewardDrops,
            float bonusRarityPct,
            float bonusSpecialPct)
        {
            if (player == null || avatar == null || rewardDrops == null || rewardDrops.Count == 0)
                return;

            PropertyId rarityPropertyId = new(PropertyEnum.LootBonusRarityPct);
            PropertyId specialPropertyId = new(PropertyEnum.LootBonusSpecialPct);

            float originalRarity = avatar.Properties[PropertyEnum.LootBonusRarityPct];
            float originalSpecial = avatar.Properties[PropertyEnum.LootBonusSpecialPct];
            bool hadRarityProperty = avatar.Properties.HasProperty(PropertyEnum.LootBonusRarityPct);
            bool hadSpecialProperty = avatar.Properties.HasProperty(PropertyEnum.LootBonusSpecialPct);

            try
            {
                if (bonusRarityPct > 0f)
                    avatar.Properties.AdjustProperty(bonusRarityPct, rarityPropertyId);

                if (bonusSpecialPct > 0f)
                    avatar.Properties.AdjustProperty(bonusSpecialPct, specialPropertyId);

                int groundRecipientId = 1;
                Vector3? positionOverride = sourceEntity?.IsInWorld == true
                    ? sourceEntity.RegionLocation.Position
                    : null;

                foreach (PendingRewardDrop rewardDrop in rewardDrops)
                {
                    if (rewardDrop == null)
                        continue;

                    if (rewardDrop.LootTableProtoRef != PrototypeId.Invalid)
                    {
                        using LootInputSettings inputSettings = ObjectPoolManager.Instance.Get<LootInputSettings>();
                        inputSettings.Initialize(LootContext.Drop, player, sourceEntity ?? avatar, positionOverride);
                        GrantRewardLootTable(rewardDrop.LootTableProtoRef, inputSettings, "ground", ref groundRecipientId, rewardDrop.ItemLevel);
                        continue;
                    }

                    if (rewardDrop.GuaranteedItem != null)
                        GrantRewardItem(rewardDrop.GuaranteedItem, player, avatar, sourceEntity, positionOverride, "ground");
                }
            }
            finally
            {
                if (hadRarityProperty)
                    avatar.Properties[PropertyEnum.LootBonusRarityPct] = originalRarity;
                else
                    avatar.Properties.RemoveProperty(rarityPropertyId);

                if (hadSpecialProperty)
                    avatar.Properties[PropertyEnum.LootBonusSpecialPct] = originalSpecial;
                else
                    avatar.Properties.RemoveProperty(specialPropertyId);
            }
        }

        public int GrantRewardsToRunPlayers(ulong runId)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null || runState.Status is not (MythicRiftRunStatus.Success or MythicRiftRunStatus.Failed))
                return 0;

            HashSet<ulong> recipientDbIds = new(runState.RewardEligiblePlayerDbIds);

            int grantedCount = 0;
            foreach (ulong playerDbId in recipientDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                if (player == null)
                    continue;

                if (GrantRewardsToPlayer(runId, player))
                    grantedCount++;
            }

            return grantedCount;
        }

        public bool TryReloadRewardTuning(out string message)
        {
            string configPath = MythicRiftRewardTuning.ConfigPath;
            MythicRiftRewardTuning previousTuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();

            if (File.Exists(configPath) == false)
            {
                _rewardTuning = MythicRiftRewardTuning.CreateDefault();
                _rewardItemPoolsByDirectory.Clear();
                message = $"Reward tuning file not found at {FileHelper.GetRelativePath(configPath)}; using built-in defaults.";
                _rewardTuningLastLoadMessage = message;
                return true;
            }

            MythicRiftRewardTuning loadedTuning = FileHelper.DeserializeJson<MythicRiftRewardTuning>(configPath, MythicRiftRewardTuning.JsonOptions);
            if (loadedTuning == null)
            {
                _rewardTuning = previousTuning;
                message = $"Failed to load reward tuning from {FileHelper.GetRelativePath(configPath)}; keeping previous profile '{previousTuning.ProfileName}'.";
                _rewardTuningLastLoadMessage = message;
                return false;
            }

            loadedTuning.Normalize();
            _rewardTuning = loadedTuning.Enabled ? loadedTuning : MythicRiftRewardTuning.CreateDefault();
            _rewardItemPoolsByDirectory.Clear();
            message = loadedTuning.Enabled
                ? $"Loaded reward tuning profile '{_rewardTuning.ProfileName}' from {FileHelper.GetRelativePath(configPath)}. primaryOverrides={_rewardTuning.PrimaryLootTableOverrides.Count} extraLootTables={_rewardTuning.ExtraLootTables.Count} rewardRecipes={_rewardTuning.RewardRecipes.Count} randomItemPools={_rewardTuning.RandomItemPools.Count} guaranteedItems={_rewardTuning.GuaranteedItems.Count}"
                : $"Reward tuning file loaded but disabled; using built-in defaults. path={FileHelper.GetRelativePath(configPath)}";
            _rewardTuningLastLoadMessage = message;
            return true;
        }

        public List<string> BuildRewardTuningDiagnostics()
        {
            MythicRiftRewardTuning tuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
            List<string> lines = new()
            {
                $"rewardTuningPath={FileHelper.GetRelativePath(MythicRiftRewardTuning.ConfigPath)}",
                $"lastLoad={_rewardTuningLastLoadMessage}",
                $"profile={tuning.ProfileName} | enabled={tuning.Enabled}",
                $"bossLootOnSuccess={tuning.GrantBossLootOnSuccess} | bossLootOnFailure={tuning.GrantBossLootOnFailure} | bossLootInThirtyWaveMode={tuning.GrantBossLootInThirtyWaveMode}",
                $"suppressNativeRiftBossLoot={tuning.SuppressNativeRiftBossLoot}",
                $"defaultDelivery={tuning.DefaultDelivery} | primaryLootTableOverrides={tuning.PrimaryLootTableOverrides.Count}",
                $"timedSuccessBonusRIF={tuning.TimedSuccessBonusRarityPct:P0} | timedSuccessBonusSIF={tuning.TimedSuccessBonusSpecialPct:P0}",
                $"checkpointBonusRIF={tuning.CheckpointSuccessBonusRarityPct:P0} | checkpointBonusSIF={tuning.CheckpointSuccessBonusSpecialPct:P0}",
                $"failureBonusRIF={tuning.FailureBonusRarityPct:P0} | failureBonusSIF={tuning.FailureBonusSpecialPct:P0}",
                $"extraLootTables={tuning.ExtraLootTables.Count} | rewardRecipes={tuning.RewardRecipes.Count} | randomItemPools={tuning.RandomItemPools.Count} | guaranteedItems={tuning.GuaranteedItems.Count} | lootTableAliases={tuning.LootTableAliases.Count}"
            };

            foreach (MythicRiftPrimaryLootTableTuning entry in tuning.PrimaryLootTableOverrides.Take(20))
            {
                string maxLevelText = entry.MaxRiftLevel > 0 ? entry.MaxRiftLevel.ToString() : "none";
                lines.Add(
                    $"primaryLoot id={entry.Id} | enabled={entry.Enabled} | delivery={entry.Delivery} | min={entry.MinRiftLevel} | max={maxLevelText} | checkpointOnly={entry.CheckpointOnly} | classicOnly={entry.ClassicOnly} | lootTable={entry.LootTablePrototype}");
            }

            if (tuning.PrimaryLootTableOverrides.Count > 20)
                lines.Add($"... {tuning.PrimaryLootTableOverrides.Count - 20} more primary loot table override entries omitted.");

            foreach (MythicRiftExtraLootTableTuning entry in tuning.ExtraLootTables.Take(20))
            {
                string maxLevelText = entry.MaxRiftLevel > 0 ? entry.MaxRiftLevel.ToString() : "none";
                lines.Add(
                    $"extraLoot id={entry.Id} | enabled={entry.Enabled} | delivery={entry.Delivery} | chance={entry.ChancePercent:0.##}% | rolls={entry.Rolls} | min={entry.MinRiftLevel} | max={maxLevelText} | successOnly={entry.SuccessOnly} | checkpointOnly={entry.CheckpointOnly} | classicOnly={entry.ClassicOnly} | lootTable={entry.LootTablePrototype}");
            }

            if (tuning.ExtraLootTables.Count > 20)
                lines.Add($"... {tuning.ExtraLootTables.Count - 20} more extra loot table entries omitted.");

            foreach (MythicRiftRewardRecipeTuning recipe in tuning.RewardRecipes.Take(20))
            {
                string maxLevelText = recipe.MaxRiftLevel > 0 ? recipe.MaxRiftLevel.ToString() : "none";
                lines.Add(
                    $"rewardRecipe id={recipe.Id} | enabled={recipe.Enabled} | min={recipe.MinRiftLevel} | max={maxLevelText} | successOnly={recipe.SuccessOnly} | checkpointOnly={recipe.CheckpointOnly} | classicOnly={recipe.ClassicOnly} | tables={recipe.Tables.Count} | bosses={string.Join(",", recipe.BossSourceIds)}");
            }

            if (tuning.RewardRecipes.Count > 20)
                lines.Add($"... {tuning.RewardRecipes.Count - 20} more reward recipe entries omitted.");

            foreach (MythicRiftRandomItemPoolTuning itemPool in tuning.RandomItemPools.Take(20))
            {
                string maxLevelText = itemPool.MaxRiftLevel > 0 ? itemPool.MaxRiftLevel.ToString() : "none";
                int candidateCount = ResolveRewardItemPool(itemPool).Count;
                lines.Add(
                    $"randomItemPool id={itemPool.Id} | enabled={itemPool.Enabled} | candidates={candidateCount} | itemLevel={itemPool.ItemLevel} | delivery={itemPool.Delivery} | chance={itemPool.ChancePercent:0.##}% | rolls={itemPool.Rolls} | min={itemPool.MinRiftLevel} | max={maxLevelText} | checkpointOnly={itemPool.CheckpointOnly} | directory={itemPool.PrototypeDirectoryPrefix} | explicitItems={itemPool.ItemPrototypePaths?.Count ?? 0}");
            }

            if (tuning.RandomItemPools.Count > 20)
                lines.Add($"... {tuning.RandomItemPools.Count - 20} more random item pool entries omitted.");

            foreach (MythicRiftGuaranteedItemTuning item in tuning.GuaranteedItems.Take(20))
            {
                string maxLevelText = item.MaxRiftLevel > 0 ? item.MaxRiftLevel.ToString() : "none";
                string maxWaveText = item.MaxWave > 0 ? item.MaxWave.ToString() : "none";
                lines.Add(
                    $"guaranteedItem id={item.Id} | enabled={item.Enabled} | runtimeId={item.ItemPrototypeRuntimeId} | prototype={item.ItemPrototypeName} | quantity={item.Quantity} | quantityMatchesWave={item.QuantityMatchesRewardWave} | cumulativeWaveQuantity={item.CumulativeWaveQuantity} | quantityCap={item.QuantityCap} | delivery={item.Delivery} | minLevel={item.MinRiftLevel} | maxLevel={maxLevelText} | minWave={item.MinWave} | maxWave={maxWaveText} | checkpointOnly={item.CheckpointOnly}");
            }

            if (tuning.GuaranteedItems.Count > 20)
                lines.Add($"... {tuning.GuaranteedItems.Count - 20} more guaranteed item entries omitted.");

            return lines;
        }

        public bool EvaluateRunTimer(ulong runId, TimeSpan currentTime)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null || runState.HasExpired(currentTime) == false)
                return false;

            return CompleteRunFailure(runState, currentTime, "Time expired. No completion rewards.", returnParticipantsToHub: true);
        }

        public bool TryHandleRiftDeathRelease(Avatar avatar, DeathReleaseRequestType requestType)
        {
            if (avatar == null || requestType != DeathReleaseRequestType.Checkpoint)
                return false;

            Player player = avatar.GetOwnerOfType<Player>();
            if (player == null)
                return false;

            MythicRiftRunState runState = GetInProgressRunForPlayer(player.DatabaseUniqueId);
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                return false;

            Region region = avatar.Region;
            if (region == null || region.Id != runState.RegionId)
                return false;

            RegionConnectionTargetPrototype startTargetProto = runState.Config.StartTargetProtoRef.As<RegionConnectionTargetPrototype>();
            if (startTargetProto == null)
                return false;

            Vector3 position = Vector3.Zero;
            Orientation orientation = Orientation.Zero;
            PrototypeId cellRef = GameDatabase.GetDataRefByAsset(startTargetProto.Cell);
            if (region.FindTargetLocation(ref position, ref orientation, startTargetProto.Area, cellRef, startTargetProto.Entity) == false)
                return false;

            position = RegionLocation.ProjectToFloor(region, position);

            using Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>();
            teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Resurrect);
            teleporter.DifficultyTierRef = region.DifficultyTierRef;

            bool teleported = teleporter.TeleportToRegionLocation(region.Id, position);
            if (teleported)
                Logger.Info($"Mythic Rift run {runState.Config.RunId} handled death release inside Rift region for playerDbId=0x{player.DatabaseUniqueId:X} target={runState.Config.StartTargetProtoRef.GetNameFormatted()}.");

            return teleported;
        }

        public bool AttachRunToRegion(ulong runId, Region region)
        {
            MythicRiftRunState runState = GetRun(runId);
            if (runState == null || region == null)
                return false;

            runState.AttachRegion(region.Id);
            RegisterRegionPlayersAsParticipants(runState, region);
            ApplyRunDifficultyToRegion(runState, region);
            EnsureRegionListener(region);
            if (runState.Config.UseBossGauntletMode == false &&
                runState.Config.Content.BossOnlyCheckpointEligible == false)
            {
                EnableRiftPopulationRespawns(runState, region);
            }

            return true;
        }

        public void Update(TimeSpan currentTime)
        {
            List<ulong> runsToRemove = null;

            foreach (MythicRiftRunState runState in _activeRuns.Values)
            {
                TryProcessPendingFailedRunEvacuation(runState, currentTime);
                TryProcessPendingBossGauntletFailureRecovery(runState, currentTime);
                RegisterBoundRegionPlayersAsParticipants(runState);
                UpdateParticipantPresence(runState, currentTime);
                TryAutoBindAndStartPendingRun(runState, currentTime);
                TryApplyRunDifficultyToBoundRegion(runState);
                MaintainRiftHazards(runState, currentTime);
                MaintainCustomRiftPopulation(runState, currentTime);
                TrySpawnPendingMilestoneEncounters(runState);
                SuppressNativeCheckpointPopulation(runState);
                TryStartBossGauntletWave(runState, currentTime);
                TryStartBossOnlyCheckpoint(runState, currentTime);
                TryMaintainBossWave(runState, currentTime);
                SuppressNativeTerminalBosses(runState, currentTime);
                RefreshRiftObjectiveWidgets(runState, currentTime);

                if (runState.HasExpired(currentTime))
                {
                    CompleteRunFailure(runState, currentTime, "Time expired. No completion rewards.", returnParticipantsToHub: true);
                }
                else
                {
                    TryNotifyRunTimeWarnings(runState, currentTime);
                }

                if (TryAbortRunForDisconnectedParticipants(runState, currentTime))
                    continue;

                if (TryHandleParticipantExit(runState, currentTime))
                {
                    runsToRemove ??= new();
                    runsToRemove.Add(runState.Config.RunId);
                    continue;
                }

                if (TryAbortStalePendingRun(runState, currentTime))
                    continue;

                if (ShouldRemoveCompletedRunBecauseRegionIsEmpty(runState))
                {
                    runsToRemove ??= new();
                    runsToRemove.Add(runState.Config.RunId);
                    continue;
                }

                if (ShouldAutoRemoveRun(runState, currentTime) == false)
                    continue;

                runsToRemove ??= new();
                runsToRemove.Add(runState.Config.RunId);
            }

            if (runsToRemove == null)
                return;

            foreach (ulong runId in runsToRemove)
            {
                if (RemoveRun(runId))
                    Logger.Info($"Mythic Rift run {runId} was removed automatically after retention cleanup.");
            }
        }

        private void QueueFailedRunEvacuation(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config == null)
                return;

            _pendingFailedRunEvacuationsAt[runState.Config.RunId] = currentTime + FailedRunEvacuationDelay;
        }

        private void QueueBossGauntletFailureRecovery(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode != true)
                return;

            _pendingBossGauntletFailureRecoveriesAt[runState.Config.RunId] = currentTime + BossGauntletFailureRecoveryDelay;
        }

        private void TryProcessPendingBossGauntletFailureRecovery(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode != true ||
                runState.Status != MythicRiftRunStatus.Failed ||
                _pendingBossGauntletFailureRecoveriesAt.TryGetValue(runState.Config.RunId, out TimeSpan recoveryAt) == false ||
                currentTime < recoveryAt)
            {
                return;
            }

            _pendingBossGauntletFailureRecoveriesAt.Remove(runState.Config.RunId);
            ReviveRunParticipantsInPlace(runState);
        }

        private void TryProcessPendingFailedRunEvacuation(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config == null ||
                _pendingFailedRunEvacuationsAt.TryGetValue(runState.Config.RunId, out TimeSpan evacuationAt) == false ||
                currentTime < evacuationAt)
            {
                return;
            }

            int returnedPlayerCount = ReturnRunParticipantsToDangerRoomHub(runState, includePlayersAlreadyOutsideRunRegion: false);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} returned {returnedPlayerCount} online participant(s) to the Danger Room hub after timer failure.");

            if (HasAnyPlayerInRunRegion(runState))
            {
                _pendingFailedRunEvacuationsAt[runState.Config.RunId] = currentTime + FailedRunEvacuationRetryDelay;
                return;
            }

            _pendingFailedRunEvacuationsAt.Remove(runState.Config.RunId);
            RequestRunRegionShutdownWhenVacant(runState);
        }

        public MythicRiftContentEntry GetContent(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                return null;

            return _contentPool.FirstOrDefault(entry => entry.Id.Equals(contentId, StringComparison.OrdinalIgnoreCase));
        }

        public void RegisterContent(MythicRiftContentEntry content)
        {
            if (content == null || content.IsValid == false)
            {
                Logger.Warn("RegisterContent(): invalid mythic rift content entry");
                return;
            }

            if (GetContent(content.Id) != null)
            {
                Logger.Warn($"RegisterContent(): duplicate mythic rift content id={content.Id}");
                return;
            }

            _contentPool.Add(content);
        }

        private MythicRiftRunState RegisterRun(MythicRiftRunConfig config)
        {
            MythicRiftRunState runState = CreateRunState(config);
            if (runState == null)
                return null;

            runState.SetRegisteredAt(Game.CurrentTime);
            _activeRuns[runState.Config.RunId] = runState;
            return runState;
        }

        private MythicRiftRunState RequestRunInternal(
            Player player,
            string contentId,
            int riftLevel,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode,
            bool useRandomContent,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (player == null)
            {
                errorMessage = "Player not found.";
                return null;
            }

            if (riftLevel <= 0)
            {
                errorMessage = "Invalid Rift level.";
                return null;
            }

            Party party = player.GetParty();
            if (party != null && party.NumMembers > 1 && player.IsPartyLeader() == false)
            {
                Logger.Info($"Mythic Rift request rejected because requester is not party leader. playerDbId=0x{player.DatabaseUniqueId:X} partyId=0x{party.PartyId:X} leaderDbId=0x{party.LeaderId:X}");
                errorMessage = "Only the party leader can request a group Mythic Rift run.";
                return null;
            }

            if (CanAccessRiftLevel(player.DatabaseUniqueId, riftLevel, mode) == false)
            {
                int unlockedLevel = GetHighestUnlockedRiftLevel(player.DatabaseUniqueId, mode);
                errorMessage = $"Requested {GetModeDisplayName(mode)} level {riftLevel} is locked. Highest unlocked level: {unlockedLevel}.";
                return null;
            }

            HashSet<ulong> launchRoster = BuildEligibleLaunchRoster(player, party);
            int requestedPlayerCount = Math.Clamp(launchRoster.Count, 1, 5);
            if (TryFindInProgressRunConflict(launchRoster, out MythicRiftRunState conflictingRun))
            {
                errorMessage = $"A Mythic Rift run is already in progress for this player or party (runId={conflictingRun.Config.RunId}, status={conflictingRun.Status}).";
                return null;
            }

            HashSet<string> excludedMapContentIds = useRandomContent
                ? BuildRandomMapExclusions(player, party)
                : null;
            HashSet<string> excludedBossFamilies = BuildRandomBossFamilyExclusions(player, party);

            MythicRiftRunState runState = useRandomContent
                ? CreateRandomDebugRun(riftLevel, requestedPlayerCount, killQuota, timeLimit, excludedMapContentIds, mode, excludedBossFamilies)
                : CreateDebugRun(contentId, riftLevel, requestedPlayerCount, killQuota, timeLimit, mode, excludedBossFamilies);

            if (runState == null)
            {
                errorMessage = useRandomContent
                    ? "Failed to create Mythic Rift run."
                    : $"Failed to create Mythic Rift run for content id: {contentId}";
                return null;
            }

            RegisterInitialParticipants(runState, launchRoster);
            if (useRandomContent)
                TrackRecentlySelectedMapContent(runState);
            TrackRecentlySelectedBossFamilies(runState);

            int excludedPartyMembers = Math.Max((party?.NumMembers ?? 1) - launchRoster.Count, 0);
            if (excludedPartyMembers > 0)
            {
                Game.ChatManager.SendChatFromCustomSystem(
                    player,
                    $"[Mythic Rift] {excludedPartyMembers} party member(s) were not included because they were offline or not in your current region.",
                    showSender: false);
            }

            Logger.Info($"Mythic Rift run {runState.Config.RunId} requested by playerDbId=0x{player.DatabaseUniqueId:X} at level {riftLevel}. mode={mode} partyId=0x{party?.PartyId ?? 0UL:X} partyLeaderDbId=0x{party?.LeaderId ?? 0UL:X} partyMembers={party?.NumMembers ?? 1} launchRoster={launchRoster.Count} excludedPartyMembers={excludedPartyMembers}");
            return runState;
        }

        private MythicRiftContentEntry SelectRandomMapContent(int riftLevel, int requestedPlayerCount, IReadOnlyCollection<string> excludedContentIds = null)
        {
            bool isCheckpointLevel = IsCheckpointRiftLevel(riftLevel);
            List<MythicRiftContentEntry> eligibleContent = isCheckpointLevel
                ? _contentPool
                    .Where(entry => entry.BossOnlyCheckpointEligible &&
                                    entry.CanAppearAtRandomRiftLevel(riftLevel) &&
                                    entry.SupportsPlayerCount(requestedPlayerCount) &&
                                    RandomCheckpointContentExclusions.Contains(entry.Id) == false)
                    .ToList()
                : _contentPool
                    .Where(entry => entry.RandomMapEligible &&
                                    entry.BossOnlyCheckpointEligible == false &&
                                    entry.CanAppearAtRandomRiftLevel(riftLevel) &&
                                    entry.SupportsPlayerCount(requestedPlayerCount))
                    .ToList();

            if (eligibleContent.Count == 0)
                return null;

            if (excludedContentIds != null && excludedContentIds.Count > 0 && eligibleContent.Count > excludedContentIds.Count)
            {
                List<MythicRiftContentEntry> filteredContent = eligibleContent
                    .Where(entry => excludedContentIds.Any(excludedId => string.Equals(excludedId, entry.Id, StringComparison.OrdinalIgnoreCase)) == false)
                    .ToList();

                if (filteredContent.Count > 0)
                    eligibleContent = filteredContent;
            }

            List<MythicRiftContentEntry> specialContent = eligibleContent.Where(entry => entry.IsSpecialRandomMap).ToList();
            List<MythicRiftContentEntry> standardContent = eligibleContent.Where(entry => entry.IsSpecialRandomMap == false).ToList();
            if (specialContent.Count > 0 && standardContent.Count > 0 && Game.Random.NextFloat() < SpecialRandomMapChance)
                return PickRandomContent(specialContent);

            if (standardContent.Count > 0)
                return PickRandomContent(standardContent);

            return PickRandomContent(eligibleContent);
        }

        private MythicRiftContentEntry SelectBossContentForRandomMap(
            MythicRiftContentEntry mapContent,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            if (mapContent?.UseOwnBossSourceWhenSelected == true && mapContent.HasValidBossSource)
                return mapContent;

            return SelectRandomBossContent(mapContent, excludedBossFamilies);
        }

        private static bool IsCheckpointRiftLevel(int riftLevel)
        {
            return riftLevel > 0 && riftLevel % CheckpointRiftLevelInterval == 0;
        }

        private MythicRiftContentEntry SelectBossContentForFixedMap(
            MythicRiftContentEntry mapContent,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            if (mapContent == null)
                return null;

            if (mapContent.HasValidBossSource && (mapContent.RandomBossEligible || mapContent.UseOwnBossSourceWhenSelected))
                return mapContent;

            return SelectRandomBossContent(mapContent, excludedBossFamilies);
        }

        private MythicRiftContentEntry SelectRandomBossContent(
            MythicRiftContentEntry mapContent,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            List<MythicRiftContentEntry> eligibleContent = _contentPool.Where(entry => entry.RandomBossEligible && entry.HasValidBossSource).ToList();
            if (eligibleContent.Count == 0)
                return null;

            if (excludedBossFamilies != null && excludedBossFamilies.Count > 0 && eligibleContent.Count > excludedBossFamilies.Count)
            {
                List<MythicRiftContentEntry> filteredContent = eligibleContent
                    .Where(entry => excludedBossFamilies.Any(family => string.Equals(NormalizeBossFamily(family), NormalizeBossFamily(entry.BossFamily), StringComparison.OrdinalIgnoreCase)) == false)
                    .ToList();

                if (filteredContent.Count > 0)
                    eligibleContent = filteredContent;
            }

            if (mapContent != null && eligibleContent.Count > 1)
            {
                List<MythicRiftContentEntry> alternateBossContent = eligibleContent
                    .Where(entry => string.Equals(entry.Id, mapContent.Id, StringComparison.OrdinalIgnoreCase) == false)
                    .ToList();

                if (alternateBossContent.Count > 0)
                    eligibleContent = alternateBossContent;
            }

            return PickRandomContent(eligibleContent);
        }

        private MythicRiftContentEntry PickRandomContent(IReadOnlyList<MythicRiftContentEntry> eligibleContent)
        {
            if (eligibleContent == null || eligibleContent.Count == 0)
                return null;

            int index = Game.Random.Next(0, eligibleContent.Count);
            return eligibleContent[index];
        }

        private void EnsureRegionListener(Region region)
        {
            if (region == null || _regionEntityDeadActions.ContainsKey(region.Id))
                return;

            Event<EntityDeadGameEvent>.Action action = (in EntityDeadGameEvent evt) => OnRegionEntityDead(region.Id, evt);
            region.EntityDeadEvent.AddActionBack(action);
            _regionEntityDeadActions[region.Id] = action;
        }

        private void CleanupRegionListener(ulong regionId)
        {
            if (regionId == 0)
                return;

            bool regionStillUsed = _activeRuns.Values.Any(run => run.RegionId == regionId);
            if (regionStillUsed)
                return;

            if (_regionEntityDeadActions.TryGetValue(regionId, out Event<EntityDeadGameEvent>.Action action) == false)
                return;

            Region region = Game.RegionManager.GetRegion(regionId);
            region?.EntityDeadEvent.RemoveAction(action);
            _regionEntityDeadActions.Remove(regionId);
        }

        private void RegisterRewardChestRegionListener(Region region)
        {
            if (region == null || _regionPlayerInteractActions.ContainsKey(region.Id))
                return;

            Event<PlayerInteractGameEvent>.Action action = OnRewardChestInteract;
            region.PlayerInteractEvent.AddActionBack(action);
            _regionPlayerInteractActions[region.Id] = action;
        }

        private void CleanupRewardChestRegionListener(ulong regionId)
        {
            if (regionId == 0)
                return;

            bool regionStillHasRewardChests = _pendingRewardChestsByEntityId.Values.Any(chest => chest.RegionId == regionId);
            if (regionStillHasRewardChests)
                return;

            if (_regionPlayerInteractActions.TryGetValue(regionId, out Event<PlayerInteractGameEvent>.Action action) == false)
                return;

            Region region = Game.RegionManager.GetRegion(regionId);
            region?.PlayerInteractEvent.RemoveAction(action);
            _regionPlayerInteractActions.Remove(regionId);
        }

        private void CleanupRewardChests(ulong runId)
        {
            if (runId == 0)
                return;

            List<ulong> chestIds = _pendingRewardChestsByEntityId
                .Where(entry => entry.Value.RunId == runId)
                .Select(entry => entry.Key)
                .ToList();

            HashSet<ulong> touchedRegionIds = new();
            foreach (ulong chestId in chestIds)
            {
                if (_pendingRewardChestsByEntityId.TryGetValue(chestId, out PendingRewardChest pendingChest))
                    touchedRegionIds.Add(pendingChest.RegionId);

                _pendingRewardChestsByEntityId.Remove(chestId);

                WorldEntity chest = Game.EntityManager.GetEntity<WorldEntity>(chestId);
                if (chest == null)
                    continue;

                if (chest.IsInWorld)
                    chest.ExitWorld();

                chest.Destroy();
            }

            foreach (ulong regionId in touchedRegionIds)
                CleanupRewardChestRegionListener(regionId);
        }

        private static void EnableRiftPopulationRespawns(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return;

            int enabledCount = 0;
            foreach (Area area in region.IterateAreas())
            {
                var spawnEvent = area?.PopulationArea?.SpawnEvent;
                if (spawnEvent == null)
                    continue;

                spawnEvent.RespawnObject = true;
                spawnEvent.RespawnDelayMS = RiftPopulationRespawnDelayMS;
                enabledCount++;
            }

            if (enabledCount > 0)
                Logger.Info($"Mythic Rift run {runState.Config.RunId} enabled Rift-only population respawns for {enabledCount} area(s), delay={RiftPopulationRespawnDelayMS}ms.");
        }

        private void ApplyRunDifficultyToRegion(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null || runState.RegionDifficultyScalingApplied)
                return;

            if (runState.AdmissionTrackingEnabled && runState.AdmissionFinalized == false)
                return;

            float currentPlayerToMobDamageMultiplier = region.Properties[PropertyEnum.DamageRegionPlayerToMob];
            float currentMobToPlayerDamageMultiplier = region.Properties[PropertyEnum.DamageRegionMobToPlayer];

            runState.CaptureRegionDifficultyScaling(currentPlayerToMobDamageMultiplier, currentMobToPlayerDamageMultiplier);

            float effectiveHealthMultiplier = Math.Max(runState.Difficulty.HealthMultiplier, 0.01f);
            float effectiveDamageMultiplier = Math.Max(runState.Difficulty.DamageMultiplier, 0.01f);

            region.Properties[PropertyEnum.DamageRegionPlayerToMob] = currentPlayerToMobDamageMultiplier / effectiveHealthMultiplier;
            region.Properties[PropertyEnum.DamageRegionMobToPlayer] = currentMobToPlayerDamageMultiplier * effectiveDamageMultiplier;

            Logger.Info(
                $"Mythic Rift run {runState.Config.RunId} applied region difficulty scaling: " +
                $"playerToMob {currentPlayerToMobDamageMultiplier:F4}->{region.Properties[PropertyEnum.DamageRegionPlayerToMob]:F4}, " +
                $"mobToPlayer {currentMobToPlayerDamageMultiplier:F4}->{region.Properties[PropertyEnum.DamageRegionMobToPlayer]:F4}, " +
                $"hpMultiplier={runState.Difficulty.HealthMultiplier:F4}, damageMultiplier={runState.Difficulty.DamageMultiplier:F4}, admittedPlayers={runState.AdmittedPlayerCount}.");
        }

        private void TryApplyRunDifficultyToBoundRegion(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0 || runState.RegionDifficultyScalingApplied)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region != null)
                ApplyRunDifficultyToRegion(runState, region);
        }

        private void TryRestoreRegionDifficultyScaling(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionDifficultyScalingApplied == false || runState.RegionId == 0)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region != null)
            {
                region.Properties[PropertyEnum.DamageRegionPlayerToMob] = runState.RegionPlayerToMobDamageMultiplierBeforeScaling;
                region.Properties[PropertyEnum.DamageRegionMobToPlayer] = runState.RegionMobToPlayerDamageMultiplierBeforeScaling;

                Logger.Info(
                    $"Mythic Rift run {runState.Config.RunId} restored region difficulty scaling: " +
                    $"playerToMob={runState.RegionPlayerToMobDamageMultiplierBeforeScaling:F4}, " +
                    $"mobToPlayer={runState.RegionMobToPlayerDamageMultiplierBeforeScaling:F4}.");
            }

            runState.ClearRegionDifficultyScaling();
        }

        private int SuppressNativeTerminalBosses(MythicRiftRunState runState, TimeSpan currentTime, bool force = false)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                return 0;

            if (force == false &&
                _nextNativeBossSuppressionScanAt.TryGetValue(runState.Config.RunId, out TimeSpan nextScanAt) &&
                currentTime < nextScanAt)
            {
                return 0;
            }

            _nextNativeBossSuppressionScanAt[runState.Config.RunId] = currentTime + NativeBossSuppressionScanInterval;

            PrototypeId nativeBossProtoRef = runState.Config.Content?.BossProtoRef ?? PrototypeId.Invalid;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return 0;

            List<Agent> nativeBossesToDestroy = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not Agent agent)
                    continue;

                if (agent.IsDestroyed || agent.IsDead || agent.IsInWorld == false)
                    continue;

                if (runState.IsTrackedBoss(agent.Id))
                    continue;

                if (runState.CustomPopulationEntityIds.Contains(agent.Id))
                    continue;

                RankPrototype rankProto = agent.GetRankPrototype();
                bool isNativeBossPrototype = nativeBossProtoRef != PrototypeId.Invalid && agent.IsAPrototype(nativeBossProtoRef);
                bool isUntrackedBossRank = rankProto?.IsRankBoss == true;
                if (isNativeBossPrototype == false && isUntrackedBossRank == false)
                    continue;

                nativeBossesToDestroy ??= new();
                nativeBossesToDestroy.Add(agent);
            }

            if (nativeBossesToDestroy == null)
                return 0;

            int destroyedCount = 0;
            foreach (Agent nativeBoss in nativeBossesToDestroy)
            {
                Logger.Info(
                    $"Mythic Rift run {runState.Config.RunId} suppressed native boss-rank entity {nativeBoss.PrototypeName} " +
                    $"so Rift boss {runState.Config.BossProtoRef.GetNameFormatted() ?? "unknown"} remains quota-gated.");
                nativeBoss.Destroy();
                destroyedCount++;
            }

            return destroyedCount;
        }

        private int SuppressNativeCheckpointPopulation(MythicRiftRunState runState)
        {
            if (runState?.Config?.Content?.BossOnlyCheckpointEligible != true)
                return 0;

            if (runState.Status != MythicRiftRunStatus.Active ||
                runState.RegionId == 0 ||
                runState.BossSpawnCount >= runState.Config.RequiredBossKillCount)
                return 0;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return 0;

            List<Agent> nativeAgentsToDestroy = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not Agent agent)
                    continue;

                if (agent is Avatar || agent.IsTeamUpAgent)
                    continue;

                if (agent.IsDestroyed || agent.IsDead || agent.IsInWorld == false)
                    continue;

                if (runState.IsTrackedBoss(agent.Id))
                    continue;

                if (agent.IsHostileToPlayers() == false)
                    continue;

                nativeAgentsToDestroy ??= new();
                nativeAgentsToDestroy.Add(agent);
            }

            if (nativeAgentsToDestroy == null)
                return 0;

            int destroyedCount = 0;
            foreach (Agent nativeAgent in nativeAgentsToDestroy)
            {
                Logger.Info($"Mythic Rift checkpoint run {runState.Config.RunId} suppressed native checkpoint entity {nativeAgent.PrototypeName} so the room behaves as a clean Rift boss arena.");
                nativeAgent.Destroy();
                destroyedCount++;
            }

            return destroyedCount;
        }

        public bool TryRefreshNativeObjectiveWidgetOverride(MissionObjective objective)
        {
            Mission mission = objective?.Mission;
            MythicRiftRunState runState = FindActiveRunForNativeObjectiveMission(mission, null);
            if (runState == null)
                return false;

            Region region = mission.Region;
            if (region == null)
                return false;

            RefreshRiftObjectiveWidgetsForMission(region, mission, runState, Game.CurrentTime);
            return true;
        }

        public bool TrySendNativeMissionUpdateOverride(Mission mission, Player player, MissionUpdateFlags missionFlags, MissionObjectiveUpdateFlags objectiveFlags)
        {
            if (missionFlags == MissionUpdateFlags.None && objectiveFlags == MissionObjectiveUpdateFlags.None)
                return false;

            MythicRiftRunState runState = FindActiveRunForNativeObjectiveMission(mission, player);
            if (runState == null)
                return false;

            SendNativeMissionTrackerSuppression(player, mission, runState);
            return true;
        }

        public bool TrySendNativeObjectiveUpdateOverride(MissionObjective objective, Player player, MissionObjectiveUpdateFlags objectiveFlags)
        {
            if (objectiveFlags == MissionObjectiveUpdateFlags.None)
                return false;

            Mission mission = objective?.Mission;
            MythicRiftRunState runState = FindActiveRunForNativeObjectiveMission(mission, player);
            if (runState == null)
                return false;

            SendNativeObjectiveTrackerSuppression(player, mission, objective, runState);
            return true;
        }

        private MythicRiftRunState FindActiveRunForNativeObjectiveMission(Mission mission, Player player)
        {
            if (mission == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                return null;

            Region region = mission.Region ?? player?.GetRegion();
            if (region == null)
                return null;

            foreach (MythicRiftRunState runState in _activeRuns.Values)
            {
                if (runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                    continue;

                if (runState.RegionId != region.Id && IsMatchingRunRegion(region, runState) == false)
                    continue;

                if (player != null && IsPlayerInRunRegion(player, runState) == false)
                    continue;

                if (IsNativeObjectiveMissionForRun(mission, runState))
                    return runState;
            }

            return null;
        }

        private static bool IsNativeObjectiveMissionForRun(Mission mission, MythicRiftRunState runState)
        {
            if (mission == null || runState?.Config == null)
                return false;

            bool isNativeTerminalMission = mission.PrototypeDataRef == runState.Config.MissionProtoRef;
            bool shouldControlTerminalMission = isNativeTerminalMission && SuspendNativeTerminalMissionsDuringRifts;
            bool shouldControlRegionEventMission = mission.IsRegionEventMission && SuspendNativeRegionEventMissionsDuringRifts;
            bool shouldControlRegionMission = mission.MissionManager?.IsRegionMissionManager() == true;

            return shouldControlTerminalMission || shouldControlRegionEventMission || shouldControlRegionMission;
        }

        private void RefreshRiftObjectiveWidgets(MythicRiftRunState runState, TimeSpan currentTime, bool force = false)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                return;

            if (force == false &&
                _nextRiftObjectiveWidgetRefreshAt.TryGetValue(runState.Config.RunId, out TimeSpan nextRefreshAt) &&
                currentTime < nextRefreshAt)
            {
                return;
            }

            _nextRiftObjectiveWidgetRefreshAt[runState.Config.RunId] = currentTime + RiftObjectiveWidgetRefreshInterval;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            RemoveNativeRegionWidgets(region.UIDataProvider, runState);
            RefreshDangerRoomRiftWidgets(region.UIDataProvider, runState, currentTime);

            using var missionHandle = HashSetPool<Mission>.Instance.Get(out HashSet<Mission> missions);
            AddNativeTerminalMission(missions, region.MissionManager, runState.Config.MissionProtoRef);
            AddActiveMissions(missions, region.MissionManager);

            foreach (Player player in new PlayerIterator(region))
            {
                AddNativeTerminalMission(missions, player?.MissionManager, runState.Config.MissionProtoRef);
                AddActiveMissions(missions, player?.MissionManager);
            }

            foreach (Mission mission in missions)
            {
                TrySuspendNativeObjectiveMissionForRun(runState, mission);
                SuppressNativeMissionTrackerForRunPlayers(mission, runState);
                RefreshRiftObjectiveWidgetsForMission(region, mission, runState, currentTime);
            }

            RemoveNativeRegionWidgets(region.UIDataProvider, runState);
        }

        private void RefreshRiftHudWidgets(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            RefreshDangerRoomRiftWidgets(region.UIDataProvider, runState, currentTime);
        }

        private void ClearRiftObjectiveWidgets(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            ClearDangerRoomRiftWidgets(region.UIDataProvider, runState);

            using var missionHandle = HashSetPool<Mission>.Instance.Get(out HashSet<Mission> missions);
            AddNativeTerminalMission(missions, region.MissionManager, runState.Config.MissionProtoRef);
            AddActiveMissions(missions, region.MissionManager);

            foreach (Player player in new PlayerIterator(region))
                AddNativeTerminalMission(missions, player?.MissionManager, runState.Config.MissionProtoRef);

            foreach (Mission mission in missions)
                ClearRiftObjectiveWidgetsForMission(region, mission);
        }

        public void ForceRefreshRiftUiForDiagnostics(MythicRiftRunState runState, Region currentRegion, List<string> lines)
        {
            if (lines == null)
                return;

            if (runState == null)
            {
                lines.Add("riftUi=skipped | reason=no active Rift run for player");
                return;
            }

            Region boundRegion = runState.RegionId != 0
                ? Game.RegionManager.GetRegion(runState.RegionId)
                : null;

            lines.Add(
                $"riftUi.runId={runState.Config.RunId} | status={runState.Status} | boundRegion={(boundRegion?.PrototypeDataRef.GetNameFormatted() ?? "none")} | boundRegionId=0x{runState.RegionId:X} | currentMatchesBound={boundRegion != null && currentRegion?.Id == boundRegion.Id}");

            if (runState.Status != MythicRiftRunStatus.Active)
            {
                lines.Add("riftUi=skipped | reason=Rift is not active");
                return;
            }

            Region refreshRegion = boundRegion ?? currentRegion;
            if (refreshRegion?.UIDataProvider == null)
            {
                lines.Add("riftUi=skipped | reason=Rift region or UIDataProvider not found");
                return;
            }

            RefreshDangerRoomRiftWidgets(refreshRegion.UIDataProvider, runState, Game.CurrentTime);
            AppendDangerRoomRiftWidgetDiagnostics(lines, refreshRegion.UIDataProvider, runState);
        }

        private void RefreshDangerRoomRiftWidgets(UIDataProvider uiDataProvider, MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (uiDataProvider == null || runState?.Config == null)
                return;

            PrototypeId contextRef = GetRiftWidgetContextRef(runState);
            if (contextRef == PrototypeId.Invalid)
                return;

            RefreshDangerRoomRiftLevelWidget(uiDataProvider, runState, contextRef, currentTime);
            RefreshRiftReadyCheckWidget(uiDataProvider, runState, contextRef, currentTime);
            RefreshRiftBossIconWidget(uiDataProvider, runState, contextRef);

            if (runState.Config.Content.BossOnlyCheckpointEligible)
            {
                uiDataProvider.DeleteWidget(GetRiftDangerRoomQuotaWidgetPrototypeRef(), contextRef);
            }
            else
            {
                int requiredCount = Math.Max(runState.Config.KillQuota, 1);
                int currentCount = Math.Clamp(runState.CurrentKillCount, 0, requiredCount);

                UIWidgetGenericFraction quotaWidget = GetRiftGenericFractionWidget(
                    uiDataProvider,
                    GetRiftDangerRoomQuotaWidgetPrototypeRef(),
                    contextRef);

                if (quotaWidget != null)
                {
                    quotaWidget.SetAreaContext(contextRef);
                    quotaWidget.SetCount(currentCount, requiredCount);
                }
            }

            TimeSpan remaining = runState.GetTimeRemaining(currentTime);
            if (remaining <= TimeSpan.Zero)
                return;

            UIWidgetGenericFraction timerWidget = GetRiftGenericFractionWidget(
                uiDataProvider,
                GetRiftDangerRoomTimerWidgetPrototypeRef(),
                contextRef);

            if (timerWidget != null)
            {
                timerWidget.SetAreaContext(contextRef);
                timerWidget.SetTimeRemaining((long)remaining.TotalMilliseconds);
            }
        }

        private static void RefreshDangerRoomRiftLevelWidget(UIDataProvider uiDataProvider, MythicRiftRunState runState, PrototypeId contextRef, TimeSpan currentTime)
        {
            LocaleStringId levelText = GetDangerRoomRiftLevelLocaleStringId(runState.Config.RiftLevel);
            if (levelText == LocaleStringId.Invalid)
                return;

            UIWidgetMissionText levelWidget = GetRiftMissionTextWidget(
                uiDataProvider,
                GetRiftDangerRoomLevelWidgetPrototypeRef(),
                contextRef);

            if (levelWidget == null)
                return;

            levelWidget.SetAreaContext(contextRef);
            levelWidget.SetText(levelText, GetRiftStatusLocaleStringId(runState, currentTime));
        }

        private void RefreshRiftReadyCheckWidget(UIDataProvider uiDataProvider, MythicRiftRunState runState, PrototypeId contextRef, TimeSpan currentTime)
        {
            PrototypeId readyCheckWidgetRef = GetRiftReadyCheckWidgetPrototypeRef();
            if (uiDataProvider == null || readyCheckWidgetRef == PrototypeId.Invalid)
                return;

            if (runState.IsReadyCheckActive(currentTime) == false)
            {
                uiDataProvider.DeleteWidget(readyCheckWidgetRef, contextRef);
                return;
            }

            UIWidgetReadyCheck readyCheckWidget = uiDataProvider.GetWidget<UIWidgetReadyCheck>(readyCheckWidgetRef, contextRef);
            if (readyCheckWidget == null)
                return;

            foreach (Player player in GetRunPlayers(runState))
            {
                string playerName = player.GetName();
                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = $"Player {player.DatabaseUniqueId:X}";

                readyCheckWidget.SetPlayerState(player.DatabaseUniqueId, playerName, PlayerState.Ready);
            }
        }

        private static void RefreshRiftBossIconWidget(UIDataProvider uiDataProvider, MythicRiftRunState runState, PrototypeId contextRef)
        {
            PrototypeId bossIconsWidgetRef = GetRiftBossIconsWidgetPrototypeRef();
            if (uiDataProvider == null || bossIconsWidgetRef == PrototypeId.Invalid)
                return;

            if (runState.BossSpawnCount <= 0 || runState.ActiveBossEntityIds.Count == 0)
            {
                uiDataProvider.DeleteWidget(bossIconsWidgetRef, contextRef);
                return;
            }

            UIWidgetEntityIconsSyncData bossIconsWidget = uiDataProvider.GetWidget<UIWidgetEntityIconsSyncData>(bossIconsWidgetRef, contextRef);
            if (bossIconsWidget == null)
                return;

            foreach (ulong bossEntityId in runState.ActiveBossEntityIds)
            {
                WorldEntity boss = uiDataProvider.Game.EntityManager.GetEntity<WorldEntity>(bossEntityId);
                boss?.ModifyTrackingContext(bossIconsWidgetRef, EntityTrackingFlag.HUD);
            }
        }

        private static void ClearDangerRoomRiftWidgets(UIDataProvider uiDataProvider, MythicRiftRunState runState)
        {
            if (uiDataProvider == null || runState?.Config == null)
                return;

            PrototypeId contextRef = GetRiftWidgetContextRef(runState);
            if (contextRef == PrototypeId.Invalid)
                return;

            uiDataProvider.DeleteWidget(GetRiftDangerRoomLevelWidgetPrototypeRef(), contextRef);
            uiDataProvider.DeleteWidget(GetRiftDangerRoomQuotaWidgetPrototypeRef(), contextRef);
            uiDataProvider.DeleteWidget(GetRiftDangerRoomTimerWidgetPrototypeRef(), contextRef);
            PrototypeId readyCheckWidgetRef = GetRiftReadyCheckWidgetPrototypeRef();
            if (readyCheckWidgetRef != PrototypeId.Invalid)
                uiDataProvider.DeleteWidget(readyCheckWidgetRef, contextRef);
            PrototypeId bossIconsWidgetRef = GetRiftBossIconsWidgetPrototypeRef();
            if (bossIconsWidgetRef != PrototypeId.Invalid)
                uiDataProvider.DeleteWidget(bossIconsWidgetRef, contextRef);
        }

        private static PrototypeId GetRiftWidgetContextRef(MythicRiftRunState runState)
        {
            if (runState?.Config == null)
                return PrototypeId.Invalid;

            return runState.Config.RegionProtoRef;
        }

        private static PrototypeId GetRiftDangerRoomLevelWidgetPrototypeRef()
        {
            if (_cachedRiftDangerRoomLevelWidgetPrototypeRef == PrototypeId.Invalid)
                _cachedRiftDangerRoomLevelWidgetPrototypeRef = ResolveRiftWidgetPrototypeRef(RiftDangerRoomLevelWidgetPrototypeName, RiftDangerRoomLevelWidgetPrototypeRef);

            return _cachedRiftDangerRoomLevelWidgetPrototypeRef;
        }

        private static PrototypeId GetRiftDangerRoomQuotaWidgetPrototypeRef()
        {
            if (_cachedRiftDangerRoomQuotaWidgetPrototypeRef == PrototypeId.Invalid)
                _cachedRiftDangerRoomQuotaWidgetPrototypeRef = ResolveRiftWidgetPrototypeRef(RiftDangerRoomQuotaWidgetPrototypeName, RiftDangerRoomQuotaWidgetPrototypeRef);

            return _cachedRiftDangerRoomQuotaWidgetPrototypeRef;
        }

        private static PrototypeId GetRiftDangerRoomTimerWidgetPrototypeRef()
        {
            if (_cachedRiftDangerRoomTimerWidgetPrototypeRef == PrototypeId.Invalid)
                _cachedRiftDangerRoomTimerWidgetPrototypeRef = ResolveRiftWidgetPrototypeRef(RiftDangerRoomTimerWidgetPrototypeName, RiftDangerRoomTimerWidgetPrototypeRef);

            return _cachedRiftDangerRoomTimerWidgetPrototypeRef;
        }

        private static PrototypeId GetRiftReadyCheckWidgetPrototypeRef()
        {
            if (_cachedRiftReadyCheckWidgetPrototypeRef == PrototypeId.Invalid)
                _cachedRiftReadyCheckWidgetPrototypeRef = FindFirstWidgetPrototypeRef<UIWidgetReadyCheckPrototype>(Array.Empty<string>());

            return _cachedRiftReadyCheckWidgetPrototypeRef;
        }

        private static PrototypeId GetRiftBossIconsWidgetPrototypeRef()
        {
            if (_cachedRiftBossIconsWidgetPrototypeRef == PrototypeId.Invalid)
                _cachedRiftBossIconsWidgetPrototypeRef = FindFirstWidgetPrototypeRef<UIWidgetEntityIconsPrototype>(RiftBossIconWidgetNameKeywords);

            return _cachedRiftBossIconsWidgetPrototypeRef;
        }

        private static PrototypeId FindFirstWidgetPrototypeRef<TPrototype>(IReadOnlyList<string> preferredNameKeywords)
            where TPrototype : MetaGameDataPrototype
        {
            List<PrototypeId> candidates = new();
            foreach (PrototypeId widgetRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<MetaGameDataPrototype>(PrototypeIterateFlags.NoAbstractApprovedOnly))
            {
                if (GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef) is TPrototype)
                    candidates.Add(widgetRef);
            }

            if (candidates.Count == 0)
                return PrototypeId.Invalid;

            if (preferredNameKeywords != null && preferredNameKeywords.Count > 0)
            {
                PrototypeId preferred = candidates.FirstOrDefault(candidate =>
                {
                    string prototypeName = GameDatabase.GetPrototypeName(candidate) ?? string.Empty;
                    return preferredNameKeywords.All(keyword => prototypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                });

                if (preferred != PrototypeId.Invalid)
                    return preferred;
            }

            return candidates[0];
        }

        private static PrototypeId ResolveRiftWidgetPrototypeRef(string prototypeName, PrototypeId fallbackRef)
        {
            PrototypeId resolvedRef = string.IsNullOrWhiteSpace(prototypeName)
                ? PrototypeId.Invalid
                : GameDatabase.GetPrototypeRefByName(prototypeName);

            if (resolvedRef != PrototypeId.Invalid && GameDatabase.GetPrototype<MetaGameDataPrototype>(resolvedRef) != null)
                return resolvedRef;

            return fallbackRef;
        }

        private static LocaleStringId GetRiftStatusLocaleStringId(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null)
                return LocaleStringId.Blank;

            if (runState.IsReadyCheckActive(currentTime))
                return (LocaleStringId)RiftStatusLocaleReadyCheck;

            if (runState.HazardEntityIds.Count > 0)
                return (LocaleStringId)RiftStatusLocaleHazards;

            if (runState.BossUnlocked || runState.ActiveBossEntityIds.Count > 0)
                return (LocaleStringId)RiftStatusLocaleBossWave;

            if (runState.Config.BossAffixes != null && runState.Config.BossAffixes.Count > 0)
                return (LocaleStringId)RiftStatusLocaleBossModifiers;

            if (runState.Config.RegionAffixes != null && runState.Config.RegionAffixes.Count > 0)
                return (LocaleStringId)RiftStatusLocaleRegionModifiers;

            return LocaleStringId.Blank;
        }

        private static void AppendDangerRoomRiftWidgetDiagnostics(List<string> lines, UIDataProvider uiDataProvider, MythicRiftRunState runState)
        {
            if (lines == null)
                return;

            PrototypeId contextRef = GetRiftWidgetContextRef(runState);
            lines.Add($"riftUi.context={contextRef.GetNameFormatted()}");
            AppendRiftWidgetPrototypeDiagnostic(lines, "level", RiftDangerRoomLevelWidgetPrototypeName, GetRiftDangerRoomLevelWidgetPrototypeRef(), typeof(UIWidgetMissionTextPrototype));
            AppendRiftWidgetPrototypeDiagnostic(lines, "quota", RiftDangerRoomQuotaWidgetPrototypeName, GetRiftDangerRoomQuotaWidgetPrototypeRef(), typeof(UIWidgetGenericFractionPrototype));
            AppendRiftWidgetPrototypeDiagnostic(lines, "timer", RiftDangerRoomTimerWidgetPrototypeName, GetRiftDangerRoomTimerWidgetPrototypeRef(), typeof(UIWidgetGenericFractionPrototype));

            string uiDump = uiDataProvider?.ToString() ?? string.Empty;
            lines.Add(string.IsNullOrWhiteSpace(uiDump)
                ? "riftUi.widgetsAfterRefresh=empty"
                : "riftUi.widgetsAfterRefresh=present");
        }

        private static void AppendRiftWidgetPrototypeDiagnostic(List<string> lines, string label, string prototypeName, PrototypeId widgetRef, Type expectedPrototypeType)
        {
            MetaGameDataPrototype widgetProto = widgetRef != PrototypeId.Invalid
                ? GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef)
                : null;

            lines.Add(
                $"riftUi.widget.{label}=name:{prototypeName} | ref:{widgetRef.GetNameFormatted()} | type:{widgetProto?.GetType().Name ?? "missing"} | expected:{expectedPrototypeType.Name} | valid:{widgetProto != null && expectedPrototypeType.IsInstanceOfType(widgetProto)}");
        }

        private static UIWidgetGenericFraction GetRiftGenericFractionWidget(UIDataProvider uiDataProvider, PrototypeId widgetRef, PrototypeId contextRef)
        {
            if (uiDataProvider == null || widgetRef == PrototypeId.Invalid)
                return null;

            if (GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef) is not UIWidgetGenericFractionPrototype)
                return null;

            return uiDataProvider.GetWidget<UIWidgetGenericFraction>(widgetRef, contextRef);
        }

        private static UIWidgetMissionText GetRiftMissionTextWidget(UIDataProvider uiDataProvider, PrototypeId widgetRef, PrototypeId contextRef)
        {
            if (uiDataProvider == null || widgetRef == PrototypeId.Invalid)
                return null;

            if (GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef) is not UIWidgetMissionTextPrototype)
                return null;

            return uiDataProvider.GetWidget<UIWidgetMissionText>(widgetRef, contextRef);
        }

        private static LocaleStringId GetRiftEntryBannerLocaleStringId(int riftLevel)
        {
            if (riftLevel <= 0 || riftLevel > RiftEntryBannerLocalizedLevelLimit)
                return LocaleStringId.Invalid;

            return (LocaleStringId)(RiftEntryBannerLocaleStringBase + (ulong)riftLevel);
        }

        private static LocaleStringId GetDangerRoomRiftLevelLocaleStringId(int riftLevel)
        {
            if (riftLevel <= 0 || riftLevel > RiftDangerRoomLevelLocalizedLevelLimit)
                return LocaleStringId.Invalid;

            return (LocaleStringId)(RiftDangerRoomLevelLocaleStringBase + (ulong)riftLevel);
        }

        private static void AddNativeTerminalMission(HashSet<Mission> missions, MissionManager missionManager, PrototypeId missionRef)
        {
            if (missions == null || missionManager == null || missionRef == PrototypeId.Invalid)
                return;

            Mission mission = missionManager.FindMissionByDataRef(missionRef);
            if (mission != null)
                missions.Add(mission);
        }

        private static void AddActiveMissions(HashSet<Mission> missions, MissionManager missionManager)
        {
            if (missions == null || missionManager == null)
                return;

            foreach (PrototypeId missionRef in missionManager.ActiveMissions)
            {
                Mission mission = missionManager.FindMissionByDataRef(missionRef);
                if (mission != null)
                    missions.Add(mission);
            }
        }

        private static int RemoveNativeRegionWidgets(UIDataProvider uiDataProvider, MythicRiftRunState runState)
        {
            if (uiDataProvider == null || runState?.Config == null)
                return 0;

            return MythicRiftUiController.RemoveNativeWidgets(
                uiDataProvider,
                GetRiftWidgetContextRef(runState),
                GetRiftDangerRoomLevelWidgetPrototypeRef(),
                GetRiftDangerRoomQuotaWidgetPrototypeRef(),
                GetRiftDangerRoomTimerWidgetPrototypeRef());
        }

        private void TrySuspendNativeObjectiveMissionForRun(MythicRiftRunState runState, Mission mission)
        {
            if (runState?.Config == null || mission == null)
                return;

            bool isNativeTerminalMission = mission.PrototypeDataRef == runState.Config.MissionProtoRef;
            bool shouldSuspendTerminalMission = isNativeTerminalMission && SuspendNativeTerminalMissionsDuringRifts;
            bool shouldSuspendRegionEventMission = mission.IsRegionEventMission && SuspendNativeRegionEventMissionsDuringRifts;
            bool shouldSuspendRegionMission = mission.MissionManager?.IsRegionMissionManager() == true;

            if (shouldSuspendTerminalMission == false && shouldSuspendRegionEventMission == false && shouldSuspendRegionMission == false)
                return;

            if (mission.PrototypeDataRef == PrototypeId.Invalid || mission.IsSuspended)
                return;

            if (mission.SetSuspendedState(true) == false)
                return;

            if (_serverSuspendedNativeObjectiveMissionsByRun.TryGetValue(runState.Config.RunId, out HashSet<Mission> suspendedMissions) == false)
            {
                suspendedMissions = new();
                _serverSuspendedNativeObjectiveMissionsByRun[runState.Config.RunId] = suspendedMissions;
            }

            suspendedMissions.Add(mission);
            string missionKind = isNativeTerminalMission ? "native terminal" : shouldSuspendRegionEventMission ? "region event" : "region";
            Logger.Info($"Mythic Rift run {runState.Config.RunId} suspended {missionKind} mission {mission.PrototypeName} while the Rift is active.");
        }

        private void RestoreSuspendedNativeObjectiveMissions(MythicRiftRunState runState)
        {
            if (runState?.Config == null)
                return;

            if (_serverSuspendedNativeObjectiveMissionsByRun.TryGetValue(runState.Config.RunId, out HashSet<Mission> suspendedMissions) == false)
                return;

            int restoredCount = 0;
            foreach (Mission mission in suspendedMissions)
            {
                if (mission?.IsSuspended != true)
                    continue;

                if (mission.SetSuspendedState(false))
                    restoredCount++;
            }

            if (restoredCount > 0)
                Logger.Info($"Mythic Rift run {runState.Config.RunId} restored {restoredCount} suspended native objective mission(s).");
        }

        private void RefreshRiftObjectiveWidgetsForMission(Region region, Mission mission, MythicRiftRunState runState, TimeSpan currentTime)
        {
            UIDataProvider uiDataProvider = region?.UIDataProvider;
            if (uiDataProvider == null || mission == null || runState?.Config == null)
                return;

            PrototypeId missionRef = mission.PrototypeDataRef;
            if (missionRef == PrototypeId.Invalid)
                return;

            bool removeMissionNameWidget = false;
            foreach (MissionObjective objective in mission.Objectives)
            {
                MissionObjectivePrototype objectiveProto = objective?.Prototype;
                if (objectiveProto == null)
                    continue;

                RefreshRiftObjectiveWidget(uiDataProvider, missionRef, objectiveProto.MetaGameWidget, runState, currentTime, ref removeMissionNameWidget);
                SuppressNativeObjectiveWidget(uiDataProvider, missionRef, objectiveProto.MetaGameWidgetFail, ref removeMissionNameWidget);
            }

            if (removeMissionNameWidget)
                SuppressMissionNameWidget(uiDataProvider, missionRef);
        }

        private void SuppressNativeMissionTrackerForRunPlayers(Mission mission, MythicRiftRunState runState)
        {
            if (mission == null || runState == null)
                return;

            foreach (Player player in GetRunPlayers(runState))
            {
                if (IsPlayerInRunRegion(player, runState) == false)
                    continue;

                SendNativeMissionTrackerSuppression(player, mission, runState);
            }
        }

        private static void SendNativeMissionTrackerSuppression(Player player, Mission mission, MythicRiftRunState runState)
        {
            if (player == null || mission == null || mission.PrototypeDataRef == PrototypeId.Invalid)
                return;

            try
            {
                NetMessageMissionUpdate missionMessage = NetMessageMissionUpdate.CreateBuilder()
                    .SetMissionPrototypeId((ulong)mission.PrototypeDataRef)
                    .SetMissionState((uint)MissionState.Inactive)
                    .SetSuppressNotification(true)
                    .SetSuspendedState(true)
                    .Build();

                player.SendMessage(missionMessage);

                foreach (MissionObjective objective in mission.Objectives)
                    SendNativeObjectiveTrackerSuppression(player, mission, objective, runState);
            }
            catch (Exception e)
            {
                Logger.Warn($"SendNativeMissionTrackerSuppression(): failed for mission {mission.PrototypeName}: {e.Message}");
            }
        }

        private static void SendNativeObjectiveTrackerSuppression(Player player, Mission mission, MissionObjective objective, MythicRiftRunState runState)
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

        private static void ClearRiftObjectiveWidgetsForMission(Region region, Mission mission)
        {
            UIDataProvider uiDataProvider = region?.UIDataProvider;
            if (uiDataProvider == null || mission == null)
                return;

            PrototypeId missionRef = mission.PrototypeDataRef;
            if (missionRef == PrototypeId.Invalid)
                return;

            foreach (MissionObjective objective in mission.Objectives)
            {
                MissionObjectivePrototype objectiveProto = objective?.Prototype;
                if (objectiveProto == null)
                    continue;

                if (objectiveProto.MetaGameWidget != PrototypeId.Invalid)
                    uiDataProvider.DeleteWidget(objectiveProto.MetaGameWidget, missionRef);

                if (objectiveProto.MetaGameWidgetFail != PrototypeId.Invalid)
                    uiDataProvider.DeleteWidget(objectiveProto.MetaGameWidgetFail, missionRef);
            }

            SuppressMissionNameWidget(uiDataProvider, missionRef);
        }

        private void RefreshRiftObjectiveWidget(UIDataProvider uiDataProvider, PrototypeId missionRef, PrototypeId widgetRef, MythicRiftRunState runState, TimeSpan currentTime, ref bool removeMissionNameWidget)
        {
            if (widgetRef == PrototypeId.Invalid)
                return;

            MetaGameDataPrototype metaDataProto = GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef);
            if (metaDataProto == null)
                return;

            removeMissionNameWidget |= metaDataProto.DisplayMissionName;

            // Keep native mission logic alive, but hide mission/patrol HUD. The dedicated Rift
            // widgets carry quota, timer, and level for every Rift map.
            uiDataProvider.DeleteWidget(widgetRef, missionRef);
        }

        private static void SuppressNativeObjectiveWidget(UIDataProvider uiDataProvider, PrototypeId missionRef, PrototypeId widgetRef, ref bool removeMissionNameWidget)
        {
            if (uiDataProvider == null || widgetRef == PrototypeId.Invalid)
                return;

            MetaGameDataPrototype metaDataProto = GameDatabase.GetPrototype<MetaGameDataPrototype>(widgetRef);
            if (metaDataProto != null)
                removeMissionNameWidget |= metaDataProto.DisplayMissionName;

            uiDataProvider.DeleteWidget(widgetRef, missionRef);
        }

        private static void SuppressMissionNameWidget(UIDataProvider uiDataProvider, PrototypeId missionRef)
        {
            PrototypeId missionNameWidgetRef = GameDatabase.UIGlobalsPrototype?.MetaGameWidgetMissionName ?? PrototypeId.Invalid;
            if (uiDataProvider == null || missionNameWidgetRef == PrototypeId.Invalid)
                return;

            uiDataProvider.DeleteWidget(missionNameWidgetRef, missionRef);
        }

        private void OnRegionEntityDead(ulong regionId, in EntityDeadGameEvent evt)
        {
            foreach (MythicRiftRunState runState in _activeRuns.Values)
            {
                if (runState.RegionId != regionId || runState.Status != MythicRiftRunStatus.Active)
                    continue;

                RegisterParticipantsFromEvent(runState, evt);

                if (TryApplyPlayerDeathPenalty(runState, evt))
                    continue;

                if (IsExpectedBossKill(runState, evt))
                {
                    runState.MarkBossDefeated(evt.Defender.Id);
                    if (runState.Config.UseBossGauntletMode)
                        DestroyDefeatedRiftEntity(evt.Defender);

                    if (runState.Config.UseBossGauntletMode)
                    {
                        ClearBossGauntletResidualEnemies(runState, reason: "boss-defeated");

                        if (runState.BossKillCount >= runState.Config.RequiredBossKillCount)
                        {
                            AdvanceBossGauntletWave(runState, Game.CurrentTime);
                            Logger.Info($"Mythic Rift run {runState.Config.RunId} advanced Boss Gauntlet after defeating wave {runState.BossGauntletCompletedWaves}.");
                        }
                        else
                        {
                            int bossesRemaining = Math.Max(runState.Config.RequiredBossKillCount - runState.BossKillCount, 0);
                            _nextCheckpointBossSpawnRetryAt[runState.Config.RunId] = Game.CurrentTime + BossGauntletInterBossRestInterval;
                            NotifyRunPlayers(runState, $"[Mythic Rift] Gauntlet boss defeated. {bossesRemaining} boss(es) remaining in wave {runState.Config.WaveNumber}. Next boss incoming.");
                        }

                        continue;
                    }

                    if (runState.BossUnlocked && runState.BossKillCount >= runState.Config.RequiredBossKillCount)
                    {
                        CaptureSuccessfulCompletionEligibility(runState);
                        CompleteRunSuccess(runState, Game.CurrentTime);
                        Logger.Info($"Mythic Rift run {runState.Config.RunId} completed after defeating {runState.BossKillCount}/{runState.Config.RequiredBossKillCount} Rift bosses.");
                    }
                    else
                    {
                        int bossesRemaining = Math.Max(runState.Config.RequiredBossKillCount - runState.BossKillCount, 0);
                        NotifyRunPlayers(runState, $"[Mythic Rift] Rift boss defeated. {bossesRemaining} boss(es) remaining.");
                    }

                    continue;
                }

                if (runState.Config.Content.BossOnlyCheckpointEligible)
                    continue;

                bool shouldCountKill = ShouldCountKill(evt);
                if (runState.BossUnlocked)
                {
                    if (runState.BossSpawnCount < runState.Config.RequiredBossKillCount && shouldCountKill)
                    {
                        if (TrySpawnConfiguredBoss(runState))
                        {
                            CaptureBossUnlockEligibility(runState);
                            NotifyBossUnlocked(runState);
                        }
                        else
                        {
                            Logger.Warn($"Mythic Rift run {runState.Config.RunId} failed to retry spawn for boss {runState.Config.BossProtoRef.GetNameFormatted() ?? "unknown"} after quota unlock.");
                        }
                    }

                    continue;
                }

                if (shouldCountKill == false)
                    continue;

                int previousKillCount = runState.CurrentKillCount;
                int killCredit = GetKillCountCredit(runState, evt);
                runState.AddKills(killCredit);
                runState.RemoveCustomPopulationEntity(evt.Defender.Id);
                if (runState.IsMilestoneMiniBossEntity(evt.Defender.Id))
                    DestroyDefeatedRiftEntity(evt.Defender);
                RefreshRiftHudWidgets(runState, Game.CurrentTime);
                TrySpawnPendingMilestoneEncounters(runState, allowAfterBossUnlock: true);

                if (runState.BossUnlocked && previousKillCount < runState.Config.KillQuota)
                {
                    QueueRiftReadyCheck(runState, Game.CurrentTime, "Rift boss wave");
                    CaptureBossUnlockEligibility(runState);
                    NotifyBossUnlocked(runState);
                    Logger.Info($"Mythic Rift run {runState.Config.RunId} unlocked its boss after reaching {runState.CurrentKillCount}/{runState.Config.KillQuota} kills.");
                }
                else
                {
                    TryNotifyKillProgress(runState);
                }
            }
        }

        private bool TryApplyPlayerDeathPenalty(MythicRiftRunState runState, in EntityDeadGameEvent evt)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active)
                return false;

            if (evt.Defender is not Avatar deadAvatar)
                return false;

            Player deadPlayer = deadAvatar.GetOwnerOfType<Player>();
            if (deadPlayer == null || IsPlayerInRunRegion(deadPlayer, runState) == false)
                return false;

            if (runState.IsParticipant(deadPlayer.DatabaseUniqueId) == false)
                return false;

            if (runState.Config.UseBossGauntletMode)
            {
                CompleteRunFailure(
                    runState,
                    Game.CurrentTime,
                    $"{deadPlayer.GetName()} fell. Boss Gauntlet rewards dropped for {runState.BossGauntletCompletedWaves} completed wave(s).");
                return true;
            }

            runState.ApplyTimePenalty(PlayerDeathTimePenalty);
            SendStartRiftTimer(runState);
            RefreshRiftHudWidgets(runState, Game.CurrentTime);

            string remaining = FormatDuration(runState.GetTimeRemaining(Game.CurrentTime));
            NotifyRunPlayers(runState, $"[Mythic Rift] {deadPlayer.GetName()} died. Death penalty: -{(int)PlayerDeathTimePenalty.TotalSeconds} sec. Time remaining: {remaining}.");
            Logger.Info($"Mythic Rift run {runState.Config.RunId} applied a {PlayerDeathTimePenalty.TotalSeconds:0}s death penalty to playerDbId=0x{deadPlayer.DatabaseUniqueId:X}.");

            if (runState.HasExpired(Game.CurrentTime))
                CompleteRunFailure(runState, Game.CurrentTime, "Time expired after a death penalty. No completion rewards.", returnParticipantsToHub: true);

            return true;
        }

        private static bool IsExpectedBossKill(MythicRiftRunState runState, in EntityDeadGameEvent evt)
        {
            if (runState == null || evt.Defender == null)
                return false;

            if (runState.BossUnlocked == false)
                return false;

            if (runState.IsTrackedBoss(evt.Defender.Id))
                return true;

            if (runState.BossSpawnCount > 0)
                return false;

            PrototypeId expectedBossRef = runState.Config.BossProtoRef;
            if (expectedBossRef == PrototypeId.Invalid)
                return false;

            return evt.Defender.IsAPrototype(expectedBossRef);
        }

        private static void RegisterParticipantsFromEvent(MythicRiftRunState runState, in EntityDeadGameEvent evt)
        {
            if (runState == null)
                return;

            if (evt.Killer != null)
                runState.MarkParticipantSeenInRunRegion(evt.Killer.DatabaseUniqueId);

            Player attackerOwner = evt.Attacker?.GetOwnerOfType<Player>();
            if (attackerOwner != null)
                runState.MarkParticipantSeenInRunRegion(attackerOwner.DatabaseUniqueId);

            if (evt.Defender?.PlayerTags == null)
                return;

            foreach (Player taggedPlayer in evt.Defender.PlayerTags.GetPlayers())
                runState.MarkParticipantSeenInRunRegion(taggedPlayer.DatabaseUniqueId);
        }

        private void RegisterRegionPlayersAsParticipants(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return;

            foreach (Player player in new PlayerIterator(region))
            {
                bool newlyRegistered = false;
                if (runState.IsParticipant(player.DatabaseUniqueId) == false)
                {
                    if (runState.AdmissionFinalized)
                        continue;

                    newlyRegistered = runState.RegisterParticipant(player.DatabaseUniqueId);
                }

                runState.MarkParticipantSeenInRunRegion(player.DatabaseUniqueId);

                if (runState.Status == MythicRiftRunStatus.Active)
                    TrySendRiftEntryBanner(runState, player);

                if (newlyRegistered && runState.Status == MythicRiftRunStatus.Active)
                {
                    SendStartRiftTimer(runState, player);
                    Game.ChatManager.SendChatFromCustomSystem(
                        player,
                        BuildJoinMessage(runState, Game.CurrentTime),
                        showSender: false);
                }
            }
        }

        private void TrySendRiftEntryBanner(MythicRiftRunState runState, Player player)
        {
            if (runState == null || player == null || runState.Status != MythicRiftRunStatus.Active)
                return;

            if (runState.MarkRiftEntryBannerSent(player.DatabaseUniqueId) == false)
                return;

            LocaleStringId bannerText = GetRiftEntryBannerLocaleStringId(runState.Config.RiftLevel);
            if (bannerText == LocaleStringId.Invalid)
            {
                Logger.Warn($"Mythic Rift run {runState.Config.RunId} skipped entry banner because level {runState.Config.RiftLevel} is outside the localized banner range 1-{RiftEntryBannerLocalizedLevelLimit}.");
                return;
            }

            player.SendBannerMessage(
                bannerText,
                TextStylePrototype.BannerMessageLarge,
                RiftEntryBannerTimeToLiveMS,
                BannerMessageStyle.FlyIn,
                doNotQueue: true,
                showImmediately: true);
        }

        private HashSet<ulong> BuildEligibleLaunchRoster(Player requester, Party party)
        {
            HashSet<ulong> launchRoster = new();
            if (requester?.DatabaseUniqueId == 0)
                return launchRoster;

            launchRoster.Add(requester.DatabaseUniqueId);
            if (party == null)
                return launchRoster;

            Region requesterRegion = requester.GetRegion();
            if (requesterRegion == null)
                return launchRoster;

            foreach (var kvp in party)
            {
                ulong memberDbId = kvp.Value.PlayerDbId;
                if (memberDbId == 0 || memberDbId == requester.DatabaseUniqueId)
                    continue;

                Player member = Game.EntityManager.GetEntityByDbGuid<Player>(memberDbId);
                if (member?.GetRegion()?.Id == requesterRegion.Id)
                    launchRoster.Add(memberDbId);
            }

            return launchRoster;
        }

        private static void RegisterInitialParticipants(MythicRiftRunState runState, IEnumerable<ulong> launchRoster)
        {
            if (runState == null || launchRoster == null)
                return;

            runState.EnableAdmissionTracking();
            foreach (ulong playerDbId in launchRoster)
                runState.RegisterParticipant(playerDbId);
        }

        public MythicRiftRewardOutcome PreviewRewardOutcome(MythicRiftRunState runState)
        {
            return ResolveRewardOutcome(runState);
        }

        private MythicRiftRewardOutcome ResolveRewardOutcome(MythicRiftRunState runState)
        {
            if (runState == null)
                return null;

            if (runState.Config?.UseBossGauntletMode == true)
                return ResolveBossGauntletRewardOutcome(runState);

            bool timedSuccess = runState.Status == MythicRiftRunStatus.Success;
            bool checkpointSuccess = timedSuccess && runState.Config.Content.BossOnlyCheckpointEligible;
            MythicRiftRewardTuning tuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
            bool grantBossLoot = timedSuccess ? tuning.GrantBossLootOnSuccess : tuning.GrantBossLootOnFailure;
            if (runState.Config?.UseThirtyWaveMode == true && tuning.GrantBossLootInThirtyWaveMode == false)
                grantBossLoot = false;

            string bossLootTableSourceId = grantBossLoot ? "native-boss" : "disabled";
            string bossLootDelivery = MythicRiftRewardTuning.NormalizeDelivery(tuning.DefaultDelivery);
            PrototypeId bossLootTableProtoRef = grantBossLoot
                ? ResolvePrimaryRewardLootTable(runState, tuning, checkpointSuccess, out bossLootTableSourceId, out bossLootDelivery)
                : PrototypeId.Invalid;

            List<MythicRiftRewardExtraLootTable> extraLootTables = ResolveExtraRewardLootTables(runState, tuning, timedSuccess, checkpointSuccess);
            if (grantBossLoot)
                extraLootTables.InsertRange(0, ResolveAdditionalBossWaveLootTables(runState, tuning.DefaultDelivery));

            List<MythicRiftRewardGuaranteedItem> guaranteedItems = ResolveGuaranteedRewardItems(runState, tuning, timedSuccess, checkpointSuccess);
            guaranteedItems.AddRange(ResolveRandomItemPoolRewards(runState, tuning, timedSuccess, checkpointSuccess));

            MythicRiftRewardOutcome rewardOutcome = new()
            {
                BossLootTableProtoRef = bossLootTableProtoRef,
                BossLootTableSourceId = bossLootTableSourceId,
                BossLootDelivery = bossLootDelivery,
                RewardProfileName = tuning.ProfileName,
                TimedSuccessBonusApplied = timedSuccess,
                BonusRarityPct = timedSuccess
                    ? tuning.TimedSuccessBonusRarityPct + (checkpointSuccess ? tuning.CheckpointSuccessBonusRarityPct : 0f)
                    : tuning.FailureBonusRarityPct,
                BonusSpecialPct = timedSuccess
                    ? tuning.TimedSuccessBonusSpecialPct + (checkpointSuccess ? tuning.CheckpointSuccessBonusSpecialPct : 0f)
                    : tuning.FailureBonusSpecialPct,
                ExtraLootTables = extraLootTables,
                GuaranteedItems = guaranteedItems
            };

            runState.SetRewardOutcome(rewardOutcome);
            return rewardOutcome;
        }

        private MythicRiftRewardOutcome ResolveBossGauntletRewardOutcome(MythicRiftRunState runState)
        {
            if (runState?.Config == null)
                return null;

            int completedWaves = Math.Max(runState.BossGauntletCompletedWaves, 0);
            MythicRiftRewardTuning tuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
            List<MythicRiftRewardExtraLootTable> extraLootTables = completedWaves > 0
                ? ResolveExtraRewardLootTables(runState, tuning, timedSuccess: false, checkpointSuccess: false)
                : new();
            List<MythicRiftRewardGuaranteedItem> guaranteedItems = completedWaves > 0
                ? ResolveGuaranteedRewardItems(runState, tuning, timedSuccess: false, checkpointSuccess: false)
                : new();
            if (completedWaves > 0)
                guaranteedItems.AddRange(ResolveRandomItemPoolRewards(runState, tuning, timedSuccess: false, checkpointSuccess: false));

            MythicRiftRewardOutcome rewardOutcome = new()
            {
                BossLootTableProtoRef = PrototypeId.Invalid,
                BossLootTableSourceId = "boss-gauntlet/deferred",
                BossLootDelivery = "ground",
                RewardProfileName = $"{tuning.ProfileName}/boss-gauntlet-wave-{completedWaves}",
                TimedSuccessBonusApplied = false,
                BonusRarityPct = Math.Min(completedWaves * 3f, 250f),
                BonusSpecialPct = Math.Min(completedWaves * 1.5f, 125f),
                ExtraLootTables = extraLootTables,
                GuaranteedItems = guaranteedItems
            };

            runState.SetRewardOutcome(rewardOutcome);
            return rewardOutcome;
        }

        private PrototypeId ResolvePrimaryRewardLootTable(MythicRiftRunState runState, MythicRiftRewardTuning tuning, bool checkpointSuccess, out string sourceId, out string delivery)
        {
            sourceId = "native-boss";
            delivery = MythicRiftRewardTuning.NormalizeDelivery(tuning?.DefaultDelivery);
            if (runState?.Config == null)
                return PrototypeId.Invalid;

            if (tuning?.PrimaryLootTableOverrides != null)
            {
                foreach (MythicRiftPrimaryLootTableTuning entry in tuning.PrimaryLootTableOverrides)
                {
                    if (entry == null || entry.AppliesTo(runState, checkpointSuccess) == false)
                        continue;

                    string lootTablePrototype = tuning.ResolveLootTableReference(entry.LootTablePrototype);
                    PrototypeId overrideLootTableProtoRef = ResolvePrototype(lootTablePrototype);
                    if (overrideLootTableProtoRef == PrototypeId.Invalid || overrideLootTableProtoRef.As<LootTablePrototype>() == null)
                    {
                        Logger.Warn($"Mythic Rift reward tuning skipped invalid primary loot table override id={entry.Id} lootTable={lootTablePrototype}");
                        continue;
                    }

                    sourceId = entry.Id;
                    delivery = MythicRiftRewardTuning.NormalizeDelivery(entry.Delivery);
                    return overrideLootTableProtoRef;
                }
            }

            return runState.Config.BossLootTableProtoRef;
        }

        private static List<MythicRiftRewardExtraLootTable> ResolveAdditionalBossWaveLootTables(MythicRiftRunState runState, string defaultDelivery)
        {
            List<MythicRiftRewardExtraLootTable> resolvedTables = new();
            if (runState?.Config?.BossWaveContent == null)
                return resolvedTables;

            foreach (MythicRiftContentEntry bossContent in runState.Config.BossWaveContent.Skip(1))
            {
                if (bossContent?.BossLootTableProtoRef == PrototypeId.Invalid)
                    continue;

                resolvedTables.Add(new()
                {
                    Id = $"wave-boss/{bossContent.Id}",
                    LootTableProtoRef = bossContent.BossLootTableProtoRef,
                    Rolls = 1,
                    ChancePercent = 100f,
                    Delivery = MythicRiftRewardTuning.NormalizeDelivery(defaultDelivery)
                });
            }

            return resolvedTables;
        }

        private List<MythicRiftRewardExtraLootTable> ResolveExtraRewardLootTables(MythicRiftRunState runState, MythicRiftRewardTuning tuning, bool timedSuccess, bool checkpointSuccess)
        {
            List<MythicRiftRewardExtraLootTable> resolvedTables = new();
            if (runState?.Config == null || tuning?.ExtraLootTables == null)
                return resolvedTables;

            foreach (MythicRiftExtraLootTableTuning entry in tuning.ExtraLootTables)
            {
                if (entry == null || entry.AppliesTo(runState, timedSuccess, checkpointSuccess) == false)
                    continue;

                string lootTablePrototype = tuning.ResolveLootTableReference(entry.LootTablePrototype);
                PrototypeId lootTableProtoRef = ResolvePrototype(lootTablePrototype);
                if (lootTableProtoRef == PrototypeId.Invalid || lootTableProtoRef.As<LootTablePrototype>() == null)
                {
                    Logger.Warn($"Mythic Rift reward tuning skipped invalid extra loot table id={entry.Id} lootTable={lootTablePrototype}");
                    continue;
                }

                resolvedTables.Add(new()
                {
                    Id = entry.Id,
                    LootTableProtoRef = lootTableProtoRef,
                    Rolls = Math.Max(entry.Rolls, 1),
                    ChancePercent = entry.ChancePercent,
                    ItemLevel = entry.ItemLevel,
                    Delivery = MythicRiftRewardTuning.NormalizeDelivery(entry.Delivery)
                });
            }

            foreach (MythicRiftRewardRecipeTuning recipe in tuning.RewardRecipes)
            {
                if (recipe == null || recipe.AppliesTo(runState, timedSuccess, checkpointSuccess) == false)
                    continue;

                foreach (MythicRiftRewardRecipeTableTuning table in recipe.Tables)
                {
                    if (table == null || table.ChancePercent <= 0f)
                        continue;

                    string lootTablePrototype = tuning.ResolveLootTableReference(table.LootTable);
                    PrototypeId lootTableProtoRef = ResolvePrototype(lootTablePrototype);
                    if (lootTableProtoRef == PrototypeId.Invalid || lootTableProtoRef.As<LootTablePrototype>() == null)
                    {
                        Logger.Warn($"Mythic Rift reward tuning skipped invalid recipe table recipe={recipe.Id} table={table.Id} lootTable={lootTablePrototype}");
                        continue;
                    }

                    resolvedTables.Add(new()
                    {
                        Id = $"{recipe.Id}/{table.Id}",
                        LootTableProtoRef = lootTableProtoRef,
                        Rolls = Math.Max(table.Rolls, 1),
                        ChancePercent = table.ChancePercent,
                        ItemLevel = table.ItemLevel,
                        Delivery = MythicRiftRewardTuning.NormalizeDelivery(table.Delivery)
                    });
                }
            }

            return resolvedTables;
        }

        private List<MythicRiftRewardGuaranteedItem> ResolveGuaranteedRewardItems(
            MythicRiftRunState runState,
            MythicRiftRewardTuning tuning,
            bool timedSuccess,
            bool checkpointSuccess)
        {
            List<MythicRiftRewardGuaranteedItem> resolvedItems = new();
            if (runState?.Config == null || tuning?.GuaranteedItems == null)
                return resolvedItems;

            foreach (MythicRiftGuaranteedItemTuning entry in tuning.GuaranteedItems)
            {
                if (entry == null || entry.AppliesTo(runState, timedSuccess, checkpointSuccess) == false)
                    continue;

                PrototypeId itemProtoRef = ResolveGuaranteedItemPrototype(entry);
                bool isItemReward = itemProtoRef != PrototypeId.Invalid && GameDatabase.DataDirectory.PrototypeIsA<ItemPrototype>(itemProtoRef);
                bool isAgentReward = itemProtoRef != PrototypeId.Invalid && GameDatabase.DataDirectory.PrototypeIsA<AgentPrototype>(itemProtoRef);
                if (isItemReward == false && isAgentReward == false)
                {
                    Logger.Warn($"Mythic Rift reward tuning skipped invalid guaranteed item id={entry.Id} runtimeId={entry.ItemPrototypeRuntimeId} prototype={entry.ItemPrototypeName}");
                    continue;
                }

                resolvedItems.Add(new()
                {
                    Id = entry.Id,
                    ItemProtoRef = itemProtoRef,
                    IsAgentReward = isAgentReward,
                    Quantity = GetGuaranteedRewardQuantity(entry, runState),
                    Delivery = MythicRiftRewardTuning.NormalizeDelivery(entry.Delivery)
                });
            }

            return resolvedItems;
        }

        private static PrototypeId ResolveGuaranteedItemPrototype(MythicRiftGuaranteedItemTuning entry)
        {
            if (entry == null)
                return PrototypeId.Invalid;

            if (entry.ItemPrototypeRuntimeId != 0)
                return (PrototypeId)entry.ItemPrototypeRuntimeId;

            return ResolvePrototype(entry.ItemPrototypeName);
        }

        private static int GetGuaranteedRewardQuantity(MythicRiftGuaranteedItemTuning entry, MythicRiftRunState runState)
        {
            if (entry == null)
                return 1;

            if (entry.QuantityMatchesRewardWave == false)
                return Math.Max(entry.Quantity, 1);

            int rewardWave = Math.Max(MythicRiftRewardTuning.GetRewardWaveNumber(runState), entry.MinWave);
            int waveQuantity = entry.QuantityCap > 0 ? Math.Min(rewardWave, entry.QuantityCap) : rewardWave;

            if (entry.CumulativeWaveQuantity == false)
                return Math.Max(waveQuantity, 1);

            int quantity = 0;
            for (int wave = entry.MinWave; wave <= rewardWave; wave++)
                quantity += entry.QuantityCap > 0 ? Math.Min(wave, entry.QuantityCap) : wave;

            return Math.Max(quantity, 1);
        }

        private List<MythicRiftRewardGuaranteedItem> ResolveRandomItemPoolRewards(
            MythicRiftRunState runState,
            MythicRiftRewardTuning tuning,
            bool timedSuccess,
            bool checkpointSuccess)
        {
            List<MythicRiftRewardGuaranteedItem> resolvedItems = new();
            if (runState?.Config == null || tuning?.RandomItemPools == null)
                return resolvedItems;

            foreach (MythicRiftRandomItemPoolTuning entry in tuning.RandomItemPools)
            {
                if (entry == null || entry.AppliesTo(runState, timedSuccess, checkpointSuccess) == false)
                    continue;

                IReadOnlyList<PrototypeId> candidates = ResolveRewardItemPool(entry);
                if (candidates.Count == 0)
                {
                    Logger.Warn($"Mythic Rift reward tuning found no eligible items for random pool id={entry.Id} directory={entry.PrototypeDirectoryPrefix} explicitItems={entry.ItemPrototypePaths?.Count ?? 0}");
                    continue;
                }

                for (int roll = 0; roll < entry.Rolls; roll++)
                {
                    if (entry.ChancePercent <= 0f ||
                        (entry.ChancePercent < 100f && Game.Random.NextFloat() * 100f >= entry.ChancePercent))
                    {
                        continue;
                    }

                    PrototypeId itemProtoRef = candidates[Game.Random.Next(0, candidates.Count)];
                    resolvedItems.Add(new()
                    {
                        Id = $"{entry.Id}/{itemProtoRef.GetNameFormatted()}",
                        ItemProtoRef = itemProtoRef,
                        Quantity = 1,
                        ItemLevel = entry.ItemLevel,
                        Delivery = MythicRiftRewardTuning.NormalizeDelivery(entry.Delivery)
                    });
                }
            }

            return resolvedItems;
        }

        private IReadOnlyList<PrototypeId> ResolveRewardItemPool(MythicRiftRandomItemPoolTuning entry)
        {
            if (entry?.ItemPrototypePaths != null && entry.ItemPrototypePaths.Count > 0)
            {
                List<PrototypeId> candidates = new();
                foreach (string itemPrototypePath in entry.ItemPrototypePaths)
                {
                    PrototypeId itemProtoRef = ResolvePrototype(itemPrototypePath);
                    ItemPrototype itemProto = itemProtoRef.As<ItemPrototype>();
                    if (itemProtoRef == PrototypeId.Invalid || itemProto == null)
                    {
                        Logger.Warn($"Mythic Rift reward tuning skipped invalid explicit random item pool item id={entry.Id} item={itemPrototypePath}");
                        continue;
                    }

                    if (itemProto.IsLiveTuningEnabled() == false)
                    {
                        Logger.Warn($"Mythic Rift reward tuning skipped disabled explicit random item pool item id={entry.Id} item={itemPrototypePath}");
                        continue;
                    }

                    candidates.Add(itemProtoRef);
                }

                return candidates;
            }

            return ResolveRewardItemPool(entry?.PrototypeDirectoryPrefix);
        }

        private IReadOnlyList<PrototypeId> ResolveRewardItemPool(string prototypeDirectoryPrefix)
        {
            string normalizedPrefix = prototypeDirectoryPrefix?.Trim().Replace('\\', '/') ?? string.Empty;
            if (normalizedPrefix.Length > 0 && normalizedPrefix.EndsWith('/') == false)
                normalizedPrefix += "/";

            if (string.IsNullOrWhiteSpace(normalizedPrefix))
                return Array.Empty<PrototypeId>();

            if (_rewardItemPoolsByDirectory.TryGetValue(normalizedPrefix, out IReadOnlyList<PrototypeId> cachedPool))
                return cachedPool;

            List<PrototypeId> candidates = new();
            foreach (PrototypeId itemProtoRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<ItemPrototype>(
                         PrototypeIterateFlags.NoAbstractApprovedOnly))
            {
                string prototypeName = GameDatabase.GetPrototypeName(itemProtoRef);
                if (prototypeName.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                ItemPrototype itemProto = itemProtoRef.As<ItemPrototype>();
                if (itemProto?.IsLiveTuningEnabled() != true)
                    continue;

                candidates.Add(itemProtoRef);
            }

            candidates.Sort((left, right) => string.Compare(
                GameDatabase.GetPrototypeName(left),
                GameDatabase.GetPrototypeName(right),
                StringComparison.OrdinalIgnoreCase));
            _rewardItemPoolsByDirectory[normalizedPrefix] = candidates;
            Logger.Info($"Mythic Rift resolved random reward item pool directory={normalizedPrefix} candidates={candidates.Count}.");
            return candidates;
        }

        private void GrantProgressionForSuccessfulRun(MythicRiftRunState runState)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Success)
                return;

            HashSet<ulong> recipientDbIds = new(runState.ProgressionEligiblePlayerDbIds);
            if (recipientDbIds.Count == 0)
            {
                Logger.Info($"Mythic Rift run {runState.Config.RunId} completed but no players satisfied the competitive progression rule for level unlocks.");
                return;
            }

            foreach (ulong playerDbId in recipientDbIds)
            {
                int unlockedLevel = GrantNextRiftLevel(playerDbId, runState.Config.RiftLevel, runState.Config.Mode);
                Logger.Info($"Mythic Rift run {runState.Config.RunId} unlocked {GetModeDisplayName(runState.Config.Mode)} level {unlockedLevel} for playerDbId=0x{playerDbId:X}.");
            }
        }

        private HashSet<string> BuildRandomMapExclusions(Player requester, Party party)
        {
            HashSet<string> excludedContentIds = new(StringComparer.OrdinalIgnoreCase);
            TryAddPlayerRandomMapExclusions(excludedContentIds, requester);
            if (party == null)
                return excludedContentIds;

            foreach (var kvp in party)
            {
                Player partyMember = Game.EntityManager.GetEntityByDbGuid<Player>(kvp.Value.PlayerDbId);
                TryAddPlayerRandomMapExclusions(excludedContentIds, partyMember, kvp.Value.PlayerDbId);
            }

            return excludedContentIds;
        }

        private void TryAddPlayerRandomMapExclusions(HashSet<string> excludedContentIds, Player player, ulong playerDbId = 0)
        {
            if (excludedContentIds == null)
                return;

            ulong resolvedPlayerDbId = player?.DatabaseUniqueId ?? playerDbId;
            TryAddLastCompletedMapContentId(excludedContentIds, resolvedPlayerDbId);
            TryAddRecentRandomMapContentIds(excludedContentIds, resolvedPlayerDbId);
            TryAddCurrentRegionMapContentId(excludedContentIds, player?.GetRegion());
        }

        private void TryAddLastCompletedMapContentId(HashSet<string> excludedContentIds, ulong playerDbId)
        {
            if (excludedContentIds == null || playerDbId == 0)
                return;

            if (_lastCompletedMapContentIdByPlayer.TryGetValue(playerDbId, out string contentId) == false)
                return;

            if (string.IsNullOrWhiteSpace(contentId))
                return;

            excludedContentIds.Add(contentId);
        }

        private void TryAddRecentRandomMapContentIds(HashSet<string> excludedContentIds, ulong playerDbId)
        {
            if (excludedContentIds == null || playerDbId == 0)
                return;

            if (_recentRandomMapContentIdsByPlayer.TryGetValue(playerDbId, out List<string> recentContentIds) == false)
                return;

            foreach (string recentContentId in recentContentIds)
            {
                if (string.IsNullOrWhiteSpace(recentContentId) == false)
                    excludedContentIds.Add(recentContentId);
            }
        }

        private void TryAddCurrentRegionMapContentId(HashSet<string> excludedContentIds, Region region)
        {
            if (excludedContentIds == null || region == null)
                return;

            MythicRiftContentEntry currentContent = ResolveContentByRegion(region);
            string contentId = currentContent?.Id;
            if (string.IsNullOrWhiteSpace(contentId))
                return;

            excludedContentIds.Add(contentId);
        }

        private MythicRiftContentEntry ResolveContentByRegion(Region region)
        {
            if (region == null)
                return null;

            return _contentPool.FirstOrDefault(content => ContentMatchesRegion(content, region));
        }

        private static bool ContentMatchesRegion(MythicRiftContentEntry content, Region region)
        {
            if (content == null || region == null)
                return false;

            if (region.PrototypeDataRef == content.RegionProtoRef)
                return true;

            RegionPrototype currentRegionProto = region.Prototype;
            RegionPrototype expectedRegionProto = content.RegionProtoRef.As<RegionPrototype>();
            if (AreRegionsEquivalent(expectedRegionProto, currentRegionProto))
                return true;

            RegionConnectionTargetPrototype startTargetProto = content.StartTargetProtoRef.As<RegionConnectionTargetPrototype>();
            RegionPrototype startTargetRegionProto = startTargetProto?.Region.As<RegionPrototype>();
            return AreRegionsEquivalent(startTargetRegionProto, currentRegionProto);
        }

        private void TrackLastCompletedMapContent(MythicRiftRunState runState)
        {
            string contentId = runState?.Config?.Content?.Id;
            if (string.IsNullOrWhiteSpace(contentId))
                return;

            foreach (ulong playerDbId in runState.ParticipantPlayerDbIds)
            {
                if (playerDbId != 0)
                {
                    _lastCompletedMapContentIdByPlayer[playerDbId] = contentId;
                    TrackRecentRandomMapContentId(playerDbId, contentId);
                }
            }
        }

        private void TrackRecentlySelectedMapContent(MythicRiftRunState runState)
        {
            string contentId = runState?.Config?.Content?.Id;
            if (string.IsNullOrWhiteSpace(contentId))
                return;

            foreach (ulong playerDbId in runState.ParticipantPlayerDbIds)
                TrackRecentRandomMapContentId(playerDbId, contentId);
        }

        private void TrackRecentRandomMapContentId(ulong playerDbId, string contentId)
        {
            if (playerDbId == 0 || string.IsNullOrWhiteSpace(contentId))
                return;

            if (_recentRandomMapContentIdsByPlayer.TryGetValue(playerDbId, out List<string> recentContentIds) == false)
            {
                recentContentIds = new();
                _recentRandomMapContentIdsByPlayer[playerDbId] = recentContentIds;
            }

            recentContentIds.RemoveAll(existingContentId => string.Equals(existingContentId, contentId, StringComparison.OrdinalIgnoreCase));
            recentContentIds.Add(contentId);

            while (recentContentIds.Count > RecentRandomMapHistoryLimit)
                recentContentIds.RemoveAt(0);
        }

        private HashSet<string> BuildRandomBossFamilyExclusions(Player requester, Party party)
        {
            HashSet<string> excludedBossFamilies = new(StringComparer.OrdinalIgnoreCase);
            TryAddPlayerRandomBossFamilyExclusions(excludedBossFamilies, requester);
            if (party == null)
                return excludedBossFamilies;

            foreach (var kvp in party)
            {
                Player partyMember = Game.EntityManager.GetEntityByDbGuid<Player>(kvp.Value.PlayerDbId);
                TryAddPlayerRandomBossFamilyExclusions(excludedBossFamilies, partyMember, kvp.Value.PlayerDbId);
            }

            return excludedBossFamilies;
        }

        private HashSet<string> BuildRunBossFamilyExclusions(MythicRiftRunState runState)
        {
            HashSet<string> excludedBossFamilies = new(StringComparer.OrdinalIgnoreCase);
            if (runState == null)
                return excludedBossFamilies;

            foreach (ulong playerDbId in runState.ParticipantPlayerDbIds)
                TryAddRecentRandomBossFamilies(excludedBossFamilies, playerDbId);

            foreach (MythicRiftContentEntry bossContent in runState.Config.BossWaveContent)
            {
                string bossFamily = NormalizeBossFamily(bossContent?.BossFamily);
                if (string.IsNullOrWhiteSpace(bossFamily) == false)
                    excludedBossFamilies.Add(bossFamily);
            }

            return excludedBossFamilies;
        }

        private void TryAddPlayerRandomBossFamilyExclusions(HashSet<string> excludedBossFamilies, Player player, ulong playerDbId = 0)
        {
            if (excludedBossFamilies == null)
                return;

            ulong resolvedPlayerDbId = player?.DatabaseUniqueId ?? playerDbId;
            TryAddRecentRandomBossFamilies(excludedBossFamilies, resolvedPlayerDbId);
        }

        private void TryAddRecentRandomBossFamilies(HashSet<string> excludedBossFamilies, ulong playerDbId)
        {
            if (excludedBossFamilies == null || playerDbId == 0)
                return;

            if (_recentRandomBossFamiliesByPlayer.TryGetValue(playerDbId, out List<string> recentBossFamilies) == false)
                return;

            foreach (string recentBossFamily in recentBossFamilies)
            {
                string normalizedFamily = NormalizeBossFamily(recentBossFamily);
                if (string.IsNullOrWhiteSpace(normalizedFamily) == false)
                    excludedBossFamilies.Add(normalizedFamily);
            }
        }

        private void TrackRecentlySelectedBossFamilies(MythicRiftRunState runState)
        {
            if (runState?.Config?.BossWaveContent == null)
                return;

            List<string> selectedFamilies = runState.Config.BossWaveContent
                .Select(content => NormalizeBossFamily(content?.BossFamily))
                .Where(family => string.IsNullOrWhiteSpace(family) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedFamilies.Count == 0)
                return;

            foreach (ulong playerDbId in runState.ParticipantPlayerDbIds)
            {
                foreach (string bossFamily in selectedFamilies)
                    TrackRecentRandomBossFamily(playerDbId, bossFamily);
            }
        }

        private void TrackRecentRandomBossFamily(ulong playerDbId, string bossFamily)
        {
            bossFamily = NormalizeBossFamily(bossFamily);
            if (playerDbId == 0 || string.IsNullOrWhiteSpace(bossFamily))
                return;

            if (_recentRandomBossFamiliesByPlayer.TryGetValue(playerDbId, out List<string> recentBossFamilies) == false)
            {
                recentBossFamilies = new();
                _recentRandomBossFamiliesByPlayer[playerDbId] = recentBossFamilies;
            }

            recentBossFamilies.RemoveAll(existingFamily => string.Equals(existingFamily, bossFamily, StringComparison.OrdinalIgnoreCase));
            recentBossFamilies.Add(bossFamily);

            while (recentBossFamilies.Count > RecentRandomBossFamilyHistoryLimit)
                recentBossFamilies.RemoveAt(0);
        }

        private void CaptureBossUnlockEligibility(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            runState.SnapshotBossUnlockEligiblePlayers(GetCurrentRunRegionPlayerDbIds(runState));
        }

        private void CaptureSuccessfulCompletionEligibility(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            HashSet<ulong> currentRunRegionPlayerDbIds = GetCurrentRunRegionPlayerDbIds(runState);
            if (runState.Config.Content.BossOnlyCheckpointEligible)
                runState.SnapshotBossUnlockEligiblePlayers(currentRunRegionPlayerDbIds);

            runState.SnapshotProgressionEligiblePlayers(currentRunRegionPlayerDbIds);
        }

        private HashSet<ulong> GetCurrentRunRegionPlayerDbIds(MythicRiftRunState runState)
        {
            HashSet<ulong> playerDbIds = new();
            if (runState == null || runState.RegionId == 0)
                return playerDbIds;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return playerDbIds;

            foreach (Player player in new PlayerIterator(region))
            {
                if (player?.DatabaseUniqueId != 0 &&
                    runState.IsParticipant(player.DatabaseUniqueId) &&
                    runState.HasParticipantLeftEarly(player.DatabaseUniqueId) == false)
                    playerDbIds.Add(player.DatabaseUniqueId);
            }

            return playerDbIds;
        }

        private static bool ShouldCountKill(in EntityDeadGameEvent evt)
        {
            if (evt.Defender == null)
                return false;

            if (evt.Defender is Avatar)
                return false;

            if (evt.Defender is Agent == false)
                return false;

            if (evt.Defender.IsHostileToPlayers() == false)
                return false;

            if (evt.Defender.WorldEntityPrototype?.MissionEntityDeathCredit == false)
                return false;

            if (evt.Killer != null)
                return true;

            if (evt.Attacker?.GetOwnerOfType<Player>() != null)
                return true;

            return evt.Defender.PlayerTags.HasTags;
        }

        private static int GetKillCountCredit(MythicRiftRunState runState, in EntityDeadGameEvent evt)
        {
            if (runState?.IsMilestoneMiniBossEntity(evt.Defender?.Id ?? 0) == true)
                return MilestoneMiniBossKillCredit;

            return 1;
        }

        private static void DestroyDefeatedRiftEntity(WorldEntity entity)
        {
            if (entity == null || entity.IsDestroyed)
                return;

            EntityHelper.ClearStandaloneBossFixups(entity.Id);

            if (entity is Agent agent)
                agent.KillSummonedOnOwnerDeath();

            if (entity.IsInWorld)
                entity.ExitWorld();

            entity.Destroy();
        }

        private MythicRiftRunConfig CreateRunConfig(
            MythicRiftContentEntry content,
            MythicRiftContentEntry bossContent,
            int riftLevel,
            int requestedPlayerCount,
            int killQuota,
            TimeSpan timeLimit,
            MythicRiftMode mode,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            if (content == null || bossContent == null)
                return null;

            if (content.HasValidMap == false ||
                content.SupportsPlayerCount(requestedPlayerCount) == false ||
                bossContent.HasValidBossSource == false)
                return null;

            bool useThirtyWaveMode = mode == MythicRiftMode.Endless;
            bool useBossGauntletMode = mode == MythicRiftMode.BossGauntlet;
            MythicRiftWaveProfile waveProfile = MythicRiftScaling.GetThirtyWaveProfile(riftLevel);
            int waveNumber = useBossGauntletMode
                ? Math.Max(riftLevel, 1)
                : useThirtyWaveMode ? waveProfile.Wave : Math.Max(riftLevel, 1);
            int rewardRiftLevel = useThirtyWaveMode
                ? waveNumber
                : Math.Max(riftLevel, 1);
            MythicRiftDifficultySnapshot difficulty = MythicRiftScaling.BuildSnapshot(waveNumber, requestedPlayerCount, mode);
            int requestedBossCount = useBossGauntletMode
                ? MythicRiftScaling.GetBossGauntletBossCount(waveNumber)
                : useThirtyWaveMode ? waveProfile.BossCount : 1;
            IReadOnlyList<MythicRiftContentEntry> bossWaveContent = MythicRiftBossWaveSelector.BuildDistinctRoster(
                bossContent,
                _contentPool.Where(entry => entry.RandomBossEligible && entry.HasValidBossSource),
                requestedBossCount,
                count => Game.Random.Next(0, count),
                excludedBossFamilies);
            if (bossWaveContent.Count == 0)
                return null;

            IReadOnlyList<PrototypeId> regionAffixes = useBossGauntletMode
                ? Array.Empty<PrototypeId>()
                : RollRiftRegionAffixes(content, waveNumber, useBossScopedAffixes: false);
            IReadOnlyList<PrototypeId> bossAffixes = useBossGauntletMode
                ? RollRiftRegionAffixes(content, waveNumber, useBossScopedAffixes: true)
                : Array.Empty<PrototypeId>();

            int resolvedKillQuota = useBossGauntletMode
                ? 1
                : content.BossOnlyCheckpointEligible
                ? 1
                : ResolveKillQuota(content, killQuota);

            return new MythicRiftRunConfig
            {
                RunId = _nextRunId++,
                RiftLevel = rewardRiftLevel,
                Content = content,
                BossContent = bossContent,
                BossWaveContent = bossWaveContent,
                RequestedPlayerCount = Math.Max(requestedPlayerCount, 1),
                EffectivePlayerCount = difficulty.EffectivePlayerCount,
                KillQuota = resolvedKillQuota,
                TimeLimit = useBossGauntletMode
                    ? TimeSpan.FromDays(1)
                    : timeLimit <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : timeLimit,
                RegionProtoRef = content.RegionProtoRef,
                StartTargetProtoRef = content.StartTargetProtoRef,
                MissionProtoRef = content.MissionProtoRef,
                BossProtoRef = bossContent.BossProtoRef,
                BossLootTableProtoRef = bossContent.BossLootTableProtoRef,
                RegionAffixes = regionAffixes,
                BossAffixes = bossAffixes,
                Difficulty = difficulty,
                Mode = mode,
                WaveNumber = waveNumber,
                RequiredBossKillCount = bossWaveContent.Count
            };
        }

        private IReadOnlyList<PrototypeId> RollRiftRegionAffixes(MythicRiftContentEntry content, int levelOrWave, bool useBossScopedAffixes)
        {
            int affixCount = GetRiftAffixCount(levelOrWave, useBossScopedAffixes);
            if (affixCount <= 0)
                return Array.Empty<PrototypeId>();

            RegionAffixTablePrototype affixTableProto = ResolveRiftAffixTable(content);
            if (affixTableProto?.RegionAffixes == null || affixTableProto.RegionAffixes.Length == 0)
                return Array.Empty<PrototypeId>();

            List<PrototypeId> pickedAffixes = new(affixCount);
            HashSet<PrototypeId> blockedAffixes = new();
            for (int pick = 0; pick < affixCount; pick++)
            {
                Picker<PrototypeId> picker = new(Game.Random);
                foreach (RegionAffixWeightedEntryPrototype weightedEntryProto in affixTableProto.RegionAffixes)
                {
                    if (weightedEntryProto == null ||
                        weightedEntryProto.Affix == PrototypeId.Invalid ||
                        weightedEntryProto.Weight <= 0 ||
                        pickedAffixes.Contains(weightedEntryProto.Affix) ||
                        blockedAffixes.Contains(weightedEntryProto.Affix))
                    {
                        continue;
                    }

                    RegionAffixPrototype affixProto = weightedEntryProto.Affix.As<RegionAffixPrototype>();
                    if (IsUsableRiftEnemyAffix(affixProto) == false)
                        continue;

                    picker.Add(weightedEntryProto.Affix, weightedEntryProto.Weight);
                }

                if (picker.Pick(out PrototypeId pickedAffix) == false || pickedAffix == PrototypeId.Invalid)
                    break;

                pickedAffixes.Add(pickedAffix);
                AddRestrictedRiftAffixes(pickedAffix, blockedAffixes);
            }

            return pickedAffixes;
        }

        private static int GetRiftAffixCount(int levelOrWave, bool useBossScopedAffixes)
        {
            int secondAffixStart = useBossScopedAffixes
                ? BossGauntletSecondBossAffixStartWave
                : RiftSecondRegionAffixStartLevel;
            int thirdAffixStart = useBossScopedAffixes
                ? BossGauntletThirdBossAffixStartWave
                : RiftThirdRegionAffixStartLevel;

            int affixCount = 1;
            if (levelOrWave >= secondAffixStart)
                affixCount++;
            if (levelOrWave >= thirdAffixStart)
                affixCount++;

            return affixCount;
        }

        private static RegionAffixTablePrototype ResolveRiftAffixTable(MythicRiftContentEntry content)
        {
            RegionPrototype regionProto = content?.RegionProtoRef.As<RegionPrototype>();
            RegionAffixTablePrototype affixTableProto = regionProto?.AffixTable.As<RegionAffixTablePrototype>();
            if (affixTableProto?.RegionAffixes != null && affixTableProto.RegionAffixes.Length > 0)
                return affixTableProto;

            return GameDatabase.GetPrototype<RegionAffixTablePrototype>(
                GameDatabase.GetPrototypeRefByName(DefaultRiftAffixTablePrototypeName));
        }

        private static bool IsUsableRiftEnemyAffix(RegionAffixPrototype affixProto)
        {
            if (affixProto == null)
                return false;

            if (affixProto.EnemyBoost != PrototypeId.Invalid)
                return true;

            return affixProto.EnemyBoostsFiltered != null &&
                   affixProto.EnemyBoostsFiltered.Any(entry => entry?.EnemyBoost != PrototypeId.Invalid);
        }

        private static void AddRestrictedRiftAffixes(PrototypeId pickedAffix, HashSet<PrototypeId> blockedAffixes)
        {
            if (pickedAffix == PrototypeId.Invalid || blockedAffixes == null)
                return;

            blockedAffixes.Add(pickedAffix);
            RegionAffixPrototype pickedAffixProto = pickedAffix.As<RegionAffixPrototype>();
            if (pickedAffixProto?.RestrictsAffixes == null)
                return;

            foreach (PrototypeId restrictedAffix in pickedAffixProto.RestrictsAffixes)
            {
                if (restrictedAffix != PrototypeId.Invalid)
                    blockedAffixes.Add(restrictedAffix);
            }
        }

        private void QueueRiftReadyCheck(MythicRiftRunState runState, TimeSpan currentTime, string label)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active)
                return;

            if (runState.IsReadyCheckActive(currentTime))
                return;

            CleanupRunHazards(runState);
            runState.BeginReadyCheck(currentTime + RiftReadyCheckDuration, label);
            runState.SetNextHazardSpawnAt(currentTime + RiftReadyCheckDuration + TimeSpan.FromSeconds(4));
            NotifyRunPlayers(runState, $"[Mythic Rift] {runState.ReadyCheckLabel} starts in {(int)RiftReadyCheckDuration.TotalSeconds} seconds.");
        }

        private bool TryHoldForRiftReadyCheck(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null)
                return false;

            if (runState.IsReadyCheckActive(currentTime))
                return true;

            if (runState.ReadyCheckEndsAt.HasValue)
            {
                runState.ClearReadyCheck();
                RefreshRiftHudWidgets(runState, currentTime);
            }

            return false;
        }

        private bool TryStartBossOnlyCheckpoint(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode == true)
                return false;

            if (runState?.Config?.Content?.BossOnlyCheckpointEligible != true)
                return false;

            if (runState.Status != MythicRiftRunStatus.Active ||
                runState.RegionId == 0 ||
                runState.BossSpawnCount >= runState.Config.RequiredBossKillCount)
                return false;

            if (TryHoldForRiftReadyCheck(runState, currentTime))
                return false;

            if (_nextCheckpointBossSpawnRetryAt.TryGetValue(runState.Config.RunId, out TimeSpan nextRetryAt) && currentTime < nextRetryAt)
                return false;

            _nextCheckpointBossSpawnRetryAt[runState.Config.RunId] = currentTime + CheckpointBossSpawnRetryInterval;
            runState.UnlockBoss();
            if (TrySpawnConfiguredBoss(runState) == false)
            {
                Logger.Debug($"Mythic Rift checkpoint run {runState.Config.RunId} could not spawn boss {runState.Config.BossProtoRef.GetNameFormatted() ?? "unknown"} yet; retrying.");
                return false;
            }

            _nextCheckpointBossSpawnRetryAt.Remove(runState.Config.RunId);
            CaptureBossUnlockEligibility(runState);
            RefreshRiftHudWidgets(runState, currentTime);
            NotifyBossUnlocked(runState);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} started boss-only checkpoint at level {runState.Config.RiftLevel} in {runState.Config.Content.Id}.");
            return true;
        }

        private bool TryStartBossGauntletWave(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode != true)
                return false;

            if (runState.Status != MythicRiftRunStatus.Active ||
                runState.RegionId == 0 ||
                runState.BossSpawnCount > 0)
            {
                return false;
            }

            if (TryHoldForRiftReadyCheck(runState, currentTime))
                return false;

            if (_nextCheckpointBossSpawnRetryAt.TryGetValue(runState.Config.RunId, out TimeSpan nextSpawnAt) && currentTime < nextSpawnAt)
                return false;

            ClearBossGauntletResidualEnemies(runState, reason: "wave-start");
            _nextCheckpointBossSpawnRetryAt[runState.Config.RunId] = currentTime + CheckpointBossSpawnRetryInterval;
            runState.UnlockBoss();
            if (TrySpawnConfiguredBoss(runState) == false)
                return false;

            _nextCheckpointBossSpawnRetryAt.Remove(runState.Config.RunId);
            CaptureBossUnlockEligibility(runState);
            RefreshRiftHudWidgets(runState, currentTime);
            string modifierText = BuildRiftModifierText(runState.Config);
            string modifierSuffix = string.IsNullOrWhiteSpace(modifierText) ? string.Empty : $" Modifiers: {modifierText}.";
            NotifyRunPlayers(runState, $"[Mythic Rift] Boss Gauntlet wave {runState.Config.WaveNumber} started. Bosses this wave: {runState.Config.RequiredBossKillCount}.{modifierSuffix}");
            Logger.Info($"Mythic Rift run {runState.Config.RunId} started Boss Gauntlet wave {runState.Config.WaveNumber} with {runState.Config.RequiredBossKillCount} boss(es).");
            return true;
        }

        private bool AdvanceBossGauntletWave(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode != true || runState.Status != MythicRiftRunStatus.Active)
                return false;

            runState.MarkBossGauntletWaveCompleted();
            int nextWave = Math.Max(runState.Config.WaveNumber + 1, 1);
            MythicRiftRunConfig nextConfig = CreateBossGauntletWaveConfig(runState, nextWave);
            if (nextConfig == null)
            {
                CompleteRunFailure(runState, currentTime, "Boss Gauntlet could not prepare the next wave. Rewards granted for completed waves.");
                return false;
            }

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region != null)
                TryRestoreRegionDifficultyScaling(runState);

            runState.ReplaceConfigForNextBossGauntletWave(nextConfig);
            TrackRecentlySelectedBossFamilies(runState);
            if (region != null)
                ApplyRunDifficultyToRegion(runState, region);

            QueueRiftReadyCheck(runState, currentTime, $"Boss Gauntlet wave {nextWave}");
            _nextCheckpointBossSpawnRetryAt[runState.Config.RunId] = currentTime + BossGauntletWaveRestInterval;
            RefreshRiftHudWidgets(runState, currentTime);
            NotifyRunPlayers(runState, $"[Mythic Rift] Wave {nextWave - 1} cleared. Next Boss Gauntlet wave starts in {(int)BossGauntletWaveRestInterval.TotalSeconds} seconds.");
            return true;
        }

        private MythicRiftRunConfig CreateBossGauntletWaveConfig(MythicRiftRunState runState, int wave)
        {
            if (runState?.Config == null)
                return null;

            HashSet<string> excludedBossFamilies = BuildRunBossFamilyExclusions(runState);
            MythicRiftContentEntry bossContent = SelectRandomBossContent(runState.Config.BossContent, excludedBossFamilies);
            if (bossContent == null)
                bossContent = runState.Config.BossContent;

            int bossCount = MythicRiftScaling.GetBossGauntletBossCount(wave);
            IReadOnlyList<MythicRiftContentEntry> bossWaveContent = MythicRiftBossWaveSelector.BuildDistinctRoster(
                bossContent,
                _contentPool.Where(entry => entry.RandomBossEligible && entry.HasValidBossSource),
                bossCount,
                count => Game.Random.Next(0, count),
                excludedBossFamilies);
            if (bossWaveContent.Count == 0)
                return null;

            MythicRiftDifficultySnapshot difficulty = MythicRiftScaling.BuildSnapshot(
                wave,
                runState.Config.RequestedPlayerCount,
                MythicRiftMode.BossGauntlet);
            IReadOnlyList<PrototypeId> bossAffixes = RollRiftRegionAffixes(runState.Config.Content, wave, useBossScopedAffixes: true);

            return new MythicRiftRunConfig
            {
                RunId = runState.Config.RunId,
                RiftLevel = wave,
                Content = runState.Config.Content,
                BossContent = bossContent,
                BossWaveContent = bossWaveContent,
                RequestedPlayerCount = runState.Config.RequestedPlayerCount,
                EffectivePlayerCount = difficulty.EffectivePlayerCount,
                KillQuota = 1,
                TimeLimit = runState.Config.TimeLimit,
                RegionProtoRef = runState.Config.RegionProtoRef,
                StartTargetProtoRef = runState.Config.StartTargetProtoRef,
                MissionProtoRef = runState.Config.MissionProtoRef,
                BossProtoRef = bossContent.BossProtoRef,
                BossLootTableProtoRef = bossContent.BossLootTableProtoRef,
                RegionAffixes = runState.Config.RegionAffixes,
                BossAffixes = bossAffixes,
                Difficulty = difficulty,
                Mode = MythicRiftMode.BossGauntlet,
                WaveNumber = wave,
                RequiredBossKillCount = bossWaveContent.Count
            };
        }

        private bool TryMaintainBossWave(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.Content?.BossOnlyCheckpointEligible == true &&
                runState.Config.UseBossGauntletMode == false)
            {
                return false;
            }

            if (runState == null ||
                runState.Status != MythicRiftRunStatus.Active ||
                runState.BossUnlocked == false ||
                runState.RegionId == 0 ||
                runState.BossSpawnCount >= runState.Config.RequiredBossKillCount)
            {
                return false;
            }

            if (TryHoldForRiftReadyCheck(runState, currentTime))
                return false;

            if (_nextCheckpointBossSpawnRetryAt.TryGetValue(runState.Config.RunId, out TimeSpan nextRetryAt) && currentTime < nextRetryAt)
                return false;

            _nextCheckpointBossSpawnRetryAt[runState.Config.RunId] = currentTime + CheckpointBossSpawnRetryInterval;
            if (TrySpawnConfiguredBoss(runState) == false)
                return false;

            _nextCheckpointBossSpawnRetryAt.Remove(runState.Config.RunId);
            CaptureBossUnlockEligibility(runState);
            NotifyBossUnlocked(runState);
            return true;
        }

        private void MaintainCustomRiftPopulation(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config?.UseBossGauntletMode == true)
                return;

            if (runState?.Config?.Content?.UseCustomPopulation != true)
                return;

            if (runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0 || runState.BossUnlocked)
                return;

            if (currentTime < runState.NextCustomPopulationSpawnAt)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            int liveCount = CountLiveCustomRiftPopulationEntities(runState, region);
            int maxAlive = GetCustomRiftPopulationMaxAlive(runState);
            if (liveCount >= maxAlive)
            {
                runState.SetNextCustomPopulationSpawnAt(currentTime + CustomRiftPopulationSpawnInterval);
                return;
            }

            int targetAlive = GetCustomRiftPopulationTargetAlive(runState);
            if (liveCount >= targetAlive)
            {
                runState.SetNextCustomPopulationSpawnAt(currentTime + CustomRiftPopulationSpawnInterval);
                return;
            }

            int spawnBatch = Math.Min(GetCustomRiftPopulationSpawnBatch(runState), maxAlive - liveCount);
            int spawned = 0;
            for (int i = 0; i < spawnBatch; i++)
            {
                if (TrySpawnCustomRiftPopulationMob(runState, region))
                    spawned++;
            }

            runState.SetNextCustomPopulationSpawnAt(currentTime + CustomRiftPopulationSpawnInterval);

            if (spawned > 0)
                Logger.Debug($"Mythic Rift run {runState.Config.RunId} spawned {spawned} custom population mob(s) for {runState.Config.Content.Id}. liveBefore={liveCount} totalSpawned={runState.CustomPopulationTotalSpawned}");
        }

        private void MaintainRiftHazards(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState?.Config == null ||
                runState.Status != MythicRiftRunStatus.Active ||
                runState.RegionId == 0 ||
                runState.IsReadyCheckActive(currentTime))
            {
                return;
            }

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            CleanupExpiredHazardRefs(runState, region);
            if (ShouldRunUseEnvironmentalHazards(runState) == false)
                return;

            if (runState.HazardEntityIds.Count >= MaxActiveRiftHazards)
                return;

            if (currentTime < runState.NextHazardSpawnAt)
                return;

            runState.SetNextHazardSpawnAt(currentTime + RiftHazardSpawnInterval);
            if (TrySpawnRiftHazard(runState, region) == false)
                return;

            RefreshRiftHudWidgets(runState, currentTime);
        }

        private static bool ShouldRunUseEnvironmentalHazards(MythicRiftRunState runState)
        {
            if (runState?.Config == null)
                return false;

            if (runState.Config.UseBossGauntletMode)
                return runState.Config.WaveNumber >= 10;

            if (runState.Config.UseThirtyWaveMode)
                return runState.Config.WaveNumber >= 10;

            return runState.Config.RiftLevel >= 30 || runState.BossUnlocked;
        }

        private void CleanupExpiredHazardRefs(MythicRiftRunState runState, Region region)
        {
            if (runState == null)
                return;

            foreach (ulong hazardEntityId in runState.HazardEntityIds.ToArray())
            {
                WorldEntity hazard = Game.EntityManager.GetEntity<WorldEntity>(hazardEntityId);
                if (hazard == null || hazard.IsDestroyed || hazard.IsInWorld == false || hazard.Region != region)
                    runState.RemoveHazardEntity(hazardEntityId);
            }
        }

        private void CleanupRunHazards(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            foreach (ulong hazardEntityId in runState.HazardEntityIds.ToArray())
            {
                WorldEntity hazard = Game.EntityManager.GetEntity<WorldEntity>(hazardEntityId);
                if (hazard != null && hazard.IsDestroyed == false)
                    DestroyDefeatedRiftEntity(hazard);

                runState.RemoveHazardEntity(hazardEntityId);
            }
        }

        private bool TrySpawnRiftHazard(MythicRiftRunState runState, Region region)
        {
            IReadOnlyList<PrototypeId> hazardRefs = GetRiftHazardPrototypeRefs();
            if (hazardRefs.Count == 0)
                return false;

            Player anchorPlayer = PickBossSpawnAnchorPlayer(runState, region);
            Avatar anchorAvatar = anchorPlayer?.CurrentAvatar;
            if (anchorAvatar == null || anchorAvatar.IsAliveInWorld == false || anchorAvatar.Region != region)
                return false;

            for (int attempt = 0; attempt < Math.Min(hazardRefs.Count, 8); attempt++)
            {
                PrototypeId hazardRef = hazardRefs[Game.Random.Next(0, hazardRefs.Count)];
                HotspotPrototype hazardProto = hazardRef.As<HotspotPrototype>();
                if (hazardProto?.Bounds == null)
                    continue;

                Vector3 spawnPosition = anchorAvatar.RegionLocation.Position + (anchorAvatar.Forward * RiftHazardSpawnDistance);
                Bounds spawnBounds = new(hazardProto.Bounds, spawnPosition);
                PathFlags pathFlags = Region.GetPathFlagsForEntity(hazardProto);
                bool foundPosition = region.ChooseRandomPositionNearPoint(
                    ref spawnBounds,
                    pathFlags,
                    PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                    BlockingCheckFlags.None,
                    RiftHazardSpawnDistance - RiftHazardSpawnSearchDistance,
                    RiftHazardSpawnDistance + RiftHazardSpawnSearchDistance,
                    out spawnPosition,
                    maxPositionTests: 48);

                if (foundPosition == false)
                {
                    spawnBounds.Center = anchorAvatar.RegionLocation.Position + (anchorAvatar.Forward * RiftHazardSpawnDistance);
                    foundPosition = region.ChoosePositionAtOrNearPoint(
                        ref spawnBounds,
                        pathFlags,
                        PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                        BlockingCheckFlags.None,
                        RiftHazardSpawnSearchDistance,
                        out spawnPosition,
                        maxPositionTests: 32);
                }

                if (foundPosition == false)
                    continue;

                Cell spawnCell = region.GetCellAtPosition(spawnPosition);
                if (spawnCell == null)
                    continue;

                spawnPosition = RegionLocation.ProjectToFloor(region, spawnPosition);
                spawnPosition.Z += hazardProto.Bounds.GetBoundHalfHeight();

                using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
                settings.EntityRef = hazardRef;
                settings.Position = spawnPosition;
                settings.Orientation = anchorAvatar.RegionLocation.Orientation;
                settings.RegionId = region.Id;
                settings.Cell = spawnCell;
                settings.IsPopulation = false;
                settings.HotspotSkipCollide = false;
                settings.Lifespan = RiftHazardDuration;

                using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
                int level = spawnCell.Area.GetCharacterLevel(hazardProto);
                settingsProperties[PropertyEnum.CharacterLevel] = level;
                settingsProperties[PropertyEnum.CombatLevel] = level;
                settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
                settingsProperties[PropertyEnum.MissionXEncounterHostilityOk] = true;
                settings.Properties = settingsProperties;

                WorldEntity hazard = Game.EntityManager.CreateEntity(settings) as WorldEntity;
                if (hazard == null)
                    continue;

                runState.RegisterHazardEntity(hazard.Id);
                Logger.Debug($"Mythic Rift run {runState.Config.RunId} spawned hazard {hazard.PrototypeName} at {spawnPosition}.");
                return true;
            }

            return false;
        }

        private static IReadOnlyList<PrototypeId> GetRiftHazardPrototypeRefs()
        {
            if (_cachedRiftHazardPrototypeRefs != null)
                return _cachedRiftHazardPrototypeRefs;

            List<PrototypeId> hazardRefs = new();
            foreach (PrototypeId hazardRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<HotspotPrototype>(PrototypeIterateFlags.NoAbstractApprovedOnly))
            {
                HotspotPrototype hazardProto = hazardRef.As<HotspotPrototype>();
                if (hazardProto == null ||
                    hazardProto.Bounds == null ||
                    (hazardProto.AppliesPowers.IsNullOrEmpty() && hazardProto.AppliesIntervalPowers.IsNullOrEmpty()))
                {
                    continue;
                }

                string prototypeName = GameDatabase.GetPrototypeName(hazardRef) ?? string.Empty;
                if (RiftHazardNameKeywords.Any(keyword => prototypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) == false)
                    continue;

                hazardRefs.Add(hazardRef);
            }

            _cachedRiftHazardPrototypeRefs = hazardRefs
                .OrderBy(hazardRef => GameDatabase.GetPrototypeName(hazardRef), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Logger.Info($"Mythic Rift discovered {_cachedRiftHazardPrototypeRefs.Count} hotspot-style environmental hazard prototype(s).");
            return _cachedRiftHazardPrototypeRefs;
        }

        private int CountLiveCustomRiftPopulationEntities(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return 0;

            int liveCount = 0;
            foreach (ulong entityId in runState.CustomPopulationEntityIds.ToArray())
            {
                WorldEntity entity = Game.EntityManager.GetEntity<WorldEntity>(entityId);
                if (entity == null || entity.IsDestroyed || entity.IsDead || entity.Region != region)
                {
                    runState.RemoveCustomPopulationEntity(entityId);
                    continue;
                }

                if (entity is Agent && entity.IsHostileToPlayers())
                    liveCount++;
            }

            return liveCount;
        }

        private bool TrySpawnCustomRiftPopulationMob(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return false;

            Player anchorPlayer = PickCustomRiftPopulationAnchorPlayer(runState, region);
            Avatar anchorAvatar = anchorPlayer?.CurrentAvatar;
            if (anchorAvatar == null || anchorAvatar.IsAliveInWorld == false || anchorAvatar.Region != region)
                return false;

            AgentPrototype mobProto = PickCustomRiftPopulationMobPrototype();
            if (mobProto == null || mobProto.Bounds == null)
                return false;

            Vector3 spawnPosition = anchorAvatar.RegionLocation.Position + (anchorAvatar.Forward * CustomRiftPopulationFallbackSpawnDistance);
            Bounds spawnBounds = new(mobProto.Bounds, spawnPosition);
            PathFlags pathFlags = Region.GetPathFlagsForEntity(mobProto);

            bool foundPosition = region.ChooseRandomPositionNearPoint(
                ref spawnBounds,
                pathFlags,
                PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                BlockingCheckFlags.CheckSpawns,
                CustomRiftPopulationSpawnMinDistance,
                CustomRiftPopulationSpawnMaxDistance,
                out spawnPosition,
                maxPositionTests: 96);

            if (foundPosition == false)
            {
                spawnBounds.Center = anchorAvatar.RegionLocation.Position + (anchorAvatar.Forward * CustomRiftPopulationFallbackSpawnDistance);
                foundPosition = region.ChoosePositionAtOrNearPoint(
                    ref spawnBounds,
                    pathFlags,
                    PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                    BlockingCheckFlags.None,
                    CustomRiftPopulationFallbackSpawnDistance,
                    out spawnPosition,
                    maxPositionTests: 48);
            }

            if (foundPosition == false)
                return false;

            Cell spawnCell = region.GetCellAtPosition(spawnPosition);
            if (spawnCell == null)
                return false;

            spawnPosition = RegionLocation.ProjectToFloor(region, spawnPosition);
            spawnPosition.Z += mobProto.Bounds.GetBoundHalfHeight();

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = mobProto.DataRef;
            settings.Position = spawnPosition;
            settings.Orientation = anchorAvatar.RegionLocation.Orientation;
            settings.RegionId = region.Id;
            settings.Cell = spawnCell;
            settings.IsPopulation = true;

            using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            int level = spawnCell.Area.GetCharacterLevel(mobProto);
            settingsProperties[PropertyEnum.CharacterLevel] = level;
            settingsProperties[PropertyEnum.CombatLevel] = level;
            settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
            settingsProperties[PropertyEnum.Rank] = mobProto.Rank?.DataRef ?? PrototypeId.Invalid;
            settingsProperties[PropertyEnum.MissionXEncounterHostilityOk] = true;
            ApplyRunAffixesToSpawnProperties(runState, mobProto.Rank?.DataRef ?? PrototypeId.Invalid, settingsProperties, includeBossScopedAffixes: false);
            settings.Properties = settingsProperties;

            Agent spawnedAgent = Game.EntityManager.CreateEntity(settings) as Agent;
            if (spawnedAgent == null)
                return false;

            if (mobProto.ModifiersGuaranteed != null && mobProto.ModifiersGuaranteed.Length > 0)
            {
                foreach (PrototypeId boost in mobProto.ModifiersGuaranteed)
                    spawnedAgent.Properties[PropertyEnum.EnemyBoost, boost] = true;
            }

            runState.RegisterCustomPopulationEntity(spawnedAgent.Id);
            return true;
        }

        private void ApplyRunAffixesToSpawnProperties(
            MythicRiftRunState runState,
            PrototypeId rankRef,
            PropertyCollection properties,
            bool includeBossScopedAffixes)
        {
            if (runState?.Config == null || properties == null)
                return;

            HashSet<PrototypeId> enemyBoosts = new();
            AddEnemyBoostsForAffixes(runState.Config.RegionAffixes, rankRef, enemyBoosts);
            if (includeBossScopedAffixes)
                AddEnemyBoostsForAffixes(runState.Config.BossAffixes, rankRef, enemyBoosts);

            foreach (PrototypeId enemyBoost in enemyBoosts)
                properties[PropertyEnum.EnemyBoost, enemyBoost] = true;
        }

        private static void AddEnemyBoostsForAffixes(IReadOnlyList<PrototypeId> affixes, PrototypeId rankRef, HashSet<PrototypeId> enemyBoosts)
        {
            if (affixes == null || affixes.Count == 0 || enemyBoosts == null)
                return;

            foreach (PrototypeId affix in affixes)
                AddEnemyBoostsForAffix(affix, rankRef, enemyBoosts);
        }

        private static void AddEnemyBoostsForAffix(PrototypeId affix, PrototypeId rankRef, HashSet<PrototypeId> enemyBoosts)
        {
            RegionAffixPrototype affixProto = affix.As<RegionAffixPrototype>();
            if (affixProto == null)
                return;

            if (affixProto.EnemyBoost != PrototypeId.Invalid)
                enemyBoosts.Add(affixProto.EnemyBoost);

            if (affixProto.EnemyBoostsFiltered == null)
                return;

            foreach (EnemyBoostEntryPrototype entry in affixProto.EnemyBoostsFiltered)
            {
                if (entry == null || entry.EnemyBoost == PrototypeId.Invalid)
                    continue;

                if (CanApplyFilteredEnemyBoost(entry, rankRef))
                    enemyBoosts.Add(entry.EnemyBoost);
            }
        }

        private static bool CanApplyFilteredEnemyBoost(EnemyBoostEntryPrototype entry, PrototypeId rankRef)
        {
            if (entry == null)
                return false;

            if (entry.RanksAllowed != null &&
                entry.RanksAllowed.Length > 0 &&
                entry.RanksAllowed.Contains(rankRef) == false)
            {
                return false;
            }

            if (entry.RanksPrevented != null &&
                entry.RanksPrevented.Contains(rankRef))
            {
                return false;
            }

            return true;
        }

        private Player PickCustomRiftPopulationAnchorPlayer(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return null;

            List<Player> players = new();
            foreach (Player player in new PlayerIterator(region))
            {
                if (player?.DatabaseUniqueId == 0 ||
                    runState.IsParticipant(player.DatabaseUniqueId) == false ||
                    runState.HasParticipantLeftEarly(player.DatabaseUniqueId))
                {
                    continue;
                }

                if (player.CurrentAvatar?.IsAliveInWorld == true)
                    players.Add(player);
            }

            if (players.Count == 0)
                return null;

            return players[Game.Random.Next(0, players.Count)];
        }

        private static Player PickBossSpawnAnchorPlayer(MythicRiftRunState runState, Region region)
        {
            if (runState == null || region == null)
                return null;

            List<Player> players = new();
            foreach (Player player in new PlayerIterator(region))
            {
                if (IsValidBossSpawnAnchorPlayer(player, runState, region))
                    players.Add(player);
            }

            if (players.Count <= 1)
                return players.FirstOrDefault();

            Player bestPlayer = null;
            float bestTotalDistanceSquared = float.MaxValue;
            foreach (Player candidate in players)
            {
                Vector3 candidatePosition = candidate.CurrentAvatar.RegionLocation.Position;
                float totalDistanceSquared = 0f;
                foreach (Player other in players)
                    totalDistanceSquared += Vector3.DistanceSquared2D(candidatePosition, other.CurrentAvatar.RegionLocation.Position);

                if (totalDistanceSquared < bestTotalDistanceSquared)
                {
                    bestPlayer = candidate;
                    bestTotalDistanceSquared = totalDistanceSquared;
                }
            }

            return bestPlayer;
        }

        private static bool IsValidBossSpawnAnchorPlayer(Player player, MythicRiftRunState runState, Region region)
        {
            if (player == null || player.DatabaseUniqueId == 0 || runState == null || region == null)
                return false;

            if (runState.HasParticipantLeftEarly(player.DatabaseUniqueId))
                return false;

            Avatar avatar = player.CurrentAvatar;
            return runState.IsParticipant(player.DatabaseUniqueId) &&
                   avatar?.IsAliveInWorld == true &&
                   avatar.Region == region;
        }

        private AgentPrototype PickCustomRiftPopulationMobPrototype()
        {
            PrototypeId[] mobRefs = GetCustomRiftPopulationMobPrototypeRefs();
            if (mobRefs.Length == 0)
                return null;

            PrototypeId mobRef = mobRefs[Game.Random.Next(0, mobRefs.Length)];
            return mobRef.As<AgentPrototype>();
        }

        private static PrototypeId[] GetCustomRiftPopulationMobPrototypeRefs()
        {
            if (_cachedCustomRiftPopulationMobPrototypeRefs != null)
                return _cachedCustomRiftPopulationMobPrototypeRefs;

            List<PrototypeId> resolvedRefs = new();
            foreach (string prototypeName in CustomRiftPopulationMobPrototypeNames)
            {
                PrototypeId prototypeRef = ResolvePrototype(prototypeName);
                if (prototypeRef != PrototypeId.Invalid && prototypeRef.As<AgentPrototype>() != null)
                    resolvedRefs.Add(prototypeRef);
            }

            _cachedCustomRiftPopulationMobPrototypeRefs = resolvedRefs.ToArray();
            return _cachedCustomRiftPopulationMobPrototypeRefs;
        }

        private static int GetCustomRiftPopulationTargetAlive(MythicRiftRunState runState)
        {
            int extraPlayers = Math.Max((runState?.EffectivePlayerCount ?? 1) - 1, 0);
            return CustomRiftPopulationBaseTargetAlive + (extraPlayers * CustomRiftPopulationTargetAlivePerExtraPlayer);
        }

        private static int GetCustomRiftPopulationMaxAlive(MythicRiftRunState runState)
        {
            int extraPlayers = Math.Max((runState?.EffectivePlayerCount ?? 1) - 1, 0);
            return CustomRiftPopulationBaseMaxAlive + (extraPlayers * CustomRiftPopulationMaxAlivePerExtraPlayer);
        }

        private static int GetCustomRiftPopulationSpawnBatch(MythicRiftRunState runState)
        {
            int extraPlayers = Math.Max((runState?.EffectivePlayerCount ?? 1) - 1, 0);
            return CustomRiftPopulationBaseSpawnBatch + (extraPlayers * CustomRiftPopulationSpawnBatchPerExtraPlayer);
        }

        private bool TrySpawnConfiguredBoss(MythicRiftRunState runState)
        {
            if (runState == null ||
                runState.RegionId == 0 ||
                runState.BossSpawnCount >= runState.Config.RequiredBossKillCount)
                return false;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return false;

            int requiredBossCount = Math.Max(runState.Config.RequiredBossKillCount, 1);
            int spawnTargetCount = runState.Config.UseBossGauntletMode
                ? Math.Min(runState.BossSpawnCount + 1, requiredBossCount)
                : requiredBossCount;
            int spawnedThisCall = 0;
            while (runState.BossSpawnCount < spawnTargetCount)
            {
                int spawnIndex = runState.BossSpawnCount;
                MythicRiftContentEntry bossContent = runState.Config.BossWaveContent.ElementAtOrDefault(spawnIndex);
                AgentPrototype bossProto = bossContent?.BossProtoRef.As<AgentPrototype>();
                if (bossProto == null)
                    break;

                if (TryResolveBossSpawnLocation(runState, region, bossProto, spawnIndex, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell) == false)
                    break;

                using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
                settings.EntityRef = bossProto.DataRef;
                settings.Position = spawnPosition;
                settings.Orientation = spawnOrientation;
                settings.RegionId = region.Id;
                settings.Cell = spawnCell;
                settings.IsPopulation = true;

                using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
                int level = spawnCell.Area.GetCharacterLevel(bossProto);
                settingsProperties[PropertyEnum.CharacterLevel] = level;
                settingsProperties[PropertyEnum.CombatLevel] = level;
                settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
                settingsProperties[PropertyEnum.Rank] = bossProto.Rank?.DataRef ?? PrototypeId.Invalid;
                settingsProperties[PropertyEnum.NoLootDrop] = true;
                ApplyRunAffixesToSpawnProperties(runState, bossProto.Rank?.DataRef ?? PrototypeId.Invalid, settingsProperties, includeBossScopedAffixes: true);
                settings.Properties = settingsProperties;

                Agent bossAgent = Game.EntityManager.CreateEntity(settings) as Agent;
                if (bossAgent == null)
                    break;

                EntityHelper.ApplyStandaloneBossFixups(bossAgent, allowMissingAffixSettingsFallback: true);
                ApplyCheckpointBossTuning(runState, bossAgent);
                runState.AttachBoss(bossAgent.Id);
                spawnedThisCall++;
                Logger.Info($"Mythic Rift run {runState.Config.RunId} spawned boss {runState.BossSpawnCount}/{requiredBossCount}: {bossAgent.PrototypeName} from boss pool entry {bossContent.Id}.");
            }

            return spawnedThisCall > 0 &&
                   (runState.Config.UseBossGauntletMode || runState.BossSpawnCount >= requiredBossCount);
        }

        private void TrySpawnPendingMilestoneEncounters(MythicRiftRunState runState, bool allowAfterBossUnlock = false)
        {
            if (runState == null ||
                runState.Status != MythicRiftRunStatus.Active ||
                runState.Config.UseBossGauntletMode ||
                runState.Config.Content.BossOnlyCheckpointEligible ||
                (runState.BossUnlocked && allowAfterBossUnlock == false))
            {
                return;
            }

            int requiredCount = Math.Max(runState.Config.KillQuota, 1);
            int progressPercent = (int)Math.Floor((double)runState.CurrentKillCount * 100d / requiredCount);
            foreach (int milestonePercent in KillProgressMilestonePercents)
            {
                if (progressPercent < milestonePercent || runState.HasSpawnedMilestoneEncounter(milestonePercent))
                    continue;

                if (TrySpawnMilestoneEncounter(runState, milestonePercent) == false)
                    continue;

                runState.MarkMilestoneEncounterSpawned(milestonePercent);
            }
        }

        private bool TrySpawnMilestoneEncounter(MythicRiftRunState runState, int milestonePercent)
        {
            Region region = runState != null && runState.RegionId > 0
                ? Game.RegionManager.GetRegion(runState.RegionId)
                : null;
            if (region == null)
                return false;

            int spawnedCount;
            string encounterName;
            switch (milestonePercent)
            {
                case 25:
                    spawnedCount = TrySpawnRankedMilestoneSquad(runState, region, Rank.Champion, 3);
                    encounterName = "Champion invasion";
                    break;

                case 50:
                    spawnedCount = TrySpawnMilestoneMiniBoss(runState, region) ? 1 : 0;
                    encounterName = "Rift mini-boss";
                    break;

                case 75:
                    spawnedCount = TrySpawnRankedMilestoneSquad(runState, region, Rank.Elite, 3);
                    encounterName = "Elite strike team";
                    break;

                default:
                    return false;
            }

            if (spawnedCount <= 0)
                return false;

            NotifyRunPlayers(runState, $"[Mythic Rift] {milestonePercent}% milestone: {encounterName} incoming.");
            Logger.Info($"Mythic Rift run {runState.Config.RunId} spawned milestone encounter {milestonePercent}% ({encounterName}), entities={spawnedCount}.");
            return true;
        }

        private int TrySpawnRankedMilestoneSquad(MythicRiftRunState runState, Region region, Rank rank, int count)
        {
            int spawnedCount = 0;
            for (int i = 0; i < Math.Max(count, 1); i++)
            {
                AgentPrototype agentProto = PickCustomRiftPopulationMobPrototype();
                if (agentProto != null && TrySpawnMilestoneAgent(runState, region, agentProto, rank, i, out _))
                    spawnedCount++;
            }

            return spawnedCount;
        }

        private bool TrySpawnMilestoneMiniBoss(MythicRiftRunState runState, Region region)
        {
            List<MythicRiftContentEntry> candidates = _contentPool
                .Where(entry => entry.RandomBossEligible && entry.HasValidBossSource)
                .Where(entry => runState.Config.BossWaveContent.Any(
                    waveBoss => string.Equals(waveBoss.Id, entry.Id, StringComparison.OrdinalIgnoreCase)) == false)
                .ToList();

            MythicRiftContentEntry selected = candidates.Count > 0
                ? candidates[Game.Random.Next(0, candidates.Count)]
                : runState.Config.BossWaveContent.FirstOrDefault();
            AgentPrototype agentProto = selected?.BossProtoRef.As<AgentPrototype>();
            if (agentProto == null || TrySpawnMilestoneAgent(runState, region, agentProto, Rank.MiniBoss, 0, out Agent miniBoss) == false)
                return false;

            runState.RegisterMilestoneMiniBossEntity(miniBoss.Id);
            return true;
        }

        private bool TrySpawnMilestoneAgent(
            MythicRiftRunState runState,
            Region region,
            AgentPrototype agentProto,
            Rank rank,
            int spawnIndex,
            out Agent spawnedAgent)
        {
            spawnedAgent = null;
            if (runState == null || region == null || agentProto == null)
                return false;

            if (TryResolvePlayerAnchoredBossSpawnLocation(
                    runState,
                    region,
                    agentProto,
                    spawnIndex,
                    out Vector3 spawnPosition,
                    out Orientation spawnOrientation,
                    out Cell spawnCell) == false)
            {
                return false;
            }

            RankPrototype rankProto = GameDatabase.PopulationGlobalsPrototype?.GetRankByEnum(rank);

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = agentProto.DataRef;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.RegionId = region.Id;
            settings.Cell = spawnCell;
            settings.IsPopulation = true;

            using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            int level = spawnCell.Area.GetCharacterLevel(agentProto);
            PrototypeId rankRef = rankProto?.DataRef ?? agentProto.Rank?.DataRef ?? PrototypeId.Invalid;
            settingsProperties[PropertyEnum.CharacterLevel] = level;
            settingsProperties[PropertyEnum.CombatLevel] = level;
            settingsProperties[PropertyEnum.DifficultyTier] = region.DifficultyTierRef;
            settingsProperties[PropertyEnum.Rank] = rankRef;
            settingsProperties[PropertyEnum.NoLootDrop] = true;
            settingsProperties[PropertyEnum.MissionXEncounterHostilityOk] = true;
            ApplyRunAffixesToSpawnProperties(runState, rankRef, settingsProperties, includeBossScopedAffixes: true);
            settings.Properties = settingsProperties;

            spawnedAgent = Game.EntityManager.CreateEntity(settings) as Agent;
            if (spawnedAgent == null)
                return false;

            EntityHelper.ApplyStandaloneBossFixups(spawnedAgent, allowMissingAffixSettingsFallback: true);
            if (agentProto.ModifiersGuaranteed != null)
            {
                foreach (PrototypeId boost in agentProto.ModifiersGuaranteed)
                    spawnedAgent.Properties[PropertyEnum.EnemyBoost, boost] = true;
            }

            runState.RegisterCustomPopulationEntity(spawnedAgent.Id);
            return true;
        }

        private bool TryResolveBossSpawnLocation(MythicRiftRunState runState, Region region, AgentPrototype bossProto, int spawnIndex, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell)
        {
            return TryResolvePlayerAnchoredBossSpawnLocation(runState, region, bossProto, spawnIndex, out spawnPosition, out spawnOrientation, out spawnCell);
        }

        private bool TryResolvePlayerAnchoredBossSpawnLocation(MythicRiftRunState runState, Region region, AgentPrototype bossProto, int spawnIndex, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell)
        {
            spawnPosition = Vector3.Zero;
            spawnOrientation = Orientation.Zero;
            spawnCell = null;

            Player anchorPlayer = PickBossSpawnAnchorPlayer(runState, region);
            Avatar anchorAvatar = anchorPlayer?.CurrentAvatar;
            if (anchorAvatar == null || anchorAvatar.IsAliveInWorld == false || anchorAvatar.Region != region)
                return false;

            Vector3 anchorPosition = anchorAvatar.RegionLocation.Position;
            Cell anchorCell = anchorAvatar.Cell ?? region.GetCellAtPosition(anchorPosition);
            if (anchorCell == null)
                return false;

            spawnOrientation = anchorAvatar.RegionLocation.Orientation;
            if (bossProto.Bounds == null)
            {
                spawnPosition = anchorPosition;
                spawnCell = anchorCell;
                return true;
            }

            PathFlags pathFlags = Region.GetPathFlagsForEntity(bossProto);
            Vector3 forward = Vector3.SafeNormalize2D(anchorAvatar.Forward, Vector3.XAxis);
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

            for (int attempt = 0; attempt < directions.Length; attempt++)
            {
                Vector3 direction = directions[(spawnIndex + attempt) % directions.Length];
                float spawnDistance = runState?.Config?.UseBossGauntletMode == true
                    ? BossGauntletBossSpawnDistance
                    : CheckpointBossSpawnDistance;
                float searchDistance = runState?.Config?.UseBossGauntletMode == true
                    ? BossGauntletBossSpawnSearchDistance
                    : CheckpointBossSpawnSearchDistance;
                Vector3 preferredPosition = anchorPosition + (direction * spawnDistance);
                if (TryChooseCheckpointBossSpawnPosition(region, bossProto, preferredPosition, anchorCell, pathFlags, searchDistance, out spawnPosition, out spawnCell))
                    return true;
            }

            if (TryChooseCheckpointBossSpawnPosition(region, bossProto, anchorPosition, anchorCell, pathFlags, CheckpointBossSpawnSearchDistance, out spawnPosition, out spawnCell))
                return true;

            spawnPosition = anchorPosition;
            spawnCell = anchorCell;
            return true;
        }

        private static bool TryChooseCheckpointBossSpawnPosition(Region region, AgentPrototype bossProto, Vector3 preferredPosition, Cell preferredCell, PathFlags pathFlags, float searchDistance, out Vector3 spawnPosition, out Cell spawnCell)
        {
            spawnPosition = Vector3.Zero;
            spawnCell = null;

            if (region == null || bossProto?.Bounds == null || preferredCell == null)
                return false;

            Bounds spawnBounds = new(bossProto.Bounds, preferredPosition);
            if (region.ChoosePositionAtOrNearPoint(
                ref spawnBounds,
                pathFlags,
                PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                BlockingCheckFlags.None,
                searchDistance,
                out Vector3 resolvedPosition,
                maxPositionTests: 64) == false)
            {
                return false;
            }

            Cell resolvedCell = region.GetCellAtPosition(resolvedPosition);
            if (resolvedCell == null || resolvedCell != preferredCell)
                return false;

            spawnPosition = resolvedPosition;
            spawnCell = resolvedCell;
            return true;
        }

        private static void ApplyCheckpointBossTuning(MythicRiftRunState runState, Agent bossAgent)
        {
            if (runState?.Config?.Content?.BossOnlyCheckpointEligible != true || bossAgent == null)
                return;

            if (runState.Config.UseThirtyWaveMode)
                return;

            float healthMultiplier = GetCheckpointBossHealthMultiplier(runState.Config.RiftLevel);
            if (healthMultiplier <= 1f)
                return;

            float existingHealthBonus = bossAgent.Properties[PropertyEnum.HealthPctBonus];
            bossAgent.Properties[PropertyEnum.HealthPctBonus] = existingHealthBonus + healthMultiplier - 1f;
            bossAgent.Properties[PropertyEnum.Health] = bossAgent.Properties[PropertyEnum.HealthMax];
        }

        private static float GetCheckpointBossHealthMultiplier(int riftLevel)
        {
            int checkpointTier = Math.Max(riftLevel / CheckpointBossHealthTierInterval, 1);
            float multiplier = CheckpointBossBaseHealthMultiplier + ((checkpointTier - 1) * CheckpointBossHealthMultiplierPerTier);
            return Math.Clamp(multiplier, CheckpointBossBaseHealthMultiplier, CheckpointBossMaxHealthMultiplier);
        }

        private void TryAutoGrantCompletionRewards(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            if (runState.Status is not (MythicRiftRunStatus.Success or MythicRiftRunStatus.Failed))
                return;

            if (runState.RewardsGranted)
                return;

            int grantedCount = GrantRewardsToRunPlayers(runState.Config.RunId);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} auto-granted completion rewards to {grantedCount} player(s).");
        }

        private void GrantCompletionCrafterAttempts(MythicRiftRunState runState)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Success)
                return;

            foreach (ulong playerDbId in runState.RewardEligiblePlayerDbIds)
            {
                if (playerDbId == 0)
                    continue;

                _completionCrafterOpportunitiesByPlayer[playerDbId] = new()
                {
                    RunId = runState.Config.RunId,
                    AttemptsRemaining = RiftCompletionCrafterAttemptsPerRun
                };

                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                if (player != null)
                {
                    MythicRiftRewardTuning tuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
                    Game.ChatManager?.SendChatFromCustomSystem(
                        player,
                        $"[Mythic Rift] Completion crafter unlocked: {RiftCompletionCrafterAttemptsPerRun} attempts, {RiftCompletionCrafterUpgradeChance:P0} chance to upgrade item level {RiftCompletionCrafterMinimumItemLevel}-{RiftCompletionCrafterMaximumItemLevel - 1} Unique or {RiftCompletionCrafterCosmicMinimumItemLevel}-{RiftCompletionCrafterMaximumItemLevel - 1} Cosmic gear slot 1-5 by +1. Costs {tuning.CompletionCrafterUniqueRecipeCost} (Unique) or {tuning.CompletionCrafterCosmicRecipeCost} (Cosmic) Champion's Commendations on a successful upgrade. A success ends this run's attempts.",
                        showSender: false);
                }
            }
        }

        private bool CompleteRunSuccess(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null)
                return false;

            CaptureSuccessfulCompletionEligibility(runState);
            runState.SnapshotRewardEligiblePlayers(GetCurrentRunRegionPlayerDbIds(runState));
            runState.MarkSuccess(currentTime);
            if (runState.Status != MythicRiftRunStatus.Success)
                return false;

            ResolveRewardOutcome(runState);
            GrantProgressionForSuccessfulRun(runState);
            TrackLastCompletedMapContent(runState);
            TryAutoGrantCompletionRewards(runState);
            GrantCompletionCrafterAttempts(runState);
            CleanupRunHazards(runState);
            TryRestoreRegionDifficultyScaling(runState);
            ClearRiftObjectiveWidgets(runState);
            SendStopRiftTimer(runState);
            int eligibleUnlockCount = runState.ProgressionEligiblePlayerDbIds.Count;
            string successMessage = eligibleUnlockCount > 0
                ? $"Rift cleared. Next level unlocked for {eligibleUnlockCount} eligible player(s). Bonus loot granted."
                : "Rift cleared. Loot granted, but no players met the next-level unlock rule.";
            TrySendRiftClearedBanner(runState);
            NotifyRunCompleted(runState, success: true, successMessage);
            TrySpawnReturnPortal(runState);
            TrySpawnCompletionCrafter(runState);
            return true;
        }

        private bool CompleteRunFailure(MythicRiftRunState runState, TimeSpan currentTime, string reason, bool returnParticipantsToHub = false)
        {
            if (runState == null)
                return false;

            runState.SnapshotRewardEligiblePlayers(GetCurrentRunRegionPlayerDbIds(runState));
            runState.MarkFailed(currentTime);
            if (runState.Status != MythicRiftRunStatus.Failed)
                return false;

            if (runState.Config.UseBossGauntletMode)
                ClearBossGauntletActiveEnemies(runState);

            ResolveRewardOutcome(runState);
            TrackLastCompletedMapContent(runState);
            TryAutoGrantCompletionRewards(runState);
            CleanupRunHazards(runState);
            TryRestoreRegionDifficultyScaling(runState);
            ClearRiftObjectiveWidgets(runState);
            SendStopRiftTimer(runState);
            NotifyRunCompleted(runState, success: false, reason);

            if (runState.Config.UseBossGauntletMode)
            {
                TrySpawnReturnPortal(runState);
                QueueBossGauntletFailureRecovery(runState, currentTime);
            }
            else if (returnParticipantsToHub)
                QueueFailedRunEvacuation(runState, currentTime);
            else
                RequestRunRegionShutdownWhenVacant(runState);

            Logger.Info($"Mythic Rift run {runState.Config.RunId} failed. reason={reason}");
            return true;
        }

        private void ClearBossGauntletActiveEnemies(MythicRiftRunState runState)
        {
            if (runState?.Config?.UseBossGauntletMode != true)
                return;

            ClearBossGauntletEnemies(runState, includeTrackedBosses: true, reason: "run-end");
        }

        private int ClearBossGauntletResidualEnemies(MythicRiftRunState runState, string reason)
        {
            if (runState?.Config?.UseBossGauntletMode != true)
                return 0;

            return ClearBossGauntletEnemies(runState, includeTrackedBosses: false, reason);
        }

        private int ClearBossGauntletEnemies(MythicRiftRunState runState, bool includeTrackedBosses, string reason)
        {
            Region region = runState.RegionId != 0
                ? Game.RegionManager.GetRegion(runState.RegionId)
                : null;
            int destroyedCount = 0;
            if (region != null)
            {
                foreach (Entity entity in region.Entities.ToArray())
                {
                    if (entity is not Agent agent || agent.IsDestroyed || agent.IsDead || agent.IsHostileToPlayers() == false)
                        continue;

                    if (includeTrackedBosses == false && runState.IsTrackedBoss(agent.Id))
                        continue;

                    DestroyDefeatedRiftEntity(agent);
                    destroyedCount++;
                }

                if (destroyedCount > 0)
                    Logger.Info($"Mythic Rift run {runState.Config.RunId} cleared {destroyedCount} Boss Gauntlet hostile residual entity/entities. reason={reason} wave={runState.Config.WaveNumber}");

                return destroyedCount;
            }

            foreach (ulong bossEntityId in runState.ActiveBossEntityIds.ToArray())
            {
                if (includeTrackedBosses == false)
                    continue;

                WorldEntity boss = Game.EntityManager.GetEntity<WorldEntity>(bossEntityId);
                if (boss == null || boss.IsDestroyed)
                    continue;

                DestroyDefeatedRiftEntity(boss);
                destroyedCount++;
            }

            return destroyedCount;
        }

        private void ReviveRunParticipantsInPlace(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            Region region = runState.RegionId != 0
                ? Game.RegionManager.GetRegion(runState.RegionId)
                : null;
            Vector3 revivePosition = Vector3.Zero;
            bool hasRevivePosition = TryResolveRunStartPosition(runState, region, out revivePosition);

            foreach (ulong playerDbId in runState.ParticipantPlayerDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                Avatar avatar = player?.CurrentAvatar;
                if (avatar == null)
                    continue;

                bool resurrected = avatar.IsDead == false || avatar.Resurrect();
                bool teleported = false;
                if (region != null && hasRevivePosition && avatar.Region?.Id == runState.RegionId)
                {
                    using Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>();
                    teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Resurrect);
                    teleporter.DifficultyTierRef = region.DifficultyTierRef;
                    teleported = teleporter.TeleportToRegionLocation(region.Id, revivePosition);
                }

                Logger.Info(
                    $"Mythic Rift run {runState.Config.RunId} revived Boss Gauntlet participant playerDbId=0x{playerDbId:X} " +
                    $"after failure. resurrectResult={resurrected} teleportResult={teleported}");
            }
        }

        private static bool TryResolveRunStartPosition(MythicRiftRunState runState, Region region, out Vector3 position)
        {
            position = Vector3.Zero;
            if (runState?.Config == null || region == null)
                return false;

            RegionConnectionTargetPrototype startTargetProto = runState.Config.StartTargetProtoRef.As<RegionConnectionTargetPrototype>();
            if (startTargetProto == null)
                return false;

            Orientation orientation = Orientation.Zero;
            PrototypeId cellRef = GameDatabase.GetDataRefByAsset(startTargetProto.Cell);
            if (region.FindTargetLocation(ref position, ref orientation, startTargetProto.Area, cellRef, startTargetProto.Entity) == false)
                return false;

            position = RegionLocation.ProjectToFloor(region, position);
            return true;
        }

        private bool AbortRun(MythicRiftRunState runState, TimeSpan currentTime, string reason)
        {
            if (runState == null)
                return false;

            runState.MarkAborted(currentTime);
            if (runState.Status != MythicRiftRunStatus.Aborted)
                return false;

            TrackLastCompletedMapContent(runState);
            CleanupRunHazards(runState);
            TryRestoreRegionDifficultyScaling(runState);
            ClearRiftObjectiveWidgets(runState);
            SendStopRiftTimer(runState);
            RequestRunRegionShutdownWhenVacant(runState);
            NotifyRunCompleted(runState, success: false, reason);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} aborted. reason={reason}");
            return true;
        }

        private void TryAutoBindAndStartPendingRun(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Pending || runState.RegionId != 0)
                return;

            foreach (ulong participantPlayerDbId in runState.ParticipantPlayerDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(participantPlayerDbId);
                Region region = player?.GetRegion();
                if (region == null)
                    continue;

                if (IsMatchingRunRegion(region, runState) == false)
                    continue;

                if (AttachRunToRegion(runState.Config.RunId, region) == false)
                    return;

                StartRun(runState.Config.RunId, currentTime);
                Logger.Info($"Mythic Rift run {runState.Config.RunId} auto-bound to region {region.PrototypeName} (0x{region.Id:X}) and started.");
                return;
            }
        }

        private static bool IsMatchingRunRegion(Region region, MythicRiftRunState runState)
        {
            if (region == null || runState?.Config == null)
                return false;

            if (region.PrototypeDataRef == runState.Config.RegionProtoRef)
                return true;

            RegionPrototype currentRegionProto = region.Prototype;
            RegionPrototype expectedRegionProto = runState.Config.RegionProtoRef.As<RegionPrototype>();
            if (AreRegionsEquivalent(expectedRegionProto, currentRegionProto))
                return true;

            RegionConnectionTargetPrototype startTargetProto = runState.Config.StartTargetProtoRef.As<RegionConnectionTargetPrototype>();
            RegionPrototype startTargetRegionProto = startTargetProto?.Region.As<RegionPrototype>();
            if (AreRegionsEquivalent(startTargetRegionProto, currentRegionProto))
                return true;

            return false;
        }

        private static bool AreRegionsEquivalent(RegionPrototype regionA, RegionPrototype regionB)
        {
            return RegionPrototype.Equivalent(regionA, regionB) || RegionPrototype.Equivalent(regionB, regionA);
        }

        private static bool IsPlayerInRunRegion(Player player, MythicRiftRunState runState)
        {
            Region region = player?.GetRegion();
            if (region == null || runState == null)
                return false;

            if (runState.RegionId != 0 && region.Id == runState.RegionId)
                return true;

            return IsMatchingRunRegion(region, runState);
        }

        private void UpdateParticipantPresence(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.ParticipantCount == 0)
                return;

            foreach (ulong participantPlayerDbId in runState.ParticipantPlayerDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(participantPlayerDbId);
                if (player == null)
                    continue;

                runState.TouchParticipantPresence(currentTime);
                return;
            }
        }

        private void RegisterBoundRegionPlayersAsParticipants(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return;

            RegisterRegionPlayersAsParticipants(runState, region);
        }

        private bool TryHandleParticipantExit(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.RegionId == 0)
                return false;

            List<ulong> exitedPlayerDbIds = null;
            foreach (ulong participantPlayerDbId in runState.ParticipantPlayerDbIds.ToList())
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(participantPlayerDbId);
                if (player == null || player.GetRegion() == null)
                    continue;

                if (runState.HasParticipantBeenSeenInRunRegion(participantPlayerDbId) == false)
                    continue;

                if (IsPlayerInRunRegion(player, runState))
                    continue;

                if (runState.MarkParticipantLeftEarly(participantPlayerDbId))
                {
                    exitedPlayerDbIds ??= new();
                    exitedPlayerDbIds.Add(participantPlayerDbId);
                }
            }

            if (exitedPlayerDbIds != null)
            {
                foreach (ulong exitedPlayerDbId in exitedPlayerDbIds)
                {
                    Player exitedPlayer = Game.EntityManager.GetEntityByDbGuid<Player>(exitedPlayerDbId);
                    string playerName = exitedPlayer?.GetName() ?? $"0x{exitedPlayerDbId:X}";
                    NotifyRunPlayers(runState, $"[Mythic Rift] {playerName} left the Rift and is no longer eligible for rewards or level unlocks.");
                    Logger.Info($"Mythic Rift run {runState.Config.RunId} marked participant playerDbId=0x{exitedPlayerDbId:X} as left early.");
                }
            }

            if (runState.ParticipantCount > 0 || HasAnyPlayerInRunRegion(runState))
                return false;

            string reason = "All participants left the Rift before completion. The Rift has closed and a new Beacon is required.";
            if (AbortRun(runState, currentTime, reason) == false)
                return false;

            Logger.Info($"Mythic Rift run {runState.Config.RunId} aborted after all participants left the Rift region.");
            return true;
        }

        private bool HasAnyPlayerInRunRegion(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return false;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return false;

            foreach (Player _ in new PlayerIterator(region))
                return true;

            return false;
        }

        private void RequestRunRegionShutdownWhenVacant(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null || region.ShutdownRequested)
                return;

            region.RequestShutdown();
            Logger.Info($"Mythic Rift run {runState.Config.RunId} requested shutdown for region {region.PrototypeName} (0x{region.Id:X}).");
        }

        private bool TrySpawnReturnPortal(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return false;

            if (runState.ExitPortalEntityId != 0 && Game.EntityManager.GetEntity<Transition>(runState.ExitPortalEntityId) != null)
                return true;

            if (TryResolveDangerRoomHubStartTarget(out PrototypeId dangerRoomHubStartTarget) == false)
                return false;

            PrototypeId exitPortalProtoRef = GameDatabase.GetPrototypeRefByName(RiftExitPortalPrototypeName);
            if (exitPortalProtoRef == PrototypeId.Invalid)
                return Logger.WarnReturn(false, $"TrySpawnReturnPortal(): Failed to resolve {RiftExitPortalPrototypeName}");

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return false;

            if (TryGetReturnPortalSpawnLocation(runState, region, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell) == false)
                return false;

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = exitPortalProtoRef;
            settings.RegionId = region.Id;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.Cell = spawnCell;
            settings.Lifespan = CompletedRunRetention;
            settings.SourceEntityId = GetFirstRunAvatarId(region);

            using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            settingsProperties[PropertyEnum.Interactable] = (int)TriBool.True;
            settingsProperties[PropertyEnum.InteractableUsesLeft] = -1;
            settingsProperties[PropertyEnum.Visible] = true;
            settings.Properties = settingsProperties;

            Transition exitPortal = Game.EntityManager.CreateEntity(settings) as Transition;
            if (exitPortal == null)
                return Logger.WarnReturn(false, "TrySpawnReturnPortal(): Failed to create return portal entity.");

            if (exitPortal.ConfigureDirectTarget(dangerRoomHubStartTarget) == false)
            {
                exitPortal.Destroy();
                return false;
            }

            runState.AttachExitPortal(exitPortal.Id);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} spawned return portal {exitPortal.PrototypeName} (0x{exitPortal.Id:X}) to Danger Room hub.");
            return true;
        }

        private bool TrySpawnCompletionCrafter(MythicRiftRunState runState)
        {
            if (runState == null || runState.RegionId == 0)
                return false;

            if (runState.CompletionCrafterEntityId != 0 && Game.EntityManager.GetEntity<WorldEntity>(runState.CompletionCrafterEntityId) != null)
                return true;

            PrototypeId vendorProtoRef = ResolvePrototype(RiftCompletionVendorPrototypeName);
            WorldEntityPrototype vendorProto = vendorProtoRef.As<WorldEntityPrototype>();
            if (vendorProtoRef == PrototypeId.Invalid || vendorProto == null)
                return Logger.WarnReturn(false, $"TrySpawnCompletionCrafter(): Failed to resolve {RiftCompletionVendorPrototypeName}");

            PrototypeId vendorTypeProtoRef = ResolvePrototype(RiftCompletionCrafterTypePrototypeName);
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProtoRef == PrototypeId.Invalid || vendorTypeProto == null || vendorTypeProto.IsCrafter == false)
                return Logger.WarnReturn(false, $"TrySpawnCompletionCrafter(): Failed to resolve crafter type {RiftCompletionCrafterTypePrototypeName}");

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return false;

            if (TryGetCompletionCrafterSpawnLocation(runState, region, vendorProto, out Vector3 spawnPosition, out Orientation spawnOrientation, out Cell spawnCell) == false)
                return false;

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = vendorProtoRef;
            settings.RegionId = region.Id;
            settings.Position = spawnPosition;
            settings.Orientation = spawnOrientation;
            settings.Cell = spawnCell;
            settings.Lifespan = CompletedRunRetention;
            settings.SourceEntityId = GetFirstRunAvatarId(region);

            using PropertyCollection settingsProperties = ObjectPoolManager.Instance.Get<PropertyCollection>();
            settingsProperties[PropertyEnum.Interactable] = (int)TriBool.True;
            settingsProperties[PropertyEnum.InteractableUsesLeft] = -1;
            settingsProperties[PropertyEnum.Visible] = true;
            settingsProperties[PropertyEnum.VendorType] = vendorTypeProtoRef;
            settings.Properties = settingsProperties;

            WorldEntity crafter = Game.EntityManager.CreateEntity(settings) as WorldEntity;
            if (crafter == null)
                return Logger.WarnReturn(false, "TrySpawnCompletionCrafter(): Failed to create completion crafter entity.");

            crafter.Properties[PropertyEnum.VendorType] = vendorTypeProtoRef;
            runState.AttachCompletionCrafter(crafter.Id);
            MythicRiftRewardTuning spawnTuning = _rewardTuning ?? MythicRiftRewardTuning.CreateDefault();
            NotifyRunPlayers(runState, $"[Mythic Rift] Completion crafter spawned. Use it for item level 69-74 unique or 63-74 cosmic upgrades; each eligible player has three attempts and one success per cleared Rift. Costs {spawnTuning.CompletionCrafterUniqueRecipeCost} (Unique) or {spawnTuning.CompletionCrafterCosmicRecipeCost} (Cosmic) Champion's Commendations on a successful upgrade.");
            Logger.Info($"Mythic Rift run {runState.Config.RunId} spawned completion crafter {crafter.PrototypeName} (0x{crafter.Id:X}) vendorType={vendorTypeProtoRef.GetNameFormatted()}.");
            return true;
        }

        private bool TryGetCompletionCrafterSpawnLocation(
            MythicRiftRunState runState,
            Region region,
            WorldEntityPrototype vendorProto,
            out Vector3 position,
            out Orientation orientation,
            out Cell cell)
        {
            position = Vector3.Zero;
            orientation = Orientation.Zero;
            cell = null;

            if (region == null || vendorProto == null)
                return false;

            if (TryGetReturnPortalSpawnLocation(runState, region, out Vector3 anchorPosition, out orientation, out Cell anchorCell) == false)
                return false;

            Vector3 right = Vector3.Perp2D(Vector3.SafeNormalize2D(Vector3.XAxis, Vector3.XAxis));
            foreach (Player player in new PlayerIterator(region))
            {
                Avatar avatar = player?.CurrentAvatar;
                if (avatar?.IsInWorld == true && avatar.Region == region)
                {
                    right = Vector3.Perp2D(Vector3.SafeNormalize2D(avatar.Forward, Vector3.XAxis));
                    orientation = avatar.RegionLocation.Orientation;
                    break;
                }
            }

            Vector3 preferredPosition = anchorPosition + right * RiftCompletionCrafterSpawnOffset;
            cell = anchorCell ?? region.GetCellAtPosition(anchorPosition);
            if (vendorProto.Bounds != null && cell != null)
            {
                Bounds spawnBounds = new(vendorProto.Bounds, preferredPosition);
                if (region.ChoosePositionAtOrNearPoint(
                    ref spawnBounds,
                    Region.GetPathFlagsForEntity(vendorProto),
                    PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.PreferNoEntity,
                    BlockingCheckFlags.None,
                    RiftCompletionCrafterSpawnOffset,
                    out Vector3 resolvedPosition,
                    maxPositionTests: 64))
                {
                    Cell resolvedCell = region.GetCellAtPosition(resolvedPosition);
                    if (resolvedCell != null)
                    {
                        position = RegionLocation.ProjectToFloor(region, resolvedPosition);
                        cell = resolvedCell;
                        return true;
                    }
                }
            }

            position = preferredPosition;
            return FinalizeReturnPortalSpawnLocation(region, ref position, ref cell);
        }

        public bool TryUseReturnPortal(Player player, Transition transition)
        {
            if (player == null || transition == null)
                return false;

            MythicRiftRunState runState = _activeRuns.Values.FirstOrDefault(run => run.ExitPortalEntityId == transition.Id);
            if (runState == null || runState.Status != MythicRiftRunStatus.Success)
                return false;

            if (TryResolveDangerRoomHubStartTarget(out PrototypeId dangerRoomHubStartTarget) == false)
                return false;

            using Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>();
            teleporter.Initialize(player, TeleportContextEnum.TeleportContext_Debug);
            teleporter.DifficultyTierRef = GameDatabase.GlobalsPrototype.DifficultyTierDefault;
            bool teleported = teleporter.TeleportToTarget(dangerRoomHubStartTarget);
            if (teleported)
                Logger.Info($"Mythic Rift run {runState.Config.RunId} used return portal 0x{transition.Id:X} for playerDbId=0x{player.DatabaseUniqueId:X}.");
            else
                Logger.Warn($"Mythic Rift run {runState.Config.RunId} failed to use return portal 0x{transition.Id:X} for playerDbId=0x{player.DatabaseUniqueId:X}.");

            return teleported;
        }

        public bool TryBlockUnsafeRiftTransition(Player player, Transition transition)
        {
            if (player == null || transition == null)
                return false;

            MythicRiftRunState runState = FindCheckpointRunForTransition(player, transition);
            if (runState == null)
                return false;

            string message = runState.Status == MythicRiftRunStatus.Success
                ? "[Mythic Rift] Use the Mythic Rift exit portal to return to the Danger Room hub."
                : "[Mythic Rift] This room exit is disabled during checkpoint Rifts. Defeat the boss, then use the Mythic Rift exit portal.";
            Game.ChatManager.SendChatFromCustomSystem(player, message, showSender: false);
            Logger.Info($"Mythic Rift run {runState.Config.RunId} blocked native checkpoint transition 0x{transition.Id:X} ({transition.PrototypeName}) for playerDbId=0x{player.DatabaseUniqueId:X}.");
            return true;
        }

        private MythicRiftRunState FindCheckpointRunForTransition(Player player, Transition transition)
        {
            Region playerRegion = player?.GetRegion();
            Region transitionRegion = transition?.Region;
            if (playerRegion == null || transitionRegion == null || playerRegion.Id != transitionRegion.Id)
                return null;

            foreach (MythicRiftRunState runState in _activeRuns.Values)
            {
                if (runState.RegionId != transitionRegion.Id)
                    continue;

                if (runState.Config.Content.BossOnlyCheckpointEligible == false)
                    continue;

                if (runState.Status != MythicRiftRunStatus.Active && runState.Status != MythicRiftRunStatus.Success)
                    continue;

                if (runState.ExitPortalEntityId != 0 && transition.Id == runState.ExitPortalEntityId)
                    continue;

                return runState;
            }

            return null;
        }

        private static ulong GetFirstRunAvatarId(Region region)
        {
            if (region == null)
                return 0;

            foreach (Player player in new PlayerIterator(region))
            {
                Avatar avatar = player.CurrentAvatar;
                if (avatar != null && avatar.IsInWorld)
                    return avatar.Id;
            }

            return 0;
        }

        private bool TryGetReturnPortalSpawnLocation(MythicRiftRunState runState, Region region, out Vector3 position, out Orientation orientation, out Cell cell)
        {
            position = Vector3.Zero;
            orientation = Orientation.Zero;
            cell = null;

            if (region == null)
                return false;

            if (runState?.BossEntityId != 0)
            {
                WorldEntity bossEntity = Game.EntityManager.GetEntity<WorldEntity>(runState.BossEntityId);
                if (bossEntity?.IsInWorld == true && bossEntity.Region == region)
                {
                    position = bossEntity.RegionLocation.Position;
                    orientation = bossEntity.RegionLocation.Orientation;
                    cell = bossEntity.Cell;
                    return FinalizeReturnPortalSpawnLocation(region, ref position, ref cell);
                }
            }

            foreach (Player player in new PlayerIterator(region))
            {
                Avatar avatar = player?.CurrentAvatar;
                if (avatar?.IsInWorld != true || avatar.Region != region)
                    continue;

                position = avatar.RegionLocation.Position + avatar.Forward * 150f;
                orientation = avatar.RegionLocation.Orientation;
                cell = avatar.Cell;
                return FinalizeReturnPortalSpawnLocation(region, ref position, ref cell);
            }

            return false;
        }

        private static bool FinalizeReturnPortalSpawnLocation(Region region, ref Vector3 position, ref Cell cell)
        {
            position = RegionLocation.ProjectToFloor(region, position);
            cell ??= region.GetCellAtPosition(position);
            return cell != null;
        }

        private bool TryAbortRunForDisconnectedParticipants(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.IsInProgress == false || runState.ParticipantCount == 0)
                return false;

            TimeSpan timeSinceLastParticipantSeen = currentTime - runState.LastParticipantOnlineAt;
            if (timeSinceLastParticipantSeen < ParticipantDisconnectAbortGracePeriod)
                return false;

            return AbortRun(
                runState,
                currentTime,
                "All participants disconnected. The Rift has closed.");
        }

        private bool TryAbortStalePendingRun(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Pending || runState.RegionId != 0)
                return false;

            TimeSpan pendingDuration = currentTime - runState.RegisteredAt;
            if (pendingDuration < PendingRunBindGracePeriod)
                return false;

            return AbortRun(
                runState,
                currentTime,
                "The Rift could not be opened correctly. Please try again with a new Beacon.");
        }

        private void NotifyRunStarted(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            string waveText = runState.Config.UseThirtyWaveMode
                ? $" | Wave {runState.Config.WaveNumber}/30 | Bosses: {runState.Config.RequiredBossKillCount}"
                : string.Empty;
            string message = runState.Config.Content.BossOnlyCheckpointEligible
                ? $"[Mythic Rift] Checkpoint Rift started: {runState.Config.Content.DisplayName} | Level {runState.Config.RiftLevel}{waveText} | Timer: {FormatDuration(runState.Config.TimeLimit)}. Defeat the empowered boss wave to unlock the next tier."
                : $"[Mythic Rift] Rift started: {runState.Config.Content.DisplayName} | Level {runState.Config.RiftLevel}{waveText} | Timer: {FormatDuration(runState.Config.TimeLimit)}. Defeat {runState.Config.KillQuota} enemies to summon the Rift boss wave.";
            string modifierText = BuildRiftModifierText(runState.Config);
            if (string.IsNullOrWhiteSpace(modifierText) == false)
                message += $" Modifiers: {modifierText}.";
            NotifyRunPlayers(runState, message);
        }

        private void NotifyBossUnlocked(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            int bossCount = Math.Max(runState.Config.RequiredBossKillCount, 1);
            string bossLabel = bossCount == 1 ? "boss" : $"{bossCount} bosses";
            string actionText = runState.ReadyCheckEndsAt.HasValue ? "preparing" : "summoned";
            string message = runState.Config.Content.BossOnlyCheckpointEligible
                ? $"[Mythic Rift] Checkpoint {bossLabel} {actionText}: {ResolveBossDisplayName(runState.Config)}. Defeat the full wave before the timer expires."
                : $"[Mythic Rift] Enemy quota complete. Final {bossLabel} {actionText}: {ResolveBossDisplayName(runState.Config)}. Defeat the full wave before the timer expires.";
            NotifyRunPlayers(runState, message);
        }

        private static string BuildRiftModifierText(MythicRiftRunConfig config)
        {
            if (config == null)
                return string.Empty;

            List<string> modifierGroups = new();
            if (config.RegionAffixes != null && config.RegionAffixes.Count > 0)
                modifierGroups.Add($"Rift: {FormatPrototypeNameList(config.RegionAffixes)}");

            if (config.BossAffixes != null && config.BossAffixes.Count > 0)
                modifierGroups.Add($"Boss: {FormatPrototypeNameList(config.BossAffixes)}");

            return string.Join(" | ", modifierGroups);
        }

        private static string FormatPrototypeNameList(IReadOnlyList<PrototypeId> prototypeRefs)
        {
            if (prototypeRefs == null || prototypeRefs.Count == 0)
                return "none";

            return string.Join(", ", prototypeRefs
                .Where(prototypeRef => prototypeRef != PrototypeId.Invalid)
                .Select(FormatPrototypeNameShort));
        }

        private static string FormatPrototypeNameShort(PrototypeId prototypeRef)
        {
            string name = prototypeRef.GetNameFormatted();
            if (string.IsNullOrWhiteSpace(name))
                return prototypeRef.ToString();

            int slashIndex = name.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex + 1 < name.Length)
                name = name[(slashIndex + 1)..];

            return name
                .Replace(".prototype", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(".defaults", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private void TryNotifyKillProgress(MythicRiftRunState runState)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.BossUnlocked)
                return;

            int requiredCount = Math.Max(runState.Config.KillQuota, 1);
            int progressPercent = (int)Math.Floor((double)runState.CurrentKillCount * 100d / requiredCount);
            foreach (int milestonePercent in KillProgressMilestonePercents)
            {
                if (progressPercent < milestonePercent)
                    continue;

                if (runState.MarkKillProgressMilestoneSent(milestonePercent) == false)
                    continue;

                int displayedKillCount = Math.Min(runState.CurrentKillCount, requiredCount);
                NotifyRunPlayers(runState, $"[Mythic Rift] Progress: {displayedKillCount}/{requiredCount} enemy progress.");
            }
        }

        private void NotifyRunCompleted(MythicRiftRunState runState, bool success, string statusMessage)
        {
            if (runState == null)
                return;

            string successPrefix = runState.Config.Content.BossOnlyCheckpointEligible
                ? "Checkpoint cleared"
                : "Rift complete";

            string message = success
                ? $"[Mythic Rift] {successPrefix}! {runState.BossKillCount} Rift boss(es) defeated in {runState.Config.Content.DisplayName}. Level {runState.Config.RiftLevel} cleared. {statusMessage}"
                : $"[Mythic Rift] Rift closed: {runState.Config.Content.DisplayName} | Level {runState.Config.RiftLevel}. {statusMessage}";
            NotifyRunPlayers(runState, message);
        }

        private void TrySendRiftClearedBanner(MythicRiftRunState runState)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Success)
                return;

            LocaleStringId bannerText = GetRiftClearedBannerLocaleStringId(runState.Config.RunId);
            foreach (Player player in GetRunPlayers(runState))
            {
                player.SendBannerMessage(
                    bannerText,
                    TextStylePrototype.BannerMessageLarge,
                    RiftClearedBannerTimeToLiveMS,
                    BannerMessageStyle.FlyIn,
                    doNotQueue: true,
                    showImmediately: true);
            }
        }

        private static LocaleStringId GetRiftClearedBannerLocaleStringId(ulong runId)
        {
            ulong offset = runId % (ulong)RiftClearedBannerLocaleStringCount;
            return (LocaleStringId)(RiftClearedBannerLocaleStringBase + offset + 1UL);
        }

        private void TryNotifyRunTimeWarnings(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active || runState.ExpiresAt.HasValue == false)
                return;

            TimeSpan remaining = runState.GetTimeRemaining(currentTime);
            int remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            foreach (int thresholdSeconds in TimeWarningThresholdSeconds)
            {
                if (remainingSeconds > thresholdSeconds)
                    continue;

                if (runState.MarkTimeWarningSent(thresholdSeconds) == false)
                    continue;

                NotifyRunPlayers(runState, $"[Mythic Rift] Time remaining: {FormatDuration(TimeSpan.FromSeconds(thresholdSeconds))}.");
            }
        }

        private static string BuildJoinMessage(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null)
                return "[Mythic Rift] You joined an active Rift.";

            string remaining = FormatDuration(runState.GetTimeRemaining(currentTime));
            if (runState.Config.Content.BossOnlyCheckpointEligible)
                return $"[Mythic Rift] You joined a checkpoint Rift. Final boss: {ResolveBossDisplayName(runState.Config)}. Time remaining: {remaining}.";

            if (runState.BossUnlocked)
                return $"[Mythic Rift] You joined an active Rift. Final boss: {ResolveBossDisplayName(runState.Config)}. Time remaining: {remaining}.";

            int remainingKills = Math.Max(runState.Config.KillQuota - runState.CurrentKillCount, 0);
            return $"[Mythic Rift] You joined an active Rift. Defeat {remainingKills} more enemies to summon the Rift boss. Time remaining: {remaining}.";
        }

        private void SendStartRiftTimer(MythicRiftRunState runState, Player player = null)
        {
            if (runState == null || runState.Status != MythicRiftRunStatus.Active)
                return;

            TimeSpan remaining = runState.GetTimeRemaining(Game.CurrentTime);
            if (remaining <= TimeSpan.Zero)
                return;

            try
            {
                NetMessageStartPvPTimer message = NetMessageStartPvPTimer.CreateBuilder()
                    .SetMetaGameId(runState.Config.RunId)
                    .SetStartTime((uint)Math.Min(remaining.TotalMilliseconds, uint.MaxValue))
                    .SetEndTime(0)
                    .SetLowTimeWarning((uint)TimeSpan.FromMinutes(2).TotalMilliseconds)
                    .SetCriticalTimeWarning((uint)TimeSpan.FromMinutes(1).TotalMilliseconds)
                    .SetLabelOverrideTextId(0UL)
                    .Build();

                if (player != null)
                {
                    player.SendMessage(message);
                    return;
                }

                SendMessageToRunPlayers(runState, message);
            }
            catch (Exception e)
            {
                Logger.Warn($"Mythic Rift run {runState.Config.RunId} failed to send optional timer UI packet: {e.Message}");
            }
        }

        private void SendStopRiftTimer(MythicRiftRunState runState)
        {
            if (runState == null)
                return;

            NetMessageStopPvPTimer message = NetMessageStopPvPTimer.CreateBuilder()
                .SetMetaGameId(runState.Config.RunId)
                .Build();

            SendMessageToRunPlayers(runState, message);
        }

        private void NotifyRunPlayers(MythicRiftRunState runState, string message)
        {
            if (runState == null || string.IsNullOrWhiteSpace(message))
                return;

            foreach (Player player in GetRunPlayers(runState))
                Game.ChatManager.SendChatFromCustomSystem(player, message, showSender: false);
        }

        private void SendMessageToRunPlayers(MythicRiftRunState runState, Google.ProtocolBuffers.IMessage message)
        {
            if (runState == null || message == null)
                return;

            foreach (Player player in GetRunPlayers(runState))
                player.SendMessage(message);
        }

        private IEnumerable<Player> GetRunPlayers(MythicRiftRunState runState)
        {
            if (runState == null)
                yield break;

            HashSet<ulong> recipientDbIds = new(runState.ParticipantPlayerDbIds);
            if (runState.RegionId != 0)
            {
                Region region = Game.RegionManager.GetRegion(runState.RegionId);
                if (region != null)
                {
                    foreach (Player regionPlayer in new PlayerIterator(region))
                    {
                        if (runState.HasParticipantLeftEarly(regionPlayer.DatabaseUniqueId) == false)
                            recipientDbIds.Add(regionPlayer.DatabaseUniqueId);
                    }
                }
            }

            foreach (ulong playerDbId in recipientDbIds)
            {
                Player player = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                if (player == null)
                    continue;

                yield return player;
            }
        }

        private static string ResolveBossDisplayName(MythicRiftRunConfig config)
        {
            if (config?.BossWaveContent?.Count > 1)
            {
                return string.Join(", ", config.BossWaveContent.Select(content =>
                    TrimTerminalSuffix(content?.DisplayName ?? content?.Id ?? "Unknown Boss")));
            }

            if (config?.BossContent?.DisplayName == null)
                return config?.BossProtoRef.GetNameFormatted() ?? "Unknown Boss";

            return TrimTerminalSuffix(config.BossContent.DisplayName);
        }

        private static string TrimTerminalSuffix(string bossName)
        {
            if (string.IsNullOrWhiteSpace(bossName))
                return "Unknown Boss";

            const string terminalSuffix = " Terminal";
            if (bossName.EndsWith(terminalSuffix, StringComparison.OrdinalIgnoreCase))
                return bossName[..^terminalSuffix.Length];

            return bossName;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            int totalSeconds = (int)Math.Ceiling(duration.TotalSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            if (minutes > 0 && seconds > 0)
                return $"{minutes} min {seconds} sec";

            if (minutes > 0)
                return $"{minutes} min";

            return $"{seconds} sec";
        }

        private static bool ShouldAutoRemoveRun(MythicRiftRunState runState, TimeSpan currentTime)
        {
            if (runState == null || runState.CompletedAt.HasValue == false)
                return false;

            return currentTime - runState.CompletedAt.Value >= CompletedRunRetention;
        }

        private bool ShouldRemoveCompletedRunBecauseRegionIsEmpty(MythicRiftRunState runState)
        {
            if (runState == null || runState.CompletedAt.HasValue == false || runState.RegionId == 0)
                return false;

            Region region = Game.RegionManager.GetRegion(runState.RegionId);
            if (region == null)
                return true;

            foreach (Player _ in new PlayerIterator(region))
                return false;

            return true;
        }

        private static bool TryResolveDangerRoomHubStartTarget(out PrototypeId startTargetRef)
        {
            startTargetRef = PrototypeId.Invalid;

            RegionPrototype dangerRoomHubRegion = ((PrototypeId)RegionPrototypeId.DangerRoomHubRegion).As<RegionPrototype>();
            if (dangerRoomHubRegion == null || dangerRoomHubRegion.StartTarget == PrototypeId.Invalid)
                return false;

            startTargetRef = dangerRoomHubRegion.StartTarget;
            return true;
        }

        private void RegisterDefaultContent()
        {
            foreach (MythicRiftContentDefinition definition in DefaultContentDefinitions)
                RegisterContent(definition);
        }

        private void RegisterContent(MythicRiftContentDefinition definition)
        {
            if (definition == null)
                return;

            MythicRiftContentEntry content = new()
            {
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                DefaultKillQuota = definition.DefaultKillQuota,
                RegionProtoRef = ResolvePrototype(definition.RegionPrototypeName),
                StartTargetProtoRef = ResolveStartTarget(definition.RegionPrototypeName),
                MissionProtoRef = ResolvePrototype(definition.MissionPrototypeName),
                BossProtoRef = ResolvePrototype(definition.BossPrototypeName),
                BossLootTableProtoRef = ResolvePrototype(definition.BossLootTablePrototypeName),
                RandomMapEligible = definition.RandomMapEligible,
                RandomBossEligible = definition.RandomBossEligible,
                IsSpecialRandomMap = definition.IsSpecialRandomMap,
                UseOwnBossSourceWhenSelected = definition.UseOwnBossSourceWhenSelected,
                UseCustomPopulation = definition.UseCustomPopulation,
                BossOnlyCheckpointEligible = definition.BossOnlyCheckpointEligible,
                BossFamily = ResolveBossFamily(
                    definition.BossFamily,
                    definition.Id,
                    definition.DisplayName,
                    definition.BossPrototypeName),
                MinRandomRiftLevel = definition.MinRandomRiftLevel,
                MaxRandomRiftLevel = definition.MaxRandomRiftLevel,
                MaxPlayerCount = definition.MaxPlayerCount
            };

            if (content.IsValid == false)
            {
                Logger.Warn($"RegisterContent(): failed to resolve mythic rift content id={definition.Id}");
                return;
            }

            RegisterContent(content);
        }

        private static string ResolveBossFamily(string explicitBossFamily, params string[] identityParts)
        {
            string explicitFamily = NormalizeBossFamily(explicitBossFamily);
            if (string.IsNullOrWhiteSpace(explicitFamily) == false)
                return explicitFamily;

            string identity = string.Join(" ", identityParts.Where(part => string.IsNullOrWhiteSpace(part) == false));
            string normalizedIdentity = NormalizeBossFamily(identity);
            if (string.IsNullOrWhiteSpace(normalizedIdentity))
                return string.Empty;

            string compactIdentity = normalizedIdentity.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            (string Family, string[] Keywords)[] knownFamilies =
            {
                ("doctor-octopus", new[] { "doctoroctopus", "docock", "octopus" }),
                ("green-goblin", new[] { "greengoblin" }),
                ("mister-sinister", new[] { "mistersinister", "mrsinister", "sinister" }),
                ("magneto", new[] { "magneto" }),
                ("juggernaut", new[] { "juggernaut" }),
                ("kingpin", new[] { "kingpin" }),
                ("taskmaster", new[] { "taskmaster" }),
                ("bullseye", new[] { "bullseye" }),
                ("elektra", new[] { "elektra" }),
                ("venom", new[] { "venom" }),
                ("carnage", new[] { "carnage" }),
                ("loki", new[] { "loki" }),
                ("malekith", new[] { "malekith" }),
                ("surtur", new[] { "surtur" }),
                ("ultron", new[] { "ultron" }),
                ("modok", new[] { "modok" }),
                ("doom", new[] { "doom" }),
                ("mandarin", new[] { "mandarin" }),
                ("hood", new[] { "hood" }),
                ("wizard", new[] { "wizard" }),
                ("rhino", new[] { "rhino" }),
                ("shocker", new[] { "shocker" }),
                ("sabretooth", new[] { "sabretooth", "sabertooth" }),
                ("sauron", new[] { "sauron" }),
                ("kurse", new[] { "kurse" }),
                ("blob", new[] { "blob" }),
                ("toad", new[] { "toad" })
            };

            foreach ((string family, string[] keywords) in knownFamilies)
            {
                if (keywords.Any(keyword => compactIdentity.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return family;
            }

            string fallback = normalizedIdentity;
            foreach (string prefix in new[] { "boss-", "danger-room-", "terminal-", "cosmic-", "midtown-", "hightown-", "icp-", "story-" })
            {
                if (fallback.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    fallback = fallback[prefix.Length..];
            }

            return fallback;
        }

        private static string NormalizeBossFamily(string family)
        {
            if (string.IsNullOrWhiteSpace(family))
                return string.Empty;

            Span<char> buffer = stackalloc char[family.Length];
            int length = 0;
            bool wroteSeparator = false;
            foreach (char ch in family.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    buffer[length++] = char.ToLowerInvariant(ch);
                    wroteSeparator = false;
                }
                else if (length > 0 && wroteSeparator == false)
                {
                    buffer[length++] = '-';
                    wroteSeparator = true;
                }
            }

            if (length > 0 && buffer[length - 1] == '-')
                length--;

            return length == 0 ? string.Empty : new(buffer[..length]);
        }

        private static PrototypeId ResolvePrototype(string prototypeName)
        {
            if (string.IsNullOrWhiteSpace(prototypeName))
                return PrototypeId.Invalid;

            PrototypeId prototypeRef = GameDatabase.GetPrototypeRefByName(prototypeName);
            if (prototypeRef == PrototypeId.Invalid)
                Logger.Warn($"ResolvePrototype(): failed to resolve {prototypeName}");

            return prototypeRef;
        }

        private static PrototypeId ResolveStartTarget(string regionPrototypeName)
        {
            PrototypeId regionProtoRef = ResolvePrototype(regionPrototypeName);
            if (regionProtoRef == PrototypeId.Invalid)
                return PrototypeId.Invalid;

            RegionPrototype regionProto = regionProtoRef.As<RegionPrototype>();
            if (regionProto == null)
                return PrototypeId.Invalid;

            return regionProto.StartTarget;
        }

        private static int ResolveKillQuota(MythicRiftContentEntry content, int killQuota)
        {
            if (killQuota > 0)
                return killQuota;

            if (content != null && content.DefaultKillQuota > 0)
                return content.DefaultKillQuota;

            return 50;
        }

        private void SyncOnlinePlayerRiftLevel(ulong playerDbId, int unlockedLevel, MythicRiftMode mode)
        {
            if (playerDbId == 0)
                return;

            Player onlinePlayer = Game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
            if (onlinePlayer == null)
                return;

            switch (mode)
            {
                case MythicRiftMode.Endless:
                    onlinePlayer.EndlessRiftHighestUnlockedLevel = unlockedLevel;
                    break;
                case MythicRiftMode.BossGauntlet:
                    break;
                default:
                    onlinePlayer.MythicRiftHighestUnlockedLevel = unlockedLevel;
                    break;
            }
        }

        private bool TryFindInProgressRunConflict(IEnumerable<ulong> playerDbIds, out MythicRiftRunState conflictingRun)
        {
            conflictingRun = null;
            if (playerDbIds == null)
                return false;

            foreach (ulong playerDbId in playerDbIds)
            {
                conflictingRun = GetInProgressRunForPlayer(playerDbId);
                if (conflictingRun != null)
                    return true;
            }

            return false;
        }

        private sealed record PendingRewardChest(
            ulong RunId,
            ulong RegionId,
            ulong PlayerDbId,
            IReadOnlyList<PendingRewardDrop> RewardDrops,
            float BonusRarityPct,
            float BonusSpecialPct);

        private sealed class PendingRewardDrop
        {
            public PrototypeId LootTableProtoRef { get; private init; } = PrototypeId.Invalid;
            public int ItemLevel { get; private init; }
            public string Id { get; private init; }
            public MythicRiftRewardGuaranteedItem GuaranteedItem { get; private init; }

            public static PendingRewardDrop CreateLootTable(PrototypeId lootTableProtoRef, int itemLevel, string id)
            {
                return new()
                {
                    LootTableProtoRef = lootTableProtoRef,
                    ItemLevel = itemLevel,
                    Id = id
                };
            }

            public static PendingRewardDrop CreateGuaranteedItem(MythicRiftRewardGuaranteedItem guaranteedItem)
            {
                return new()
                {
                    Id = guaranteedItem?.Id,
                    GuaranteedItem = guaranteedItem
                };
            }
        }

        private sealed record MythicRiftContentDefinition(
            string Id,
            string DisplayName,
            int DefaultKillQuota,
            string RegionPrototypeName,
            string MissionPrototypeName,
            string BossPrototypeName,
            string BossLootTablePrototypeName,
            bool RandomMapEligible = true,
            bool RandomBossEligible = true,
            bool IsSpecialRandomMap = false,
            bool UseOwnBossSourceWhenSelected = false,
            bool UseCustomPopulation = false,
            bool BossOnlyCheckpointEligible = false,
            int MinRandomRiftLevel = 1,
            int MaxRandomRiftLevel = 0,
            int MaxPlayerCount = 0,
            string BossFamily = null);
    }
}
