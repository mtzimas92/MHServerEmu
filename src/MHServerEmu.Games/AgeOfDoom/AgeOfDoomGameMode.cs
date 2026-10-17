using Gazillion;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.Events.Templates;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.MetaGames;
using MHServerEmu.Games.MetaGames.GameModes;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.OmegaTierItems;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI.Widgets;

namespace MHServerEmu.Games.AgeOfDoom;

public sealed class AgeOfDoomGameMode : MetaGameMode
{
    private static readonly PrototypeId FallbackObjectiveWidgetRef = (PrototypeId)1488507445230442250;
    private enum Stage { Cinematic, Incursion, Council, MidtownDooms, DoomAssault, Complete, Failed }
    private enum DoomAssault { Solo, Red, Blue }
    private enum CinematicTransition { None, BeginIncursion, BeginCouncil, BeginMidtownDooms, Complete }

    private static readonly Logger Logger = LogManager.CreateLogger();
    private static readonly AgeOfDoomTuning Tuning = AgeOfDoomTuning.Load();
    private static readonly Dictionary<(ulong, ulong), RunState> Runs = new();

    private readonly Event<EntityDeadGameEvent>.Action _entityDeadAction;
    private readonly Event<CinematicFinishedGameEvent>.Action _cinematicFinishedAction;
    private readonly EventPointer<TickEvent> _tickEvent = new();
    private readonly EventPointer<TimeoutEvent> _timeoutEvent = new();
    private readonly EventPointer<CinematicTimeoutEvent> _cinematicTimeoutEvent = new();
    private RunState _run;

    public AgeOfDoomGameMode(MetaGame metaGame, MetaGameModePrototype proto) : base(metaGame, proto)
    {
        _entityDeadAction = OnEntityDead;
        _cinematicFinishedAction = OnCinematicFinished;
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
            Region.CinematicFinishedEvent.RemoveAction(_cinematicFinishedAction);
            Region.CinematicFinishedEvent.AddActionBack(_cinematicFinishedAction);
            RefreshUI();
            if (_run.CurrentStage == Stage.Cinematic)
            {
                if (_run.CinematicStarted)
                    ScheduleCinematicTimeout();
                else
                    StartPendingCinematic();
            }
            ScheduleTick();
            return;
        }

        _run = new() { Started = true, CurrentStage = Stage.Cinematic };
        Runs[key] = _run;
        Region.EntityDeadEvent.AddActionBack(_entityDeadAction);
        Region.CinematicFinishedEvent.AddActionBack(_cinematicFinishedAction);
        SetModeText(Prototype.Name);
        BeginCinematicTransition(0, CinematicTransition.BeginIncursion);
        ScheduleTick();
    }

    public override void OnAddPlayer(Player player)
    {
        if (_run?.CurrentStage == Stage.Cinematic && _run.CinematicStarted == false)
            StartPendingCinematic();
        RefreshUI();
    }

    public override void OnDeactivate()
    {
        Region?.EntityDeadEvent.RemoveAction(_entityDeadAction);
        Region?.CinematicFinishedEvent.RemoveAction(_cinematicFinishedAction);
        Game?.GameEventScheduler?.CancelAllEvents(_pendingEvents);
    }

    public override void OnDestroy()
    {
        Region?.EntityDeadEvent.RemoveAction(_entityDeadAction);
        Region?.CinematicFinishedEvent.RemoveAction(_cinematicFinishedAction);
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
                switch (_run.StageAfterDoom)
                {
                    case Stage.Council:
                        BeginCinematicTransition(1, CinematicTransition.BeginCouncil);
                        break;
                    case Stage.MidtownDooms:
                        BeginCinematicTransition(2, CinematicTransition.BeginMidtownDooms);
                        break;
                    case Stage.Complete:
                        BeginCinematicTransition(3, CinematicTransition.Complete);
                        break;
                }
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
    }

    private void BeginKillStage(int killsPerPlayer)
    {
        _run.CurrentStage = Stage.Incursion;
        _run.Required = Math.Max(1, killsPerPlayer * CountPlayers());
        SpawnWave();
        RefreshUI();
    }

    private void BeginCinematicTransition(int cinematicIndex, CinematicTransition transition)
    {
        _run.CurrentStage = Stage.Cinematic;
        _run.PendingTransition = transition;
        _run.PendingCinematicPlayers.Clear();
        _run.PendingCinematicRef = PrototypeId.Invalid;
        _run.CinematicStarted = false;
        _run.Progress = 0;
        _run.Required = 1;

        if (cinematicIndex < 0 || cinematicIndex >= Tuning.CinematicPrototypes.Count)
        {
            CompleteCinematicTransition();
            return;
        }

        string cinematicName = Tuning.CinematicPrototypes[cinematicIndex];
        PrototypeId cinematicRef = GameDatabase.GetPrototypeRefByName(cinematicName);
        if (cinematicRef.As<FullscreenMoviePrototype>() == null)
        {
            Logger.Warn($"[AgeOfDoom] invalid cinematic prototype '{cinematicName}'.");
            CompleteCinematicTransition();
            return;
        }

        _run.PendingCinematicRef = cinematicRef;
        StartPendingCinematic();
    }

    private void StartPendingCinematic()
    {
        if (_run?.CurrentStage != Stage.Cinematic || _run.CinematicStarted ||
            _run.PendingCinematicRef == PrototypeId.Invalid)
            return;

        foreach (Player player in MetaGame.Players)
        {
            if (player?.CurrentAvatar?.IsInWorld != true || player.CurrentAvatar.Region != Region)
                continue;

            _run.PendingCinematicPlayers.Add(player.Id);
            player.QueueFullscreenMovie(_run.PendingCinematicRef);
        }

        if (_run.PendingCinematicPlayers.Count == 0)
            return;

        _run.CinematicStarted = true;
        ScheduleCinematicTimeout();
    }

    private void OnCinematicFinished(in CinematicFinishedGameEvent evt)
    {
        if (_run?.CurrentStage != Stage.Cinematic || evt.MovieRef != _run.PendingCinematicRef || evt.Player == null)
            return;

        if (_run.PendingCinematicPlayers.Remove(evt.Player.Id) && _run.PendingCinematicPlayers.Count == 0)
            CompleteCinematicTransition();
    }

    private void CompleteCinematicTransition()
    {
        if (_run == null || _run.CurrentStage != Stage.Cinematic)
            return;

        if (_cinematicTimeoutEvent.IsValid)
            Game?.GameEventScheduler?.CancelEvent(_cinematicTimeoutEvent);

        CinematicTransition transition = _run.PendingTransition;
        _run.PendingTransition = CinematicTransition.None;
        _run.PendingCinematicRef = PrototypeId.Invalid;
        _run.CinematicStarted = false;
        _run.PendingCinematicPlayers.Clear();

        switch (transition)
        {
            case CinematicTransition.BeginIncursion:
                StartRunTimer();
                BeginKillStage(Tuning.IncursionKillsPerPlayer);
                break;
            case CinematicTransition.BeginCouncil:
                BeginStage(Stage.Council);
                break;
            case CinematicTransition.BeginMidtownDooms:
                BeginStage(Stage.MidtownDooms);
                break;
            case CinematicTransition.Complete:
                CompleteRun();
                break;
        }
    }

    private void StartRunTimer()
    {
        if (_run.TimerStarted || Tuning.TimeLimitMinutes <= 0)
            return;

        _run.TimerStarted = true;
        TimeSpan duration = TimeSpan.FromMinutes(Tuning.TimeLimitMinutes);
        SendStartPvPTimer(duration, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        Schedule(_timeoutEvent, duration);
    }

    private void ScheduleCinematicTimeout() =>
        Schedule(_cinematicTimeoutEvent, TimeSpan.FromSeconds(Math.Max(1, Tuning.CinematicTimeoutSeconds)));

    private void ScheduledCinematicTimeout()
    {
        _cinematicTimeoutEvent.Set(null);
        CompleteCinematicTransition();
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
        if (_run.CurrentStage == Stage.Cinematic)
        {
            if (_run.CinematicStarted == false)
                StartPendingCinematic();
            _run.PendingCinematicPlayers.RemoveWhere(id =>
            {
                Player player = Game.EntityManager.GetEntity<Player>(id);
                return player?.CurrentAvatar?.IsInWorld != true || player.CurrentAvatar.Region != Region;
            });
            if (_run.CinematicStarted && _run.PendingCinematicPlayers.Count == 0)
                CompleteCinematicTransition();
            if (_run.CurrentStage == Stage.Cinematic)
            {
                ScheduleTick();
                return;
            }
        }
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
        if (_run == null || _run.IsFinished)
            return;

        _run.CurrentStage = Stage.Complete;
        RefreshUI();
        foreach (Player player in MetaGame.Players)
        {
            player.OnScoringEvent(new(ScoringEventType.MetaGameModeComplete, Prototype));
            GrantCompletionRewards(player);
        }
        _run.RewardsGranted = true;
    }

    private void GrantCompletionRewards(Player player)
    {
        Avatar avatar = player?.CurrentAvatar;
        if (_run.RewardsGranted || avatar?.IsInWorld != true || avatar.Region != Region)
            return;

        int groundRecipientId = 1;
        foreach (AgeOfDoomCompletionRewardTuning reward in Tuning.CompletionRewards)
        {
            if (reward?.Enabled != true || Game.Random.NextFloat() * 100f >= reward.ChancePercent)
                continue;

            if (string.IsNullOrWhiteSpace(reward.LootTablePrototype) == false)
            {
                GrantCompletionLootTable(player, avatar, reward, ref groundRecipientId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(reward.ItemPrototype) == false)
                GrantCompletionItem(player, avatar, reward, ref groundRecipientId);
        }
    }

    private void GrantCompletionLootTable(
        Player player,
        Avatar avatar,
        AgeOfDoomCompletionRewardTuning reward,
        ref int groundRecipientId)
    {
        PrototypeId lootTableRef = GameDatabase.GetPrototypeRefByName(reward.LootTablePrototype);
        if (lootTableRef.As<LootTablePrototype>() == null)
        {
            Logger.Warn($"[AgeOfDoom] completion reward '{reward.Id}' has invalid loot table '{reward.LootTablePrototype}'.");
            return;
        }

        int itemLevel = reward.ItemLevel > 0 ? reward.ItemLevel : avatar.CharacterLevel;
        for (int roll = 0; roll < reward.Rolls; roll++)
        {
            using var inputSettingsHandle = LootInputSettingsPool.Get(out LootInputSettings inputSettings);
            inputSettings.Initialize(LootContext.Drop, player, avatar, itemLevel);
            if (reward.Delivery == "inventory")
                Game.LootManager.GiveLootFromTable(lootTableRef, inputSettings);
            else
                Game.LootManager.SpawnLootFromTable(lootTableRef, inputSettings, groundRecipientId++);
        }
    }

    private void GrantCompletionItem(
        Player player,
        Avatar avatar,
        AgeOfDoomCompletionRewardTuning reward,
        ref int groundRecipientId)
    {
        PrototypeId itemRef = GameDatabase.GetPrototypeRefByName(reward.ItemPrototype);
        ItemPrototype itemProto = itemRef.As<ItemPrototype>();
        if (itemProto == null)
        {
            Logger.Warn($"[AgeOfDoom] completion reward '{reward.Id}' has invalid item '{reward.ItemPrototype}'.");
            return;
        }

        PrototypeId rarityRef = string.IsNullOrWhiteSpace(reward.RarityPrototype)
            ? PrototypeId.Invalid
            : GameDatabase.GetPrototypeRefByName(reward.RarityPrototype);
        if (string.IsNullOrWhiteSpace(reward.RarityPrototype) == false && rarityRef == PrototypeId.Invalid)
        {
            Logger.Warn($"[AgeOfDoom] completion reward '{reward.Id}' has invalid rarity '{reward.RarityPrototype}'.");
            return;
        }

        int itemLevel = reward.ItemLevel > 0 ? reward.ItemLevel : avatar.CharacterLevel;
        int maximumStack = Math.Max(itemProto.StackSettings?.MaxStacks ?? 1, 1);
        int remaining = reward.Quantity;
        using var summaryHandle = LootResultSummaryPool.Get(out LootResultSummary summary);
        while (remaining > 0)
        {
            ItemSpec itemSpec = rarityRef != PrototypeId.Invalid
                ? OmegaTierItemFactory.CreateItemSpec(Game, itemRef, rarityRef, LootContext.Drop, player, itemLevel)
                : Game.LootManager.CreateItemSpec(itemRef, LootContext.Drop, player, itemLevel);
            if (itemSpec == null)
            {
                Logger.Warn($"[AgeOfDoom] completion reward '{reward.Id}' failed to create item '{reward.ItemPrototype}'.");
                return;
            }

            itemSpec.StackCount = Math.Min(remaining, maximumStack);
            remaining -= itemSpec.StackCount;
            summary.Add(new LootResult(itemSpec));
        }

        if (reward.Delivery == "inventory")
        {
            Game.LootManager.GiveLootFromSummary(summary, player, PrototypeId.Invalid);
            return;
        }

        using var inputSettingsHandle = LootInputSettingsPool.Get(out LootInputSettings inputSettings);
        inputSettings.Initialize(LootContext.Drop, player, avatar, itemLevel);
        Game.LootManager.SpawnLootFromSummary(summary, inputSettings, groundRecipientId++);
    }

    private void FailRun()
    {
        if (_run == null || _run.IsFinished) return;
        _run.CurrentStage = Stage.Failed;
        foreach (ulong id in _run.Entities) Game.EntityManager.GetEntity<WorldEntity>(id)?.Destroy();
        _run.Entities.Clear();
        RefreshUI();
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
        public bool TimerStarted;
        public Stage CurrentStage;
        public int Progress;
        public int Required;
        public int DoomPhaseIndex;
        public DoomAssault Assault;
        public Stage StageAfterDoom;
        public CinematicTransition PendingTransition;
        public PrototypeId PendingCinematicRef;
        public bool CinematicStarted;
        public readonly HashSet<ulong> PendingCinematicPlayers = new();
        public readonly HashSet<ulong> Entities = new();
        public readonly Dictionary<ulong, int> DoomTargetSlots = new();
        public readonly HashSet<int> CompletedDoomSlots = new();
        public bool RewardsGranted;
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

    public sealed class CinematicTimeoutEvent : CallMethodEvent<AgeOfDoomGameMode>
    {
        protected override CallbackDelegate GetCallback() => mode => mode.ScheduledCinematicTimeout();
    }
}
