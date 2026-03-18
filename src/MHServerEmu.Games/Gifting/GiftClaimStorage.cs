using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.Gifting
{
    public static class GiftClaimStorage
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly string ClaimsPath = Path.Combine(FileHelper.DataDirectory, "GiftClaims.dat");
        private static readonly Dictionary<(ulong PlayerDbId, ulong ItemId), DateTime> _claims = new();
        private static readonly object _lockObject = new();
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

        private static Timer _flushTimer;
        private static bool _isDirty;
        private static int _pendingWriteCount;
        private static int _flushInProgress;

        public static void Initialize()
        {
            Logger.Info("Initializing GiftClaimStorage");
            LoadFromDisk();

            _flushTimer ??= new Timer(_ => FlushIfDirty(), null, FlushInterval, FlushInterval);
        }

        public static bool HasClaimed(ulong playerDbId, ulong itemId)
        {
            lock (_lockObject)
            {
                return _claims.ContainsKey((playerDbId, itemId));
            }
        }

        public static bool CanClaimDaily(ulong playerDbId, ulong itemId)
        {
            lock (_lockObject)
            {
                if (_claims.TryGetValue((playerDbId, itemId), out DateTime lastClaimTime))
                {
                    // Can claim if last claim was on a different day (UTC)
                    return lastClaimTime.Date < DateTime.UtcNow.Date;
                }
                // Never claimed before, can claim
                return true;
            }
        }

        public static void SaveClaim(ulong playerDbId, ulong itemId)
        {
            bool flushNow;

            lock (_lockObject)
            {
                _claims[(playerDbId, itemId)] = DateTime.UtcNow;
                _isDirty = true;
                _pendingWriteCount++;

                // Trigger an immediate flush for larger bursts so claim data doesn't stay in memory too long.
                flushNow = _pendingWriteCount >= 256;
            }

            if (flushNow)
                FlushIfDirty();
        }

        public static Task SaveClaimAsync(ulong playerDbId, ulong itemId)
        {
            SaveClaim(playerDbId, itemId);
            return Task.CompletedTask;
        }

        private static void FlushIfDirty()
        {
            if (Interlocked.Exchange(ref _flushInProgress, 1) == 1)
                return;

            try
            {
                Dictionary<(ulong PlayerDbId, ulong ItemId), DateTime> snapshot;

                lock (_lockObject)
                {
                    if (_isDirty == false)
                        return;

                    snapshot = new Dictionary<(ulong PlayerDbId, ulong ItemId), DateTime>(_claims);
                }

                if (SaveToDisk(snapshot))
                {
                    lock (_lockObject)
                    {
                        _isDirty = false;
                        _pendingWriteCount = 0;
                    }
                }
                else
                {
                    lock (_lockObject)
                    {
                        _isDirty = true;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _flushInProgress, 0);
            }
        }

        private static void LoadFromDisk()
        {
            if (!File.Exists(ClaimsPath))
            {
                Logger.Info("No existing claims file found");
                return;
            }

            try
            {
                using var reader = new BinaryReader(File.OpenRead(ClaimsPath));
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    ulong playerDbId = reader.ReadUInt64();
                    ulong itemId = reader.ReadUInt64();
                    long ticks = reader.ReadInt64();
                    _claims[(playerDbId, itemId)] = new DateTime(ticks);
                }
                Logger.Info($"Loaded {count} gift claims from storage");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading gift claims: {ex.Message}");
            }
        }

        private static bool SaveToDisk(Dictionary<(ulong PlayerDbId, ulong ItemId), DateTime> snapshot)
        {
            try
            {
                using var writer = new BinaryWriter(File.Create(ClaimsPath));
                writer.Write(snapshot.Count);
                foreach (var ((playerDbId, itemId), claimTime) in snapshot)
                {
                    writer.Write(playerDbId);
                    writer.Write(itemId);
                    writer.Write(claimTime.Ticks);
                }
                Logger.Info($"Saved {snapshot.Count} gift claims to storage");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving gift claims: {ex.Message}");
                return false;
            }
        }
    }
}
