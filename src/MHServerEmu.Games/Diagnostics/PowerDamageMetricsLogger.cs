using System.Text.Json;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.Diagnostics
{
    /// <summary>
    /// Command-controlled JSONL logger for server-authoritative player damage.
    /// </summary>
    public static class PowerDamageMetricsLogger
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
        private static readonly object Lock = new();

        private static StreamWriter _writer;
        private static string _sessionId;
        private static string _outputPath;
        private static DateTime _startedUtc;
        private static long _damageEventCount;
        private static volatile bool _deterministicMode;

        public static bool IsEnabled { get; private set; }
        public static bool IsDeterministic => _deterministicMode;
        public static string OutputPath { get { lock (Lock) return _outputPath; } }
        public static string SessionId { get { lock (Lock) return _sessionId; } }
        public static long DamageEventCount { get { lock (Lock) return _damageEventCount; } }

        public static bool SetDeterministicMode(bool enabled, string actor)
        {
            bool changed = _deterministicMode != enabled;
            _deterministicMode = enabled;

            lock (Lock)
            {
                if (IsEnabled)
                    WriteEvent(new MarkerEvent(_sessionId, DateTime.UtcNow, actor, $"deterministic={(enabled ? "on" : "off")}"));
            }

            Logger.Info($"Power damage deterministic mode {(enabled ? "enabled" : "disabled")} by {actor}.");
            return changed;
        }

        public static bool Start(string label, string startedBy, out string outputPath)
        {
            lock (Lock)
            {
                if (IsEnabled)
                {
                    outputPath = _outputPath;
                    return false;
                }

                _startedUtc = DateTime.UtcNow;
                _sessionId = $"{_startedUtc:yyyyMMdd_HHmmss}_{SanitizeFileName(label)}";
                string dir = Path.Combine(FileHelper.ServerRoot, "Logs", "PowerDamage");
                Directory.CreateDirectory(dir);

                _outputPath = Path.Combine(dir, $"PowerDamage_{_sessionId}.jsonl");
                _writer = new(File.Open(_outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };

                _damageEventCount = 0;
                IsEnabled = true;

                WriteEvent(new SessionEvent("session_start", _sessionId, _startedUtc, startedBy, label, _deterministicMode, null));
                outputPath = _outputPath;
                Logger.Info($"Power damage metrics logging started: {_outputPath}");
                return true;
            }
        }

        public static bool Stop(string stoppedBy, out string outputPath, out long damageEventCount)
        {
            lock (Lock)
            {
                outputPath = _outputPath;
                damageEventCount = _damageEventCount;

                if (IsEnabled == false)
                    return false;

                WriteEvent(new SessionEvent("session_stop", _sessionId, DateTime.UtcNow, stoppedBy, null, _deterministicMode, _damageEventCount));

                _writer?.Dispose();
                _writer = null;
                IsEnabled = false;

                Logger.Info($"Power damage metrics logging stopped: {outputPath} ({damageEventCount} damage events)");
                return true;
            }
        }

        public static bool Mark(string label, string markedBy)
        {
            lock (Lock)
            {
                if (IsEnabled == false)
                    return false;

                WriteEvent(new MarkerEvent(_sessionId, DateTime.UtcNow, markedBy, label));
                return true;
            }
        }

        public static void RecordDamage(PowerResults powerResults, WorldEntity target, WorldEntity ultimateOwner,
            WorldEntity powerOwner, long healthBefore, long healthAfter, long adjustHealth)
        {
            if (IsEnabled == false || powerResults == null || target == null || powerResults.TestFlag(PowerResultFlags.Hostile) == false)
                return;

            Avatar sourceAvatar = ultimateOwner?.GetMostResponsiblePowerUser<Avatar>(true)
                ?? powerOwner?.GetMostResponsiblePowerUser<Avatar>(true);
            Player sourcePlayer = sourceAvatar?.GetOwnerOfType<Player>();
            if (sourcePlayer == null)
                return;

            float rawPhysical = powerResults.Properties[PropertyEnum.Damage, DamageType.Physical];
            float rawEnergy = powerResults.Properties[PropertyEnum.Damage, DamageType.Energy];
            float rawMental = powerResults.Properties[PropertyEnum.Damage, DamageType.Mental];
            float rawServerDamage = rawPhysical + rawEnergy + rawMental;

            float clientPhysical = powerResults.GetDamageForClient(DamageType.Physical);
            float clientEnergy = powerResults.GetDamageForClient(DamageType.Energy);
            float clientMental = powerResults.GetDamageForClient(DamageType.Mental);
            float clientDamage = clientPhysical + clientEnergy + clientMental;

            long actualHealthDamage = Math.Max(0L, -adjustHealth);
            if (rawServerDamage <= 0f && clientDamage <= 0f && actualHealthDamage <= 0L)
                return;

            var region = target.Region;
            var rankProto = target.GetRankPrototype();
            var evt = new DamageEvent(
                _sessionId,
                DateTime.UtcNow,
                target.Game?.CurrentTime.TotalMilliseconds ?? 0d,
                target.Game?.Id ?? 0,
                region?.Id ?? 0,
                ToPrototypeName(region?.PrototypeDataRef ?? PrototypeId.Invalid),
                ToPrototypeName(region?.DifficultyTierRef ?? PrototypeId.Invalid),
                sourcePlayer.Id,
                sourcePlayer.DatabaseUniqueId,
                sourcePlayer.GetName(),
                sourceAvatar.Id,
                ToPrototypeName(sourceAvatar.PrototypeDataRef),
                powerOwner?.Id ?? 0,
                ultimateOwner?.Id ?? 0,
                ToPrototypeName(powerOwner?.PrototypeDataRef ?? PrototypeId.Invalid),
                ToPrototypeName(ultimateOwner?.PrototypeDataRef ?? PrototypeId.Invalid),
                target.Id,
                ToPrototypeName(target.PrototypeDataRef),
                ToPrototypeName(rankProto?.DataRef ?? PrototypeId.Invalid),
                target.CombatLevel,
                Math.Max(0L, healthBefore),
                Math.Max(0L, healthAfter),
                Math.Max(0L, target.Properties[PropertyEnum.HealthMax]),
                ToPrototypeName(powerResults.PowerPrototype?.DataRef ?? PrototypeId.Invalid),
                ((ulong)powerResults.PowerAssetRefOverride).ToString(),
                powerResults.Flags.ToString(),
                powerResults.TestFlag(PowerResultFlags.Critical),
                powerResults.TestFlag(PowerResultFlags.SuperCritical),
                powerResults.TestFlag(PowerResultFlags.Blocked),
                powerResults.TestFlag(PowerResultFlags.Resisted),
                powerResults.TestFlag(PowerResultFlags.OverTime),
                powerResults.TestFlag(PowerResultFlags.Proc),
                powerResults.TestFlag(PowerResultFlags.InstantKill),
                _deterministicMode,
                actualHealthDamage,
                rawServerDamage,
                clientDamage,
                rawPhysical,
                rawEnergy,
                rawMental,
                clientPhysical,
                clientEnergy,
                clientMental);

            lock (Lock)
            {
                if (IsEnabled == false)
                    return;

                WriteEvent(evt);
                _damageEventCount++;
            }
        }

        private static void WriteEvent(object evt)
        {
            _writer.WriteLine(JsonSerializer.Serialize(evt, JsonOptions));
        }

        private static string ToPrototypeName(PrototypeId protoRef)
        {
            if (protoRef == PrototypeId.Invalid)
                return string.Empty;

            string name = GameDatabase.GetPrototypeName(protoRef);
            return string.IsNullOrEmpty(name) ? ((ulong)protoRef).ToString() : name;
        }

        private static string SanitizeFileName(string label)
        {
            string value = string.IsNullOrWhiteSpace(label) ? "session" : label.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value.Length > 48 ? value[..48] : value;
        }

        private sealed record SessionEvent(
            string EventType,
            string SessionId,
            DateTime TimestampUtc,
            string Actor,
            string Label,
            bool DeterministicMode,
            long? DamageEventCount)
        {
            public string Schema { get; init; } = "mh_power_damage_v1";
        }

        private sealed record MarkerEvent(
            string SessionId,
            DateTime TimestampUtc,
            string Actor,
            string Label)
        {
            public string Schema { get; init; } = "mh_power_damage_v1";
            public string EventType { get; init; } = "marker";
        }

        private sealed record DamageEvent(
            string SessionId,
            DateTime TimestampUtc,
            double GameTimeMs,
            ulong GameId,
            ulong RegionId,
            string RegionPrototype,
            string DifficultyTier,
            ulong PlayerEntityId,
            ulong PlayerDbId,
            string PlayerName,
            ulong AvatarEntityId,
            string AvatarPrototype,
            ulong PowerOwnerEntityId,
            ulong UltimateOwnerEntityId,
            string PowerOwnerPrototype,
            string UltimateOwnerPrototype,
            ulong TargetEntityId,
            string TargetPrototype,
            string TargetRank,
            int TargetCombatLevel,
            long TargetHealthBefore,
            long TargetHealthAfter,
            long TargetHealthMax,
            string PowerPrototype,
            string PowerAssetRefOverride,
            string ResultFlags,
            bool Critical,
            bool SuperCritical,
            bool Blocked,
            bool Resisted,
            bool OverTime,
            bool Proc,
            bool InstantKill,
            bool DeterministicMode,
            long ActualHealthDamage,
            float RawServerDamage,
            float ClientDisplayDamage,
            float RawPhysical,
            float RawEnergy,
            float RawMental,
            float ClientPhysical,
            float ClientEnergy,
            float ClientMental)
        {
            public string Schema { get; init; } = "mh_power_damage_v1";
            public string EventType { get; init; } = "damage";
        }
    }
}
