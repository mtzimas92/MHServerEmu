using Gazillion;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.PowerCollections;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.Events.Templates;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Navi;
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.MetaGames.GameModes
{
    public class PvEScaleGameMode : MetaGameMode
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        // Field semantics reverse-engineered from data (no reference implementation survives):
        // threat rises steadily over time (WaveDifficultyPerSecond), is pushed back down a little
        // by killing wave mobs (MobTotalDifficultyReduction, split across each spawned batch), and gets
        // a bigger, deliberate knock-down from collecting the phase's power-up orb
        // (PowerUpDifficultyReduction) - matches the in-game quest text ("collect power flares to
        // decrease the threat"). Survive the full WaveDurationMS without threat reaching
        // WaveDifficultyFailureThreshold to win.
        private const float WaveSpawnRadius = 1200f;
        private const float PowerUpSpawnRadius = 3000f;
        private const int WaveTickIntervalMS = 6000;
        private const float MaxSpawnSearchDistance = 800f;

        // Measured live (level 60, semi-geared): the original flat 2 clusters/player/tick under-spawned
        // relative to kill capacity. Bumping to a 2->3 ramp + a 5-cluster opening burst fixed the dead-time
        // problem, but once the spawn-in invuln shield bug was separately fixed (WaveOnSpawnPower - see
        // SpawnOneWaveCluster), mobs became fully engageable immediately and that same density proved to be
        // more than a player could keep up with (tracked-alive count climbed into the 50s-60s over a phase,
        // ending in death). Cut back down now that mobs no longer get a free multi-second grace window.
        // Starting point for further live tuning, not a final curve.
        private const float WaveClustersPerPlayerStart = 1.5f;
        private const float WaveClustersPerPlayerEnd = 2.5f;
        private const int InitialBurstClustersPerPlayer = 3;

        // Generic "counter bar" widget shared by all Danger Room region-score tracking - reused here
        // rather than authoring a new one, since GetWidget<T> lazily creates/registers it on first use.
        // Tried switching to the unused UI/MetaGame/ObjectiveCountRight.prototype (2026-07-25) to get a
        // Dinos-specific label without touching this shared widget's text (it's referenced by 113 real
        // native Danger Room mission-activate states) - confirmed via testing that ObjectiveCountRight's
        // MissionObjectiveCount lineage renders a client-side-baked "$MissionObjectiveName$" placeholder
        // and a plain counter instead of a bar, REGARDLESS of server-side Descriptor/WidgetMovieClipOverride/
        // DisplayMissionName patches (same "client resolves from its own local copy" class of dead end as
        // the AreaName fix). Reverted back to this shared widget until a genuinely safe replacement with
        // matching native (unpatched) client-side behavior is found - see the disabled patch entries in
        // PatchDataMod_Event_DinosInvadeManhattan.json for what was tried.
        private static readonly PrototypeId ThreatMeterWidgetRef = (PrototypeId)1488507445230442250;

        // No BossMedium marker exists anywhere in this region's actual generated cellset (confirmed via
        // a full cell sweep - every BossMedium marker in the game lives in unrelated story-instance
        // cells never part of this region's generator), despite boss clusters requesting it natively.
        // The purpose-built arena cells (UESvsDinos_SuperParkLeft_A/_Right_A) instead carry
        // SpecialEncounterMarker markers - the actual intended boss spawn point.
        private static readonly PrototypeId BossArenaMarkerRef = (PrototypeId)9370422672432174362;

        // Wave clusters have no UsePopulationMarker of their own (confirmed - none of the Dinosaur
        // cluster prototypes request one, unlike the boss cluster), so a purely freeform radius+navmesh
        // search was used for their spawn position. That search let mobs land inside buildings, because
        // building interiors in this borrowed Upper East Side terrain are legitimately walkable navmesh,
        // just not intended for random encounter spawns - real patrol content never has this problem
        // because it always spawns at hand-placed Encounter markers instead of computing points freely.
        // The actual Upper_East_Side_A/B cells this region's Area generator draws from (confirmed via a
        // full cell sweep) are dense with these same hand-placed markers, so build a position pool from
        // them and prefer it over freeform points.
        private static readonly PrototypeId[] WaveEncounterMarkerRefs =
        {
            (PrototypeId)14723915334591517051, // EncounterTiny
            (PrototypeId)392708073803551267,   // EncounterTinyV2
            (PrototypeId)14194767860991529508, // EncounterTinyV3
            (PrototypeId)17133636552251085349, // EncounterTinyV4
            (PrototypeId)13090379083104719399, // EncounterTinyV6
            (PrototypeId)18169821773171790288, // EncounterSmall
            (PrototypeId)12978219259455935097, // EncounterSmallV3
            (PrototypeId)9309559586219495992,  // EncounterMedium
            (PrototypeId)14205956048628683202, // EncounterLarge
        };
        private const float EncounterMarkerSearchRadius = 2400f;

        // Each wave phase (Base/Break) is a separate MetaGameMode instance, so _threat can't just live on
        // this object if it's meant to carry across phases instead of resetting every ~3 minutes. Persisted
        // here keyed by (Game.Id, MetaGame.Id) - shared by every mode belonging to the same playthrough -
        // written back on every UpdateThreatMeter() call, and cleared once the run truly ends (win via the
        // boss phase's SucceedMode, or any FailMode) rather than on every per-phase SucceedMode transition.
        //
        // MUST include Game.Id, not just MetaGame.Id: MetaGame.Id (like every Entity.Id) is only unique
        // WITHIN a single Game instance. GameThreadManager runs multiple concurrent Game instances in one
        // process, and WorldManager.GetOrCreatePublicRegion/RegionLoadBalancer can and does spin up more
        // than one live "Dinos Invade Manhattan" instance (each its own Game) under player load - two
        // totally unrelated concurrent runs can trivially get the same bare MetaGame.Id and, with only
        // that as the key, would read/write EACH OTHER'S persisted threat. Confirmed live via
        // DinosWaveBattleLogCollator sharing this exact same bug (fixed alongside this) - two unrelated
        // runs' log lines were interleaving in one file, which is what exposed this.
        private static readonly Dictionary<(ulong GameId, ulong MetaGameId), float> _persistedThreatByMetaGameId = new();

        private (ulong, ulong) PersistedThreatKey => (Game.Id, MetaGame.Id);

        private readonly PvEScaleGameModePrototype _proto;
        private readonly HashSet<ulong> _waveEntities = new();
        private readonly HashSet<ulong> _bossEntities = new();
        private readonly HashSet<ulong> _bossSupportEntities = new();
        private readonly Dictionary<ulong, float> _mobThreatReduction = new();
        private readonly List<Vector3> _encounterMarkerPositions = new();

        private readonly Event<EntityDeadGameEvent>.Action _entityDeadAction;
        private readonly Event<OrbPickUpEvent>.Action _orbPickUpAction;

        private readonly EventPointer<WaveTickEvent> _waveTickEvent = new();
        private readonly EventPointer<PhaseTimeoutEvent> _phaseTimeoutEvent = new();
        private readonly EventPointer<BossSpawnEvent> _bossSpawnEvent = new();
        private readonly EventPointer<PowerUpSpawnEvent> _powerUpSpawnEvent = new();

        private float _threat;
        private int _lastScaledPlayerCount;
        private bool _isBossPhase;
        private bool _modeEnded;
        private ulong _powerUpEntityId;
        private int _killCountThisPhase;
        private int _powerUpCountThisPhase;

        public PvEScaleGameMode(MetaGame metaGame, MetaGameModePrototype proto) : base(metaGame, proto)
        {
            _proto = proto as PvEScaleGameModePrototype;
            _entityDeadAction = OnEntityDead;
            _orbPickUpAction = OnOrbPickUp;
        }

        private bool IsWaveBattleLoggingEnabled => Game?.CustomGameOptions?.DinosWaveBattleLoggingEnable ?? false;

        private void LogWaveBattle(string line)
        {
            if (IsWaveBattleLoggingEnabled == false) return;
            DinosWaveBattleLogCollator.WriteLine(Game.Id, MetaGame.Id, line);
        }

        #region Override

        public override void OnActivate()
        {
            if (Region == null) return;
            base.OnActivate();
            SetModeText(_proto.Name);

            _threat = _persistedThreatByMetaGameId.TryGetValue(PersistedThreatKey, out float persistedThreat) ? persistedThreat : 0f;
            _lastScaledPlayerCount = Math.Max(1, CountInWorldPlayers());
	    _modeEnded = false;
            _powerUpEntityId = 0;
            _killCountThisPhase = 0;
            _powerUpCountThisPhase = 0;
            _isBossPhase = _proto.BossPopulationObjects.HasValue();
            UpdateThreatMeter();

            if (_isBossPhase == false)
            {
                _encounterMarkerPositions.Clear();
                foreach (PrototypeId markerRef in WaveEncounterMarkerRefs)
                    Region.SpawnMarkerRegistry.GetPositionsByMarker(markerRef, _encounterMarkerPositions);
            }

            if (MetaGame.Debug)
                Logger.Debug($"OnActivate(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} isBossPhase={_isBossPhase} durationMs={_proto.WaveDurationMS}");

            if (IsWaveBattleLoggingEnabled)
            {
                int playerCount = CountInWorldPlayers();
                Player firstPlayer = GetRandomPlayer();
                Avatar firstAvatar = firstPlayer?.CurrentAvatar;
                if (firstPlayer != null && firstAvatar != null)
                    DinosWaveBattleLogCollator.SetPlayerLabel(Game.Id, MetaGame.Id, $"{firstPlayer.GetName()}_L{firstAvatar.CharacterLevel}");

                LogWaveBattle($"PHASE_START phase={GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} boss={_isBossPhase} " +
                    $"durationS={_proto.WaveDurationMS / 1000} enteringThreat={_threat:F2}/{GetEffectiveFailureThreshold()} players={playerCount}");
            }

            Region.EntityDeadEvent.AddActionBack(_entityDeadAction);
            Region.OrbPickUpEvent.AddActionBack(_orbPickUpAction);

            if (_proto.WaveDurationMS > 0)
            {
                ScheduleEvent(_phaseTimeoutEvent, _proto.WaveDurationMS);

                // Phase1Break etc. (MetaGameModeIdle) already show a countdown for the between-wave break -
                // ShowTimer is true on the wave Base phases too, but PvEScaleGameMode never called
                // SendStartPvPTimer to act on it, so the mission tracker never showed a timer during the
                // actual wave. Mirrors MetaGameModeIdle.OnActivate()'s usage.
                if (_proto.ShowTimer)
                    SendStartPvPTimer(TimeSpan.FromMilliseconds(_proto.WaveDurationMS), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            }

            if (_isBossPhase)
                ScheduleEvent(_bossSpawnEvent, Math.Max(1, _proto.WaveBossDelayMS));
            else
            {
                SpawnWaveBatch(InitialBurstClustersPerPlayer);
                ScheduleEvent(_waveTickEvent, WaveTickIntervalMS);
            }

            if (_proto.PowerUpItem != PrototypeId.Invalid && _proto.PowerUpSpawnMS > 0)
                ScheduleEvent(_powerUpSpawnEvent, _proto.PowerUpSpawnMS);
        }

        // Base MetaGameMode.OnAddPlayer() only resends UINotificationOnActivate (the "Defeat Enemies
        // Wave 1" banner) to every player added to an already-active mode - it never (re)starts the
        // PvP timer for them. MetaGameModeIdle.OnAddPlayer() has the correct pattern (notification +
        // a player-targeted SendStartPvPTimer with the REMAINING duration, not the full one) but
        // PvEScaleGameMode never had its own override, so it fell back to the base's notification-only
        // behavior. On a shared PublicCombatZone instance (this region, now capped at 10 players) where
        // players join an already-running wave far more often than a fresh one, every joiner after the
        // first saw the phase-start banner with no visible timer ever appearing for them - not just
        // cosmetic, since the client-side countdown never got a start message to react to. Mirrors
        // MetaGameModeIdle.OnAddPlayer()/GetDurationTime() using this class's own _startTime instead.
        public override void OnAddPlayer(Player player)
        {
            if (player == null) return;
            base.OnAddPlayer(player);

            if (_modeEnded) return;

            if (_proto.ShowTimer && _proto.WaveDurationMS > 0)
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(_proto.WaveDurationMS) - (Game.CurrentTime - _startTime);
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                SendStartPvPTimer(remaining, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, player);
            }
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();

            if (_proto.ShowTimer)
                SendStopPvPTimer();

            Region?.EntityDeadEvent.RemoveAction(_entityDeadAction);
            Region?.OrbPickUpEvent.RemoveAction(_orbPickUpAction);

            var scheduler = Game?.GameEventScheduler;
            scheduler?.CancelAllEvents(_pendingEvents);

            DestroyTrackedEntities(_waveEntities);
            DestroyTrackedEntities(_bossEntities);
            DestroyTrackedEntities(_bossSupportEntities);
            _mobThreatReduction.Clear();

            if (_powerUpEntityId != 0)
            {
                Game?.EntityManager.GetEntity<WorldEntity>(_powerUpEntityId)?.Destroy();
                _powerUpEntityId = 0;
            }
        }

        public override bool OnResurrect(Player player)
        {
            if (_proto.DeathRegionTarget == PrototypeId.Invalid) return false;
            EjectPlayer(player);
            return true;
        }

        #endregion

        #region Wave Spawning

        private void ScheduledWaveTick()
        {
            if (_modeEnded || Region == null) return;

            // Reduction scales with player count (SpawnWaveBatch spawns ClustersPerPlayerPerTick clusters
            // PER PLAYER, each worth MobTotalDifficultyReduction in kill-reduction potential), so a full
            // group could hold threat near zero far more easily than a solo player. Scale the rise the
            // same way so per-capita pressure stays roughly constant regardless of group size.
            int inWorldPlayerCount = Math.Max(1, CountInWorldPlayers());

            // GetEffectiveFailureThreshold() scales the fail ceiling live off inWorldPlayerCount, but
            // _threat itself was accumulated relative to whatever the ceiling was on the ticks that
            // built it up. If a player leaves mid-phase, the ceiling instantly shrinks while the banked
            // threat doesn't - confirmed live: a 2-player run sitting comfortably under a 50 ceiling
            // (34.31) lost a player, the ceiling snapped to 25, and the very next tick auto-failed the
            // remaining player on progress that had nothing to do with their own performance. Rescale
            // the banked threat by the same ratio the ceiling just moved by, so threat/ceiling stays
            // constant across a player count change instead of the ceiling alone lurching underneath it.
            if (inWorldPlayerCount != _lastScaledPlayerCount)
            {
                float oldThreat = _threat;
                _threat = _threat * inWorldPlayerCount / _lastScaledPlayerCount;

                if (IsWaveBattleLoggingEnabled)
                    LogWaveBattle($"RESCALE threat={oldThreat:F2}->{_threat:F2} players={_lastScaledPlayerCount}->{inWorldPlayerCount}");

                _lastScaledPlayerCount = inWorldPlayerCount;
            }

	    _threat += _proto.WaveDifficultyPerSecond * inWorldPlayerCount * (WaveTickIntervalMS / 1000f);
            UpdateThreatMeter();

            int clustersPerPlayer = GetClustersPerPlayerThisTick();
            SpawnWaveBatch(clustersPerPlayer);

            if (MetaGame.Debug)
                Logger.Debug($"ScheduledWaveTick(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} threat={_threat:F2}/{GetEffectiveFailureThreshold()} tracked={_waveEntities.Count} clustersPerPlayer={clustersPerPlayer}");

            if (IsWaveBattleLoggingEnabled)
                LogWaveBattle($"TICK threat={_threat:F2}/{GetEffectiveFailureThreshold()} clustersPerPlayer={clustersPerPlayer} tracked={_waveEntities.Count} players={inWorldPlayerCount}");

            if (CheckThreatFailure()) return;

            ScheduleEvent(_waveTickEvent, WaveTickIntervalMS);
        }

        // Linear ramp from WaveClustersPerPlayerStart to WaveClustersPerPlayerEnd over the phase's
        // WaveDurationMS, with the fractional remainder resolved probabilistically so the average rate
        // over many ticks matches the ramp exactly instead of always rounding the same direction.
        private int GetClustersPerPlayerThisTick()
        {
            float elapsedFraction = 0f;
            if (_proto.WaveDurationMS > 0)
            {
                float elapsedMs = (float)(Game.CurrentTime - _startTime).TotalMilliseconds;
                elapsedFraction = Math.Clamp(elapsedMs / _proto.WaveDurationMS, 0f, 1f);
            }

            float clusters = WaveClustersPerPlayerStart + (WaveClustersPerPlayerEnd - WaveClustersPerPlayerStart) * elapsedFraction;
            int wholeClusters = (int)MathF.Floor(clusters);
            float fractional = clusters - wholeClusters;
            if (Game.Random.NextFloat() < fractional)
                wholeClusters++;

            return wholeClusters;
        }

        // Spawns near every player in the region (not just one random pick) so density scales with
        // player count.
        private void SpawnWaveBatch(int clustersPerPlayer)
        {
            using var playersHandle = ListPool<Player>.Instance.Get(out List<Player> players);
            foreach (Player player in MetaGame.Players)
                players.Add(player);

            foreach (var player in players)
            {
                Avatar avatar = player.CurrentAvatar;
                if (avatar == null || avatar.IsInWorld == false) continue;

                for (int i = 0; i < clustersPerPlayer; i++)
                    SpawnOneWaveCluster(avatar.RegionLocation.Position);
            }
        }

        private int CountInWorldPlayers()
        {
            int count = 0;
            foreach (Player player in MetaGame.Players)
            {
                Avatar avatar = player.CurrentAvatar;
                if (avatar != null && avatar.IsInWorld)
                    count++;
            }
            return count;
        }

        // Threat RISE already scales by player count (ScheduledWaveTick), but the failure ceiling used
        // to be a flat value regardless of group size - meaning a 10-player instance had 10x the
        // absolute threat swing per tick against the SAME fixed buffer a solo player used, with far less
        // margin for kill-lag/coordination overhead. Scaling the ceiling by the same per-player term
        // makes the rise-vs-ceiling RATIO constant regardless of group size, closing that gap. The
        // prototype's own WaveDifficultyFailureThreshold field is reused as the PER-PLAYER unit (patched
        // down from a flat 50 to 18 - the exact per-player passive-rise-per-phase number, WaveDifficultyPerSecond
        // * 180s - so zero kills/power-ups over a full phase lands exactly at the ceiling, not comfortably under it).
        private float GetEffectiveFailureThreshold()
        {
            return _proto.WaveDifficultyFailureThreshold * Math.Max(1, CountInWorldPlayers());
        }

        private void SpawnOneWaveCluster(Vector3 origin)
        {
            var popObj = ResolveWavePopulationObject();
            if (popObj == null)
            {
                Logger.Warn($"SpawnOneWaveCluster(): Failed to resolve WavePopulation for {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)}");
                return;
            }

            if (ChooseNearbyEncounterMarkerPosition(origin, out Vector3 desiredPosition) == false)
            {
                float angle = Game.Random.NextFloat() * MathF.PI * 2f;
                desiredPosition = origin + new Vector3(MathF.Cos(angle) * WaveSpawnRadius, MathF.Sin(angle) * WaveSpawnRadius, 0f);
            }
            Vector3 position = ChooseValidSpawnPosition(popObj, desiredPosition);

            using var spawnedHandle = ListPool<WorldEntity>.Instance.Get(out List<WorldEntity> spawned);
            Region.PopulationManager.SpawnObjectUsePosition(popObj, position, spawned);
            if (spawned.Count == 0) return;

            float perMobReduction = _proto.MobTotalDifficultyReduction > 0f
                ? _proto.MobTotalDifficultyReduction / spawned.Count
                : 0f;

            PrototypeId boostRef = PickRandomBoost(_proto.WaveEnemyBoosts, _proto.WaveEnemyBoostsPickCount);

            foreach (var entity in spawned)
            {
                if (entity == null) continue;
                _waveEntities.Add(entity.Id);
                _mobThreatReduction[entity.Id] = perMobReduction;
                MetaGame.DiscoverEntity(entity);

                if (boostRef != PrototypeId.Invalid)
                    entity.Properties[PropertyEnum.EnemyBoost, boostRef] = true;

                // NOT calling WaveOnSpawnPower here - confirmed root cause of the "blue shield, 5s
                // untargetable/undamageable but can still attack" complaint. WaveOnSpawnPower
                // (Powers/PvPPowers/PvEScaleWaveSpawnInEffect.prototype) inherits from
                // Powers/Blueprints/ConditionPowers/InvulnUntargetableImmobilePower.defaults - a real
                // invuln+untargetable+immobile condition, not a cosmetic-only spawn flourish. Standard
                // patrol spawns never hit this because it's specific to this metagame's own spawn/despawn
                // power slots, which this code is the one choosing to activate.
            }
        }

        // Picks a random hand-placed Encounter marker within range of origin instead of a freely
        // computed point, so wave mobs land where the region's own level design put encounter spots
        // (which are never inside buildings) rather than wherever a raw radius+navmesh search happens
        // to validate (which can be inside a building, since building interiors here are legitimately
        // walkable navmesh).
        // Picking uniformly among every marker within EncounterMarkerSearchRadius (2400) meant a typical
        // spawn was hundreds to ~2400 units out - at a raptor's 350 units/sec run speed that's up to ~6.9s
        // of closing distance before it's within normal engagement/health-bar range, which read as a
        // multi-second "invulnerable, but it can still hit me" window (it wasn't invulnerable - it just
        // hadn't arrived yet). Try a tight near-radius first so mobs typically spawn close enough to
        // engage almost immediately; only fall back to the full search radius (still far better than the
        // old freeform/building-risk method) when nothing hand-placed exists nearby.
        private const float EncounterMarkerNearRadius = 900f;

        private bool ChooseNearbyEncounterMarkerPosition(Vector3 origin, out Vector3 position)
        {
            position = Vector3.Zero;
            if (_encounterMarkerPositions.Count == 0) return false;

            if (TryPickWithinRadius(origin, EncounterMarkerNearRadius, out position))
                return true;

            return TryPickWithinRadius(origin, EncounterMarkerSearchRadius, out position);
        }

        private bool TryPickWithinRadius(Vector3 origin, float radius, out Vector3 position)
        {
            position = Vector3.Zero;

            using var candidatesHandle = ListPool<Vector3>.Instance.Get(out List<Vector3> candidates);
            foreach (Vector3 markerPosition in _encounterMarkerPositions)
                if (Vector3.DistanceSquared2D(origin, markerPosition) <= radius * radius)
                    candidates.Add(markerPosition);

            if (candidates.Count == 0) return false;

            position = candidates[Game.Random.Next(0, candidates.Count)];
            return true;
        }

        // WavePopulation isn't a spawnable PopulationObjectPrototype directly - it's a
        // PvEScaleWavePopulationPrototype wrapping Choices[] of PopulationRequiredObjectListPrototype,
        // each holding several PopulationRequiredObjectPrototype entries (the actual cluster templates).
        // MainSequence.prototype bundles 15 unrelated enemy-faction choices (AIM, Doombots, HYDRA,
        // Purifiers, street thugs, etc.) alongside the one Dinosaur/Beetle/Cliffwalker choice - for a
        // region literally called "Dinos Invade Manhattan", only the dino-themed choice(s) should spawn,
        // not a uniform pick across all of them.
        // King Lizard is being promoted to the wave's own final boss (see ScheduledBossSpawn /
        // BossPopulationObjects) - no longer a trash-tier wave spawn alongside its own boss form.
        private static readonly PrototypeId DinosaurEliteKingLizardClusterRef = (PrototypeId)12057864840882822400;

        private PopulationObjectPrototype ResolveWavePopulationObject()
        {
            if (_proto.WavePopulation == PrototypeId.Invalid) return null;

            var wavePopProto = GameDatabase.GetPrototype<PvEScaleWavePopulationPrototype>(_proto.WavePopulation);
            if (wavePopProto == null || wavePopProto.Choices.IsNullOrEmpty()) return null;

            var choices = GetThemedChoices(wavePopProto.Choices);
            var choice = choices[Game.Random.Next(0, choices.Count)];
            if (choice == null || choice.RequiredObjects.IsNullOrEmpty()) return null;

            var requiredObject = PickRequiredObject(choice.RequiredObjects);
            return requiredObject?.GetPopObject();
        }

        private PopulationRequiredObjectPrototype PickRequiredObject(PopulationRequiredObjectPrototype[] requiredObjects)
        {
            List<PopulationRequiredObjectPrototype> candidates = new();
            foreach (var obj in requiredObjects)
                if (obj != null && obj.ObjectTemplate != DinosaurEliteKingLizardClusterRef)
                    candidates.Add(obj);

            if (candidates.Count == 0)
                return requiredObjects[Game.Random.Next(0, requiredObjects.Length)];

            return candidates[Game.Random.Next(0, candidates.Count)];
        }

        private static List<PopulationRequiredObjectListPrototype> GetThemedChoices(PopulationRequiredObjectListPrototype[] choices)
        {
            List<PopulationRequiredObjectListPrototype> themed = new();
            foreach (var choice in choices)
            {
                if (choice?.RequiredObjects.IsNullOrEmpty() != false) continue;

                foreach (var requiredObject in choice.RequiredObjects)
                {
                    if (requiredObject == null || requiredObject.ObjectTemplate == PrototypeId.Invalid) continue;
                    string name = GameDatabase.GetFormattedPrototypeName(requiredObject.ObjectTemplate);
                    if (name.Contains("Dinosaur", StringComparison.OrdinalIgnoreCase))
                    {
                        themed.Add(choice);
                        break;
                    }
                }
            }

            // Fall back to the full pool if nothing matched, rather than spawning nothing.
            return themed.Count > 0 ? themed : new List<PopulationRequiredObjectListPrototype>(choices);
        }

        #endregion

        #region Boss

        private void ScheduledBossSpawn()
        {
            if (_modeEnded || Region == null) return;
            if (_proto.BossPopulationObjects.IsNullOrEmpty()) return;

            int index = Game.Random.Next(0, _proto.BossPopulationObjects.Length);
            var bossObj = GameDatabase.GetPrototype<PopulationObjectPrototype>(_proto.BossPopulationObjects[index]);
            if (bossObj == null)
            {
                Logger.Warn($"ScheduledBossSpawn(): Failed to resolve boss population object for {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)}");
                return;
            }

            Vector3 position = ChooseBossSpawnPosition(bossObj);

            using var spawnedHandle = ListPool<WorldEntity>.Instance.Get(out List<WorldEntity> spawned);
            Region.PopulationManager.SpawnObjectUsePosition(bossObj, position, spawned);
            if (spawned.Count == 0) return;

            // Boss clusters (e.g. MagnetoBossCluster) are a Leader + Henchmen formation - only the
            // Leader's death should count as victory. Without this, killing a single weak henchman
            // ends the fight instantly and the real boss just gets despawned mid-fight during cleanup.
            PrototypeId leaderRef = (bossObj as PopulationLeaderPrototype)?.Leader ?? PrototypeId.Invalid;
            PrototypeId boostRef = PickRandomBoost(_proto.BossEnemyBoosts, _proto.BossEnemyBoostsPicks);

            foreach (var entity in spawned)
            {
                if (entity == null) continue;
                MetaGame.DiscoverEntity(entity);
                if (boostRef != PrototypeId.Invalid)
                    entity.Properties[PropertyEnum.EnemyBoost, boostRef] = true;

                bool isLeader = leaderRef == PrototypeId.Invalid || entity.PrototypeDataRef == leaderRef;
                if (isLeader)
                    _bossEntities.Add(entity.Id);
                else
                    _bossSupportEntities.Add(entity.Id);
            }

            SendUINotification(_proto.BossUINotification);

            if (MetaGame.Debug)
                Logger.Debug($"ScheduledBossSpawn(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} spawned {GameDatabase.GetFormattedPrototypeName(bossObj.DataRef)} leader={leaderRef.GetNameFormatted()} total={spawned.Count} at {position}");
        }

        // Spawning near whichever player got randomly picked meant the boss could land on an isolated
        // player while the rest of the party was elsewhere killing waves. The region has a dedicated
        // arena (UESBossParkSuper's two custom cells) carrying native SpecialEncounterMarker markers -
        // use those instead so the fight always happens in the built arena, drawing the party together.
        private Vector3 ChooseBossSpawnPosition(PopulationObjectPrototype bossObj)
        {
            using var positionsHandle = ListPool<Vector3>.Instance.Get(out List<Vector3> positions);
            Region.SpawnMarkerRegistry.GetPositionsByMarker(BossArenaMarkerRef, positions);
            if (positions.Count > 0)
            {
                Vector3 arenaPosition = positions[Game.Random.Next(0, positions.Count)];
                return ChooseValidSpawnPosition(bossObj, arenaPosition);
            }

            Logger.Warn($"ChooseBossSpawnPosition(): No SpecialEncounterMarker found in {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)}'s region, falling back to a random player's position");

            Player player = GetRandomPlayer();
            Avatar avatar = player?.CurrentAvatar;
            Vector3 desiredPosition = avatar != null && avatar.IsInWorld ? avatar.RegionLocation.Position : Vector3.Zero;
            return ChooseValidSpawnPosition(bossObj, desiredPosition);
        }

        private PrototypeId PickRandomBoost(PvEScaleEnemyBoostEntryPrototype[] boosts, int pickCount)
        {
            if (boosts.IsNullOrEmpty() || pickCount <= 0) return PrototypeId.Invalid;
            int index = Game.Random.Next(0, boosts.Length);
            return boosts[index]?.EnemyBoost ?? PrototypeId.Invalid;
        }

        private PrototypeId PickRandomBoost(PrototypeId[] boosts, int pickCount)
        {
            if (boosts.IsNullOrEmpty() || pickCount <= 0) return PrototypeId.Invalid;
            int index = Game.Random.Next(0, boosts.Length);
            return boosts[index];
        }

        #endregion

        #region Power-Up

        // PvEWaveBattlePowerUpItem isn't a plain Item - it's a "vacuumable orb" agent (Orb AI profile,
        // 1 HP, untargetable/unaffectable) that homes in on and gets auto-collected by nearby players,
        // the same mechanic as health/XP orbs. Spawn it as a raw entity by ref (EntityHelper.CreateOrb
        // is the closest existing reference for this pattern, though it's permanently debug-gated).
        private void ScheduledPowerUpSpawn()
        {
            if (_modeEnded || Region == null) return;
            if (_proto.PowerUpItem == PrototypeId.Invalid) return;

            Player player = GetRandomPlayer();
            Avatar avatar = player?.CurrentAvatar;
            if (avatar == null || avatar.IsInWorld == false) return;

            Vector3 position = ChoosePowerUpPosition(avatar.RegionLocation.Position);

            using EntitySettings settings = ObjectPoolManager.Instance.Get<EntitySettings>();
            settings.EntityRef = _proto.PowerUpItem;
            settings.Position = position;
            settings.Orientation = Orientation.Zero;
            settings.RegionId = Region.Id;

            var entity = Game.EntityManager.CreateEntity(settings) as WorldEntity;
            if (entity == null)
            {
                Logger.Warn($"ScheduledPowerUpSpawn(): Failed to create power-up entity for {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)}");
                return;
            }

            _powerUpEntityId = entity.Id;
            MetaGame.DiscoverEntity(entity);

            // MetaGame.DiscoverEntity (above) is a separate, MetaGame-specific tracked-entity list -
            // it's Region.DiscoverEntity that actually drives the client-side map/edge-pointer icon
            // (WorldEntity.OnEnteredWorld() calls this automatically when DiscoverInRegion is true,
            // but call it explicitly here too in case entities created via direct EntityManager.CreateEntity
            // rather than the population system don't reliably hit that path).
            Region.DiscoverEntity(entity, true);

            SendUINotification(_proto.PowerUpSpawnUINotification);

            if (MetaGame.Debug)
                Logger.Debug($"ScheduledPowerUpSpawn(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} spawned power-up at {position}");
        }

        private Vector3 ChoosePowerUpPosition(Vector3 fallbackOrigin)
        {
            if (_proto.PowerUpMarkerType != PrototypeId.Invalid)
            {
                using var positionsHandle = ListPool<Vector3>.Instance.Get(out List<Vector3> positions);
                Region.SpawnMarkerRegistry.GetPositionsByMarker(_proto.PowerUpMarkerType, positions);
                if (positions.Count > 0)
                    return positions[Game.Random.Next(0, positions.Count)];
            }

            float angle = Game.Random.NextFloat() * MathF.PI * 2f;
            return fallbackOrigin + new Vector3(MathF.Cos(angle) * PowerUpSpawnRadius, MathF.Sin(angle) * PowerUpSpawnRadius, 0f);
        }

        #endregion

        #region Completion

        private bool CheckThreatFailure()
        {
            if (_threat < GetEffectiveFailureThreshold()) return false;

            FailMode();
            return true;
        }

        private void ScheduledPhaseTimeout()
        {
            if (_modeEnded) return;

            if (_isBossPhase)
            {
                FailMode();
                return;
            }

            SucceedMode();
        }

        private void OnEntityDead(in EntityDeadGameEvent evt)
        {
            var defender = evt.Defender;
            if (defender == null || _modeEnded) return;

            // Vacuumable orbs (this power-up's whole prototype family) are consumed via
            // Region.OrbPickUpEvent from their own AI profile, not the combat-death pipeline - this
            // branch is a harmless fallback in case a future PowerUpItem substitution is killed
            // through combat instead. See OnOrbPickUp for the path that actually fires.
            if (defender.Id == _powerUpEntityId)
            {
                CollectPowerUp(defender);
                return;
            }

            if (_bossEntities.Remove(defender.Id))
            {
                MetaGame.UniscoverEntity(defender);
                if (IsWaveBattleLoggingEnabled)
                    LogWaveBattle("BOSS_KILLED");
                SucceedMode();
                return;
            }

            if (_bossSupportEntities.Remove(defender.Id))
            {
                MetaGame.UniscoverEntity(defender);
                return;
            }

            if (_waveEntities.Remove(defender.Id))
            {
                MetaGame.UniscoverEntity(defender);
                if (_mobThreatReduction.TryGetValue(defender.Id, out float reduction))
                {
                    float threatBefore = _threat;
                    _threat = Math.Max(0f, _threat - reduction);
                    _mobThreatReduction.Remove(defender.Id);
                    UpdateThreatMeter();
                    _killCountThisPhase++;

                    if (IsWaveBattleLoggingEnabled)
                        LogWaveBattle($"KILL threat={threatBefore:F2}->{_threat:F2} reduction={reduction:F2} killsThisPhase={_killCountThisPhase}");
                }
            }
        }

        private void OnOrbPickUp(in OrbPickUpEvent evt)
        {
            if (_modeEnded || evt.Orb == null || evt.Orb.Id != _powerUpEntityId) return;
            CollectPowerUp(evt.Orb);
        }

        private void CollectPowerUp(WorldEntity orb)
        {
            _powerUpEntityId = 0;
            MetaGame.UniscoverEntity(orb);
            float threatBefore = _threat;
            // PowerUpDifficultyReduction is a 0.0-1.0 fraction of CURRENT threat (not a flat amount),
            // so a pickup stays meaningful whether it's collected early (low threat) or late (near the ceiling),
            // and its relative impact no longer shrinks as the player-scaled ceiling (GetEffectiveFailureThreshold) grows.
            float reduction = _threat * _proto.PowerUpDifficultyReduction;
            _threat = Math.Max(0f, _threat - reduction);
            UpdateThreatMeter();
            SendUINotification(_proto.PowerUpPickupUINotification);
            _powerUpCountThisPhase++;
            if (MetaGame.Debug)
                Logger.Debug($"CollectPowerUp(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} power-up collected, threat={_threat:F2}");
            if (IsWaveBattleLoggingEnabled)
                LogWaveBattle($"POWERUP threat={threatBefore:F2}->{_threat:F2} reduction={reduction:F2} powerUpsThisPhase={_powerUpCountThisPhase}");
        }

        private void SucceedMode()
        {
            if (_modeEnded) return;
            _modeEnded = true;
            if (MetaGame.Debug)
                Logger.Debug($"SucceedMode(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} -> mode index {_proto.NextMode}");

            if (IsWaveBattleLoggingEnabled)
            {
                LogWaveBattle($"PHASE_END phase={GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} outcome=SUCCESS " +
                    $"finalThreat={_threat:F2}/{GetEffectiveFailureThreshold()} kills={_killCountThisPhase} powerUps={_powerUpCountThisPhase}");
                if (_isBossPhase)
                    DinosWaveBattleLogCollator.EndRun(Game.Id, MetaGame.Id, "WIN");
            }

            // Only the boss phase succeeding is the actual end of the run - every wave phase's own
            // SucceedMode is just a phase->break transition and must NOT clear the persisted threat.
            if (_isBossPhase)
                _persistedThreatByMetaGameId.Remove(PersistedThreatKey);

            MetaGame.ScheduleActivateGameMode(_proto.NextMode);
        }

        private void FailMode()
        {
            if (_modeEnded) return;
            _modeEnded = true;
            if (MetaGame.Debug)
                Logger.Debug($"FailMode(): {GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} threat={_threat:F2} -> mode index {_proto.FailMode}");
            SendUINotification(_proto.FailUINotification);

            if (IsWaveBattleLoggingEnabled)
            {
                LogWaveBattle($"PHASE_END phase={GameDatabase.GetFormattedPrototypeName(_proto.DataRef)} outcome=FAIL " +
                    $"finalThreat={_threat:F2}/{GetEffectiveFailureThreshold()} kills={_killCountThisPhase} powerUps={_powerUpCountThisPhase}");
                DinosWaveBattleLogCollator.EndRun(Game.Id, MetaGame.Id, "LOSS");
            }

            _persistedThreatByMetaGameId.Remove(PersistedThreatKey);
            MetaGame.ScheduleActivateGameMode(_proto.FailMode);
        }

        #endregion

        #region Helpers

        private Player GetRandomPlayer()
        {
            using var playersHandle = ListPool<Player>.Instance.Get(out List<Player> players);
            foreach (Player player in MetaGame.Players)
                players.Add(player);

            if (players.Count == 0) return null;
            return players[Game.Random.Next(0, players.Count)];
        }

        private void EjectPlayer(Player player)
        {
            using Teleporter teleporter = ObjectPoolManager.Instance.Get<Teleporter>();
            teleporter.Initialize(player, TeleportContextEnum.TeleportContext_MetaGame);
            teleporter.TeleportToTarget(_proto.DeathRegionTarget);
        }

        // SendPvEInstanceRegionScoreUpdate alone doesn't render anything - every working example
        // (MetaStateTrackRegionScore, MetaStateLimitPlayerDeaths) pairs it with a UIWidgetGenericFraction
        // via MetaGame.GetWidget<T>(widgetRef)?.SetCount(current, total), which is the piece that
        // actually puts a bar on screen. GetWidget<T> lazily creates/registers the widget on first call,
        // so reusing an existing Live widget prototype (Danger Room's generic counter bar) is enough -
        // no new widget prototype of our own needed.
        private void UpdateThreatMeter()
        {
            _persistedThreatByMetaGameId[PersistedThreatKey] = _threat;

            SendPvEInstanceRegionScoreUpdate((int)MathF.Round(_threat), null);

            var widget = MetaGame.GetWidget<UIWidgetGenericFraction>(ThreatMeterWidgetRef);
            widget?.SetCount((int)MathF.Round(_threat), (int)GetEffectiveFailureThreshold());
        }

        // Raw player-relative offsets can land inside building geometry - those spawns are then
        // unreachable, so they can never be killed, which permanently stalls their share of the
        // threat reduction. Mirrors IncursionManager.ChooseSpawnPosition: check the desired
        // point first, then expand a pathable search ring around it until a clear spot is found.
        private Vector3 ChooseValidSpawnPosition(PopulationObjectPrototype popObj, Vector3 desiredPosition)
        {
            var entityProto = GetRepresentativeEntityProto(popObj);
            if (entityProto == null) return desiredPosition;

            PathFlags pathFlags = Region.GetPathFlagsForEntity(entityProto);
            Bounds bounds = new(entityProto.Bounds, desiredPosition);
            var posFlags = PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.CanPathTo;
            var blockFlags = BlockingCheckFlags.CheckSpawns;

            if (Region.IsLocationClear(ref bounds, pathFlags, posFlags, blockFlags))
                return bounds.Center;

            const float searchStep = 200f;
            float minDistance = 0f;
            float maxDistance = 0f;
            while (true)
            {
                maxDistance += searchStep;
                if (maxDistance > MaxSpawnSearchDistance) return desiredPosition;

                if (Region.ChooseRandomPositionNearPoint(ref bounds, pathFlags, posFlags, blockFlags,
                    minDistance, maxDistance, out Vector3 candidate))
                {
                    return candidate;
                }

                minDistance = maxDistance;
            }
        }

        private static WorldEntityPrototype GetRepresentativeEntityProto(PopulationObjectPrototype popObj)
        {
            using var entitiesHandle = HashSetPool<PrototypeId>.Instance.Get(out HashSet<PrototypeId> entities);
            popObj.GetContainedEntities(entities);
            foreach (var entityRef in entities)
            {
                var proto = GameDatabase.GetPrototype<WorldEntityPrototype>(entityRef);
                if (proto != null) return proto;
            }
            return null;
        }

        private static void ActivatePowerForEntity(PrototypeId powerRef, WorldEntity entity)
        {
            PowerIndexProperties indexProps = new(0, entity.CharacterLevel, entity.CombatLevel);
            entity.AssignPower(powerRef, indexProps);
            var position = entity.RegionLocation.Position;
            var powerSettings = new PowerActivationSettings(entity.Id, Vector3.Zero, position)
            { Flags = PowerActivationSettingsFlags.NotifyOwner };
            entity.ActivatePower(powerRef, ref powerSettings);
        }

        private void DestroyTrackedEntities(HashSet<ulong> tracked)
        {
            if (Game == null) return;
            var manager = Game.EntityManager;
            foreach (var id in tracked)
            {
                var entity = manager.GetEntity<WorldEntity>(id);
                if (entity == null) continue;

                if (_proto.WaveOnDespawnPower != PrototypeId.Invalid)
                    ActivatePowerForEntity(_proto.WaveOnDespawnPower, entity);

                entity.Destroy();
            }
            tracked.Clear();
        }

        private void ScheduleEvent<T>(EventPointer<T> eventPointer, int timeMs) where T : CallMethodEvent<PvEScaleGameMode>, new()
        {
            if (timeMs <= 0) return;
            var scheduler = Game?.GameEventScheduler;
            if (scheduler == null) return;

            TimeSpan timeOffset = TimeSpan.FromMilliseconds(timeMs);
            if (eventPointer.IsValid) return;

            scheduler.ScheduleEvent(eventPointer, timeOffset, _pendingEvents);
            eventPointer.Get().Initialize(this);
        }

        #endregion

        #region Events

        public class WaveTickEvent : CallMethodEvent<PvEScaleGameMode>
        {
            protected override CallbackDelegate GetCallback() => gameMode => gameMode.ScheduledWaveTick();
        }

        public class PhaseTimeoutEvent : CallMethodEvent<PvEScaleGameMode>
        {
            protected override CallbackDelegate GetCallback() => gameMode => gameMode.ScheduledPhaseTimeout();
        }

        public class BossSpawnEvent : CallMethodEvent<PvEScaleGameMode>
        {
            protected override CallbackDelegate GetCallback() => gameMode => gameMode.ScheduledBossSpawn();
        }

        public class PowerUpSpawnEvent : CallMethodEvent<PvEScaleGameMode>
        {
            protected override CallbackDelegate GetCallback() => gameMode => gameMode.ScheduledPowerUpSpawn();
        }

        #endregion
    }
}
