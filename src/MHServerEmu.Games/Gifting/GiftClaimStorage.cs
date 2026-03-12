using System;
using System.Collections.Generic;
using System.IO;
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

        public static void Initialize()
        {
            Logger.Info("Initializing GiftClaimStorage");
            LoadFromDisk();
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
            lock (_lockObject)
            {
                _claims[(playerDbId, itemId)] = DateTime.UtcNow;
                _ = SaveClaimAsync(playerDbId, itemId);
            }
        }

        public  static async Task SaveClaimAsync(ulong playerDbId, ulong itemId)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _claims[(playerDbId, itemId)] = DateTime.UtcNow;
                    SaveToDisk();
                }
            });
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

        private static void SaveToDisk()
        {
            try
            {
                using var writer = new BinaryWriter(File.Create(ClaimsPath));
                writer.Write(_claims.Count);
                foreach (var ((playerDbId, itemId), claimTime) in _claims)
                {
                    writer.Write(playerDbId);
                    writer.Write(itemId);
                    writer.Write(claimTime.Ticks);
                }
                Logger.Info($"Saved {_claims.Count} gift claims to storage");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving gift claims: {ex.Message}");
            }
        }
    }
}

