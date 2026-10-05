using Gazillion;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.Events.Templates;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.MetaGames;
using MHServerEmu.Games.MetaGames.GameModes;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.AgeOfDoom;

public sealed class AgeOfDoomGameMode : MetaGameMode
{
    private static readonly PrototypeId FallbackObjectiveWidgetRef = (PrototypeId)1488507445230442250;
    private enum Stage { Incursion, Council, MidtownDooms, DoomAssault, Complete, Failed }
    private enum DoomAssault { Solo, Red, Blue }

    private static readonly Logger Logger = LogManager.CreateLogger();
    private static readonly AgeOfDoomTuning Tuning = AgeOfDoomTuning.Load();
    private static readonly Dictionary<(ulong, ulong), RunState> Runs = new();

    private readonly Event<EntityDeadGameEvent>.Action _entityDeadAction;
    private readonly EventPointer<TickEvent> _tickEvent = new();
    private readonly EventPointer<TimeoutEvent> _timeoutEvent = new();
    private RunState _run;

    public AgeOfDoomGameMode(MetaGame metaGame, MetaGameModePrototype proto) : base(metaGame, proto)
    {
        _entityDeadAction = OnEntityDead;
    }

    public static bool ShouldReplace(MetaGame metaGame) =>
        Tuning.Enabled && metaGame?.PrototypeDataRef == (PrototypeId)Tuning.MetaGamePrototypeId;

    public override void OnActivate()
    {
        // Do not call the native mode activation. Its ApplyStates/EventHandler chain is the
        // Age of Ultron mission sequencer that this mode intentionally replaces.
        _startTime = Game.CurrentTime;
        var key = (Game.Id, MetaGame.Id);
        if (Runs.TryGetValue(key, out _run) && _run.Started)
        {
            Region.EntityDeadEvent.RemoveAction(_entityDeadAction);
            Region.EntityDeadEvent.AddActionBack(_entityDeadAction);
            RefreshUI();
            ScheduleTick();
            return;
        }

        _run = new() { Started = true, CurrentStage = Stage.Incursion };
        Runs[key] = _run;
        Region.EntityDeadEvent.AddActionBack(_entityDeadAction);
        SetModeText(Prototype.Name);
        BeginKillStage(Tuning.IncursionKillsPerPlayer);

        if (Tuning.TimeLimitMinutes > 0)
        {
            TimeSpan duration = TimeSpan.FromMinutes(Tuning.TimeLimitMinutes);
            SendStartPvPTimer(duration, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            Schedule(_timeoutEvent, duration);
        }

        Logger.Info($"[AgeOfDoom] run-start gameId=0x{Game.Id:X} metaGameId=0x{MetaGame.Id:X} players={CountPlayers()}");
        ScheduleTick();
    }

    public override void OnAddPlayer(Player player)
    {
        RefreshUI();
    }

    public override void OnDeactivate()
    {
        Region?.EntityDeadEvent.RemoveAction(_entityDeadAction);
        Game?.GameEventScheduler?.CancelAllEvents(_pendingEvents);
    }

    public override void OnDestroy()
    {
        Region?.EntityDeadEvent.RemoveAction(_entityDeadAction);
        Runs.Remove((Game.Id, MetaGame.Id));
        base.OnDestroy();
    }

    private void OnEntityDead(in EntityDeadGameEvent evt)
    {
        if (_run == null || _run.IsFinished || evt.Defender == null || _run.Entities.Remove(evt.Defender.Id) == false)
            return;

        if (_run.CurrentStage == Stage.DoomAssault && _run.DoomTargetSlots.Remove(evt.Defender.Id, out int slot))
            _run.CompletedDoomSlots.Add(slot);
        _run.Progress++;
        RefreshUI();
        if (_run.Progress >= _run.Required)
            AdvanceStage();
    }

    private void AdvanceStage()
    {
        if (_run.CurrentStage == Stage.DoomAssault)
        {
            if (_run.DoomPhaseIndex + 1 < Tuning.DoomPhasePrototypes.Count)
            {
                _run.DoomPhaseIndex++;
                _run.Progress = 0;
                _run.Entities.Clear();
                _run.DoomTargetSlots.Clear();
                _run.CompletedDoomSlots.Clear();
                SpawnCurrentDoomPhase();
            }
            else
            {
                BeginStage(_run.StageAfterDoom);
            }

            RefreshUI();
            return;
        }

        switch (_run.CurrentStage)
        {
            case Stage.Incursion:
                BeginDoomAssault(DoomAssault.Solo, Stage.Council);
                break;
            case Stage.Council:
                BeginDoomAssault(DoomAssault.Red, Stage.MidtownDooms);
                break;
            case Stage.MidtownDooms:
                BeginDoomAssault(DoomAssault.Blue, Stage.Complete);
                break;
        }

        Logger.Info($"[AgeOfDoom] stage-start gameId=0x{Game.Id:X} stage={_run.CurrentStage} required={_run.Required}");
        RefreshUI();
    }

    private void BeginStage(Stage stage)
    {
        _run.Progress = 0;
        _run.Entities.Clear();

        switch (stage)
        {
            case Stage.Council:
                _run.CurrentStage = Stage.Council;
                _run.Required = Math.Max(1, Tuning.CouncilBosses);
                SpawnCouncil();
                break;
            case Stage.MidtownDooms:
                _run.CurrentStage = Stage.MidtownDooms;
                _run.Required = Math.Max(1, Tuning.MidtownDoomBosses);
                SpawnMidtownDooms();
                break;
            case Stage.Complete:
                CompleteRun();
                break;
        }
    }

    private void BeginDoomAssault(DoomAssault assault, Stage stageAfterDoom)
    {
        _run.CurrentStage = Stage.DoomAssault;
        _run.StageAfterDoom = stageAfterDoom;
        _run.Assault = assault;
        _run.DoomPhaseIndex = 0;
        _run.Progress = 0;
        _run.Required = assault == DoomAssault.Solo ? 1 : 2;
        _run.Entities.Clear();
        _run.DoomTargetSlots.Clear();
        _run.CompletedDoomSlots.Clear();
        SpawnCurrentDoomPhase();
        Logger.Info(
            $"[AgeOfDoom] doom-assault-start gameId=0x{Game.Id:X} assault={assault} count={_run.Required} " +
            $"phases={Tuning.DoomPhasePrototypes.Count} nextStage={stageAfterDoom}");
    }

    private void BeginKillStage(int killsPerPlayer)
    {
        _run.Required = Math.Max(1, killsPerPlayer * CountPlayers());
        SpawnWave();
        RefreshUI();
    }

    private void ScheduledTick()
    {
        _tickEvent.Set(null);
        if (_run == null || _run.IsFinished) return;

        _run.Entities.RemoveWhere(id =>
        {
            if (Game.EntityManager.GetEntity<WorldEntity>(id) != null)
                return false;
            _run.DoomTargetSlots.Remove(id);
            return true;
        });
        if (_run.CurrentStage == Stage.DoomAssault)
        {
            SpawnCurrentDoomPhase();
            EnsureDoomTargets();
        }

        if (_run.CurrentStage == Stage.Incursion)
        {
            int cap = Math.Max(3, Tuning.MaximumLivingEnemiesPerPlayer * CountPlayers());
            if (_run.Entities.Count < cap) SpawnWave();
        }
        else if (_run.Entities.Count == 0)
        {
            if (_run.CurrentStage == Stage.Council) SpawnCouncil();
            if (_run.CurrentStage == Stage.MidtownDooms) SpawnMidtownDooms();
        }
        ScheduleTick();
    }

    private void SpawnWave()
    {
        IReadOnlyList<string> pool = Tuning.MinionPrototypes;
        int count = CountPlayers() * 4;
        for (int i = 0; i < count && _run.Progress + _run.Entities.Count < _run.Required; i++) SpawnRandom(pool);
    }

    private void SpawnCouncil()
    {
        int missing = Math.Max(0, _run.Required - _run.Progress - _run.Entities.Count);
        for (int i = 0; i < missing; i++) SpawnRandom(Tuning.CouncilBossPrototypes);
    }

    private void SpawnMidtownDooms()
    {
        int missing = Math.Max(0, _run.Required - _run.Progress - _run.Entities.Count);
        for (int i = 0; i < missing; i++) SpawnAgent(Tuning.MidtownDoomPrototype);
    }

    private void SpawnCurrentDoomPhase()
    {
        if (_run.DoomPhaseIndex >= Tuning.DoomPhasePrototypes.Count)
        {
            BeginStage(_run.StageAfterDoom);
            return;
        }

        for (int slot = 0; slot < _run.Required; slot++)
        {
            if (_run.CompletedDoomSlots.Contains(slot) || _run.DoomTargetSlots.ContainsValue(slot))
                continue;

            string prototype = slot == 0
                ? Tuning.DoomPhasePrototypes[_run.DoomPhaseIndex]
                : Tuning.DoomPhasePrototypes[_run.Assault == DoomAssault.Red ? 1 : 2];
            SpawnAgent(prototype, slot);
        }
    }

    private void SpawnRandom(IReadOnlyList<string> pool)
    {
        if (pool?.Count > 0) SpawnAgent(pool[Game.Random.Next(0, pool.Count)]);
    }

    private bool SpawnAgent(string name, int doomSlot = -1)
    {
        AgentPrototype proto = GameDatabase.GetPrototypeRefByName(name).As<AgentPrototype>();
        Avatar anchor = GetRandomAvatar();
        if (proto == null || anchor == null)
        {
            Logger.Warn($"[AgeOfDoom] spawn-failed prototype={name}");
            return false;
        }

        Bounds spawnBounds = new();
        spawnBounds.InitializeFromPrototype(proto.Bounds);
        spawnBounds.Center = anchor.RegionLocation.Position;
        PositionCheckFlags positionFlags = PositionCheckFlags.CanBeBlockedEntity | PositionCheckFlags.CanPathTo;
        if (Region.ChooseRandomPositionNearPoint(
                ref spawnBounds,
                anchor.Locomotor.PathFlags,
                positionFlags,
                BlockingCheckFlags.CheckSpawns,
                Tuning.SpawnRadius * 0.35f,
                Tuning.SpawnRadius,
                out Vector3 position,
                maxPositionTests: 64) == false)
        {
            return false;
        }

        Cell cell = Region.GetCellAtPosition(position);
        if (cell == null)
            return false;

        position = RegionLocation.ProjectToFloor(Region, cell, position);
        if (proto.Bounds != null)
            position.Z += proto.Bounds.GetBoundHalfHeight();

        using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
        settings.EntityRef = proto.DataRef;
        settings.RegionId = Region.Id;
        settings.Cell = cell;
        settings.Position = position;
        settings.Orientation = Orientation.FromDeltaVector2D(anchor.RegionLocation.Position - position);
        settings.IsPopulation = true;
        using var propsHandle = PropertyCollectionPool.Get(out PropertyCollection props);
        int level = cell.Area.GetCharacterLevel(proto);
        props[PropertyEnum.CharacterLevel] = level;
        props[PropertyEnum.CombatLevel] = level;
        props[PropertyEnum.DifficultyTier] = Region.DifficultyTierRef;
        props[PropertyEnum.Rank] = proto.Rank?.DataRef ?? PrototypeId.Invalid;
        props[PropertyEnum.MissionXEncounterHostilityOk] = true;
        settings.Properties = props;

        Agent agent = Game.EntityManager.CreateEntity(settings) as Agent;
        if (agent?.IsInWorld != true)
        {
            agent?.Destroy();
            return false;
        }
        _run.Entities.Add(agent.Id);
        if (doomSlot >= 0)
            _run.DoomTargetSlots.Add(agent.Id, doomSlot);
        MythicRiftStandaloneBossFixups.Apply(agent, allowMissingAffixSettingsFallback: true);
        if (_run.CurrentStage is Stage.DoomAssault or Stage.MidtownDooms or Stage.Council)
            AssignNearestAvatarTarget(agent);
        MetaGame.DiscoverEntity(agent);
        if (_run.CurrentStage is Stage.Council or Stage.MidtownDooms or Stage.DoomAssault)
        {
            Logger.Info(
                $"[AgeOfDoom] boss-spawned gameId=0x{Game.Id:X} stage={_run.CurrentStage} " +
                $"prototype={name} entityId=0x{agent.Id:X} position={agent.RegionLocation.Position}");
        }
        return true;
    }

    private void EnsureDoomTargets()
    {
        foreach (ulong entityId in _run.Entities)
        {
            Agent doom = Game.EntityManager.GetEntity<Agent>(entityId);
            if (doom?.IsInWorld == true && doom.IsDead == false && doom.AIController?.TargetEntity == null)
                AssignNearestAvatarTarget(doom);
        }
    }

    private void AssignNearestAvatarTarget(Agent agent)
    {
        Avatar nearest = null;
        float nearestDistanceSq = float.MaxValue;
        foreach (Player player in MetaGame.Players)
        {
            Avatar avatar = player?.CurrentAvatar;
            if (avatar?.IsInWorld != true || avatar.IsDead)
                continue;

            float distanceSq = Vector3.DistanceSquared(agent.RegionLocation.Position, avatar.RegionLocation.Position);
            if (distanceSq >= nearestDistanceSq)
                continue;

            nearest = avatar;
            nearestDistanceSq = distanceSq;
        }

        if (nearest == null)
            return;

        agent.SetDormant(false);
        agent.AIController?.SetTargetEntity(nearest);
    }

    private void CompleteRun()
    {
        _run.CurrentStage = Stage.Complete;
        RefreshUI();
        foreach (Player player in MetaGame.Players)
            player.OnScoringEvent(new(ScoringEventType.MetaGameModeComplete, Prototype));
        Logger.Info($"[AgeOfDoom] run-complete gameId=0x{Game.Id:X} metaGameId=0x{MetaGame.Id:X}");
    }

    private void FailRun()
    {
        if (_run == null || _run.IsFinished) return;
        _run.CurrentStage = Stage.Failed;
        foreach (ulong id in _run.Entities) Game.EntityManager.GetEntity<WorldEntity>(id)?.Destroy();
        _run.Entities.Clear();
        RefreshUI();
        Logger.Info($"[AgeOfDoom] run-failed gameId=0x{Game.Id:X} reason=timeout");
    }

    private Avatar GetRandomAvatar()
    {
        using var handle = ListPool<Avatar>.Get(out List<Avatar> avatars);
        foreach (Player player in MetaGame.Players)
            if (player?.CurrentAvatar?.IsInWorld == true) avatars.Add(player.CurrentAvatar);
        return avatars.Count > 0 ? avatars[Game.Random.Next(0, avatars.Count)] : null;
    }

    private int CountPlayers()
    {
        int count = 0;
        foreach (Player player in MetaGame.Players)
            if (player?.CurrentAvatar?.IsInWorld == true) count++;
        return Math.Max(1, count);
    }

    private void RefreshUI()
    {
        if (_run == null) return;
        PrototypeId widgetRef = GameDatabase.GetPrototypeRefByName(Tuning.ObjectiveWidgetPrototype);
        if (widgetRef == PrototypeId.Invalid)
            widgetRef = FallbackObjectiveWidgetRef;
        MetaGame.GetWidget<UIWidgetGenericFraction>(widgetRef)?.SetCount(_run.Progress, Math.Max(1, _run.Required));
    }

    private void ScheduleTick() => Schedule(_tickEvent, TimeSpan.FromSeconds(Math.Max(1, Tuning.SpawnIntervalSeconds)));

    private void Schedule<T>(EventPointer<T> pointer, TimeSpan delay) where T : CallMethodEvent<AgeOfDoomGameMode>, new()
    {
        if (pointer.IsValid || Game?.GameEventScheduler == null) return;
        Game.GameEventScheduler.ScheduleEvent(pointer, delay, _pendingEvents);
        pointer.Get().Initialize(this);
    }

    private sealed class RunState
    {
        public bool Started;
        public Stage CurrentStage;
        public int Progress;
        public int Required;
        public int DoomPhaseIndex;
        public DoomAssault Assault;
        public Stage StageAfterDoom;
        public readonly HashSet<ulong> Entities = new();
        public readonly Dictionary<ulong, int> DoomTargetSlots = new();
        public readonly HashSet<int> CompletedDoomSlots = new();
        public bool IsFinished => CurrentStage is Stage.Complete or Stage.Failed;
    }

    public sealed class TickEvent : CallMethodEvent<AgeOfDoomGameMode>
    {
        protected override CallbackDelegate GetCallback() => mode => mode.ScheduledTick();
    }

    public sealed class TimeoutEvent : CallMethodEvent<AgeOfDoomGameMode>
    {
        protected override CallbackDelegate GetCallback() => mode => mode.FailRun();
    }
}
