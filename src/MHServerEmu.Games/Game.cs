﻿using System.Diagnostics;
using System.Globalization;
using Gazillion;
using Google.ProtocolBuffers;
using MHServerEmu.Core.Config;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Metrics;
using MHServerEmu.Core.Network;
using MHServerEmu.Core.System.Random;
using MHServerEmu.Core.System.Time;
using MHServerEmu.Frontend;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.LiveTuning;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.MetaGames;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games
{
    public enum GameShutdownReason
    {
        ServerShuttingDown,
        GameInstanceCrash
    }

    public partial class Game
    {
        public const string Version = "1.52.0.1700";

        [ThreadStatic]
        internal static Game Current;

        private const int TargetFrameRate = 20;
        public static readonly TimeSpan StartTime = TimeSpan.FromMilliseconds(1);
        public readonly NetStructGameOptions GameOptions;
        public readonly CustomGameOptionsConfig CustomGameOptions;

        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly Stopwatch _gameTimer = new();
        private FixedQuantumGameTime _realGameTime = new(TimeSpan.FromMilliseconds(1));
        private TimeSpan _currentGameTime = TimeSpan.FromMilliseconds(1);   // Current time in the game simulation
        private TimeSpan _lastFixedTimeUpdateProcessTime;                   // How long the last fixed update took
        private int _frameCount;

        private int _liveTuningChangeNum;

        private Thread _gameThread;

        private ulong _currentRepId;

        // Dumped ids: 0xF9E00000FA2B3EA (Lobby), 0xFF800000FA23AE9 (Tower), 0xF4A00000FA2B47D (Danger Room), 0xFCC00000FA29FE7 (Midtown)
        public ulong Id { get; }
        public bool IsRunning { get; private set; } = false;
        public bool HasBeenShutDown { get; private set; } = false;

        public GRandom Random { get; } = new();
        public PlayerConnectionManager NetworkManager { get; }
        public EventScheduler GameEventScheduler { get; private set; }
        public EntityManager EntityManager { get; }
        public RegionManager RegionManager { get; }
        public AdminCommandManager AdminCommandManager { get; }
        public LootManager LootManager { get; }

        public LiveTuningData LiveTuningData { get; private set; } = new();

        public TimeSpan FixedTimeBetweenUpdates { get; } = TimeSpan.FromMilliseconds(1000f / TargetFrameRate);
        public TimeSpan RealGameTime { get => (TimeSpan)_realGameTime; }
        public TimeSpan CurrentTime { get => GameEventScheduler != null ? GameEventScheduler.CurrentTime : _currentGameTime; }
        public ulong NumQuantumFixedTimeUpdates { get => (ulong)CurrentTime.CalcNumTimeQuantums(FixedTimeBetweenUpdates); }

        public ulong CurrentRepId { get => ++_currentRepId; }
        public Dictionary<ulong, IArchiveMessageHandler> MessageHandlerDict { get; } = new();

        public override string ToString() => $"serverGameId=0x{Id:X}";

        public Game(ulong id)
        {
            Id = id;

            // Initialize game options
            var config = ConfigManager.Instance.GetConfig<GameOptionsConfig>();
            GameOptions = config.ToProtobuf();

            CustomGameOptions = ConfigManager.Instance.GetConfig<CustomGameOptionsConfig>();

            // The game uses 16 bits of the current UTC time in seconds as the initial replication id
            _currentRepId = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond) & 0xFFFF;

            AdminCommandManager = new(this);
            NetworkManager = new(this);
            RegionManager = new();
            EntityManager = new(this);
            LootManager = new(this);

            Random = new();

            Initialize();
        }

        public bool Initialize()
        {
            bool success = true;

            _realGameTime.SetQuantumSize(FixedTimeBetweenUpdates);
            _realGameTime.UpdateToNow();
            _currentGameTime = RealGameTime;

            GameEventScheduler = new(RealGameTime, FixedTimeBetweenUpdates);

            success &= RegionManager.Initialize(this);
            success &= EntityManager.Initialize();

            LiveTuningManager.Instance.CopyLiveTuningData(LiveTuningData);
            LiveTuningData.GetLiveTuningUpdate();   // pre-generate update protobuf
            _liveTuningChangeNum = LiveTuningData.ChangeNum;

            return success;
        }

        public void Run()
        {
            // NOTE: This is now separate from the constructor so that we can have
            // a dummy game with no simulation running that we use to parse messages.
            if (IsRunning) throw new InvalidOperationException();
            IsRunning = true;

            // Initialize and start game thread
            _gameThread = new(GameLoop) { IsBackground = true, CurrentCulture = CultureInfo.InvariantCulture };
            _gameThread.Start();

            Logger.Info($"Game 0x{Id:X} started, initial replication id: {_currentRepId}");
        }

        public void Shutdown(GameShutdownReason reason)
        {
            if (IsRunning == false || HasBeenShutDown)
                return;

            Logger.Info($"Game shutdown requested. Game={this}, Reason={reason}");

            // Cancel all events
            GameEventScheduler.CancelAllEvents();

            // Clean up network manager
            NetworkManager.SendAllPendingMessages();
            foreach (PlayerConnection playerConnection in NetworkManager)
                playerConnection.Disconnect();
            NetworkManager.Update();        // We need this to process player saves (for now)

            // Clean up regions
            RegionManager.DestroyAllRegions();

            // Mark this game as shut down for the player manager
            HasBeenShutDown = true;
        }

        public void RequestShutdown()
        {
            if (IsRunning == false || HasBeenShutDown)
                return;

            IsRunning = false;
        }

        public void AddClient(FrontendClient client)
        {
            NetworkManager.AsyncAddClient(client);
        }

        public void RemoveClient(FrontendClient client)
        {
            NetworkManager.AsyncRemoveClient(client);
        }

        public void PostMessage(FrontendClient client, MessagePackage message)
        {
            NetworkManager.AsyncPostMessage(client, message);
        }

        /// <summary>
        /// Sends an <see cref="IMessage"/> over the specified <see cref="PlayerConnection"/>.
        /// </summary>
        public void SendMessage(PlayerConnection connection, IMessage message)
        {
            NetworkManager.SendMessage(connection, message);
        }

        /// <summary>
        /// Sends an <see cref="IMessage"/> to all connected players.
        /// </summary>
        public void BroadcastMessage(IMessage message)
        {
            NetworkManager.BroadcastMessage(message);
        }

        public Entity AllocateEntity(PrototypeId entityRef)
        {
            var proto = GameDatabase.GetPrototype<EntityPrototype>(entityRef);

            Entity entity;
            if (proto is SpawnerPrototype)
                entity = new Spawner(this);
            else if (proto is TransitionPrototype)
                entity = new Transition(this);
            else if (proto is AvatarPrototype)
                entity = new Avatar(this);
            else if (proto is MissilePrototype)
                entity = new Missile(this);
            else if (proto is PropPrototype) // DestructiblePropPrototype
                entity = new WorldEntity(this);
            else if (proto is AgentPrototype) // AgentTeamUpPrototype OrbPrototype SmartPropPrototype
                entity = new Agent(this);
            else if (proto is ItemPrototype) // CharacterTokenPrototype BagItemPrototype CostumePrototype CraftingIngredientPrototype
                                             // CostumeCorePrototype CraftingRecipePrototype ArmorPrototype ArtifactPrototype
                                             // LegendaryPrototype MedalPrototype RelicPrototype TeamUpGearPrototype
                                             // InventoryStashTokenPrototype EmoteTokenPrototype
                entity = new Item(this);
            else if (proto is KismetSequenceEntityPrototype)
                entity = new KismetSequenceEntity(this);
            else if (proto is HotspotPrototype)
                entity = new Hotspot(this);
            else if (proto is WorldEntityPrototype)
                entity = new WorldEntity(this);
            else if (proto is MissionMetaGamePrototype)
                entity = new MissionMetaGame(this);
            else if (proto is PvPPrototype)
                entity = new PvP(this);
            else if (proto is MetaGamePrototype) // MatchMetaGamePrototype
                entity = new MetaGame(this);
            else if (proto is PlayerPrototype)
                entity = new Player(this);
            else
                entity = new Entity(this);

            return entity;
        }

        public Power AllocatePower(PrototypeId powerProtoRef)
        {
            Type powerClassType = GameDatabase.DataDirectory.GetPrototypeClassType(powerProtoRef);

            if (powerClassType == typeof(MissilePowerPrototype))
                return new MissilePower(this, powerProtoRef);
            else if (powerClassType == typeof(SummonPowerPrototype))
                return new SummonPower(this, powerProtoRef);
            else
                return new Power(this, powerProtoRef);
        }

        public IEnumerable<Region> RegionIterator()
        {            
            foreach (Region region in RegionManager.AllRegions) 
                yield return region;
        }

        // StartTime is always a TimeSpan of 1 ms, so we can make both Game::GetTimeFromStart() and Game::GetTimeFromDelta() static

        public static long GetTimeFromStart(TimeSpan gameTime) => (long)(gameTime - StartTime).TotalMilliseconds;
        public static TimeSpan GetTimeFromDelta(long delta) => StartTime.Add(TimeSpan.FromMilliseconds(delta));

        private void GameLoop()
        {
            Current = this;
            _gameTimer.Start();

            try
            {
                while (IsRunning)
                {
                    Update();
                }

                Shutdown(GameShutdownReason.ServerShuttingDown);
            }
            catch (Exception e)
            {
                HandleGameInstanceCrash(e);
                Shutdown(GameShutdownReason.GameInstanceCrash);
            }
        }

        private void Update()
        {
            // NOTE: We process input in NetworkManager.ReceiveAllPendingMessages() outside of UpdateFixedTime(), same as the client.

            NetworkManager.Update();                            // Add / remove clients
            NetworkManager.ReceiveAllPendingMessages();         // Process input
            NetworkManager.ProcessPendingPlayerConnections();   // Load pending players

            RegionManager.Update();                             // Clean up old regions

            UpdateLiveTuning();                                 // Check if live tuning data is out of date

            UpdateFixedTime();                                  // Update simulation state
        }

        private void UpdateFixedTime()
        {
            // First we make sure enough time has passed to do another fixed time update
            _realGameTime.UpdateToNow();

            if (_currentGameTime + FixedTimeBetweenUpdates > RealGameTime)
            {
                // Thread.Sleep() can sleep for longer than specified, so rather than sleeping
                // for the entire time window between fixed updates, we do it in 1 ms intervals.
                Thread.Sleep(1);
                return;
            }

            int timesUpdated = 0;

            TimeSpan updateStartTime = _gameTimer.Elapsed;
            while (_currentGameTime + FixedTimeBetweenUpdates <= RealGameTime)
            {
                _currentGameTime += FixedTimeBetweenUpdates;

                TimeSpan stepStartTime = _gameTimer.Elapsed;

                DoFixedTimeUpdate();
                _frameCount++;
                timesUpdated++;

                _lastFixedTimeUpdateProcessTime = _gameTimer.Elapsed - stepStartTime;
                MetricsManager.Instance.RecordGamePerformanceMetric(Id, GamePerformanceMetricEnum.FrameTime, _lastFixedTimeUpdateProcessTime);

                if (_lastFixedTimeUpdateProcessTime > FixedTimeBetweenUpdates)
                    Logger.Trace($"UpdateFixedTime(): Frame took longer ({_lastFixedTimeUpdateProcessTime.TotalMilliseconds:0.00} ms) than FixedTimeBetweenUpdates ({FixedTimeBetweenUpdates.TotalMilliseconds:0.00} ms)");

                // Bail out if we have fallen behind more exceeded frame budget
                if (_gameTimer.Elapsed - updateStartTime > FixedTimeBetweenUpdates)
                    break;
            }

            // Track catch-up frames
            if (timesUpdated > 1)
            {
                Logger.Trace($"UpdateFixedTime(): Simulated {timesUpdated} frames in a single fixed update to catch up");
                MetricsManager.Instance.RecordGamePerformanceMetric(Id, GamePerformanceMetricEnum.CatchUpFrames, timesUpdated - 1);
            }

            // Skip time if we have fallen behind
            TimeSpan timeSkip = RealGameTime - _currentGameTime;
            if (timeSkip != TimeSpan.Zero)
            {
                Logger.Trace($"UpdateFixedTime(): Taking too long to catch up, skipping {timeSkip.TotalMilliseconds} ms");
                MetricsManager.Instance.RecordGamePerformanceMetric(Id, GamePerformanceMetricEnum.TimeSkip, timeSkip);
            }

            _currentGameTime = RealGameTime;
        }

        private void DoFixedTimeUpdate()
        {
            GameEventScheduler.TriggerEvents(_currentGameTime);

            // Re-enable locomotion and physics when we get rid of multithreading issues
            EntityManager.LocomoteEntities();
            EntityManager.PhysicsResolveEntities();
            EntityManager.ProcessDeferredLists();

            // Send responses to all clients
            NetworkManager.SendAllPendingMessages();
        }

        private void UpdateLiveTuning()
        {
            // This won't do anything unless this game's live tuning data is out of date
            LiveTuningManager.Instance.CopyLiveTuningData(LiveTuningData);  

            if (_liveTuningChangeNum != LiveTuningData.ChangeNum)
            {
                NetworkManager.BroadcastMessage(LiveTuningData.GetLiveTuningUpdate());
                _liveTuningChangeNum = LiveTuningData.ChangeNum;
            }
        }

        private void HandleGameInstanceCrash(Exception exception)
        {
#if DEBUG
            const string buildConfiguration = "Debug";
#elif RELEASE
            const string buildConfiguration = "Release";
#endif

            DateTime now = DateTime.Now;

            string crashReportDir = Path.Combine(FileHelper.ServerRoot, "CrashReports");
            if (Directory.Exists(crashReportDir) == false)
                Directory.CreateDirectory(crashReportDir);

            string crashReportFilePath = Path.Combine(crashReportDir, $"GameInstanceCrash_{now.ToString(FileHelper.FileNameDateFormat)}.txt");

            using (StreamWriter writer = new(crashReportFilePath))
            {
                writer.WriteLine(string.Format("Assembly Version: {0} | {1} UTC | {2}\n",
                    AssemblyHelper.GetAssemblyInformationalVersion(),
                    AssemblyHelper.ParseAssemblyBuildTime().ToString("yyyy.MM.dd HH:mm:ss"),
                    buildConfiguration));

                writer.WriteLine($"Local Server Time: {now:yyyy.MM.dd HH:mm:ss.fff}\n");

                writer.WriteLine($"Game: {this}\n");

                writer.WriteLine($"Exception:\n{exception}\n");

                writer.WriteLine("Active Regions:");
                foreach (Region region in RegionIterator())
                    writer.WriteLine(region.ToString());
                writer.WriteLine();

                writer.WriteLine("Scheduled Events:");
                foreach (var kvp in GameEventScheduler.GetScheduledEventCounts())
                    writer.WriteLine($"{kvp.Key} x{kvp.Value}");
                writer.WriteLine();

                writer.WriteLine($"Server Status:\n{ServerManager.Instance.GetServerStatus(true)}\n");
            }

            Logger.ErrorException(exception, $"Game instance crashed, report saved to {crashReportFilePath}");
        }
    }
}
