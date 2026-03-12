using MHServerEmu.Core.Logging;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Loot;
using System.Text.Json;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Games.GameData.Prototypes;  // Add this for ItemPrototype
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities;
using System.Collections.Concurrent;
using MHServerEmu.Core.System.Time;
using MHServerEmu.DatabaseAccess;



namespace MHServerEmu.Games.Gifting
{
    public class GiftItemEntry
    {
        public ulong ItemPrototype { get; set; }  // Store as ulong
        public int Count { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime? EndDate { get; set; }  // Optional end date - null means no expiration
        public bool IsDaily { get; set; } = false;  // If true, can be claimed once per day
    }

    public class PlayerSpecificGiftEntry
    {
        public string Email { get; set; }  // Player email to receive the gift
        public ulong ItemPrototype { get; set; }
        public int Count { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsDaily { get; set; } = false;  // If true, can be claimed once per day
    }

    public static class GiftItemDistributor
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly string PendingItemsPath = Path.Combine(FileHelper.DataDirectory, "PendingItems.json");
        private static readonly string PlayerSpecificItemsPath = Path.Combine(FileHelper.DataDirectory, "PlayerSpecificItems.json");
        private static List<GiftItemEntry> _cachedItems;
        private static List<PlayerSpecificGiftEntry> _playerSpecificItems;

        public static bool Initialize()
        {
            Logger.Info("Initializing GiftItemDistributor");
            LoadGiftItems();
            LoadPlayerSpecificItems();
            GiftClaimStorage.Initialize();
            return true;
        }

        private static void LoadGiftItems()
        {
            Logger.Info("LoadGiftItems called");
            if (File.Exists(PendingItemsPath))
            {
                Logger.Info($"Loading gifts from {PendingItemsPath}");
                string jsonContent = File.ReadAllText(PendingItemsPath);
                _cachedItems = JsonSerializer.Deserialize<List<GiftItemEntry>>(jsonContent);
                Logger.Info($"Loaded {_cachedItems.Count} gift entries");
            }
            else
            {
                _cachedItems = new List<GiftItemEntry>();
                Logger.Info("Created new empty gift list");
            }
        }

        private static void LoadPlayerSpecificItems()
        {
            Logger.Info("LoadPlayerSpecificItems called");
            if (File.Exists(PlayerSpecificItemsPath))
            {
                Logger.Info($"Loading player-specific gifts from {PlayerSpecificItemsPath}");
                string jsonContent = File.ReadAllText(PlayerSpecificItemsPath);
                _playerSpecificItems = JsonSerializer.Deserialize<List<PlayerSpecificGiftEntry>>(jsonContent);
                Logger.Info($"Loaded {_playerSpecificItems.Count} player-specific gift entries");
            }
            else
            {
                _playerSpecificItems = new List<PlayerSpecificGiftEntry>();
                Logger.Info("Created new empty player-specific gift list");
            }
        }

        public static void DistributeGiftItems(Player player)
        {
            var currentTime = DateTime.UtcNow;
            ulong playerDbId = player.PlayerConnection.PlayerDbId;
            int giftsDistributed = 0;

            // Distribute global gifts (available to all players)
            if (_cachedItems != null && _cachedItems.Count > 0)
            {
                foreach (var entry in _cachedItems)
                {
                    if (entry.AddedDate > currentTime)
                    {
                        //Logger.Debug($"Gift {entry.ItemPrototype} not yet available. Current time: {currentTime}, Gift available: {entry.AddedDate}");
                        continue;
                    }

                    if (entry.EndDate.HasValue && entry.EndDate.Value < currentTime)
                    {
                        //Logger.Debug($"Gift {entry.ItemPrototype} has expired. Current time: {currentTime}, Gift expired: {entry.EndDate}");
                        continue;
                    }

                    bool shouldClaim = false;
                    if (entry.IsDaily)
                    {
                        // Daily gifts: check if claimed today
                        if (GiftClaimStorage.CanClaimDaily(playerDbId, entry.ItemPrototype))
                            shouldClaim = true;
                    }
                    else
                    {
                        // One-time gifts: check if ever claimed
                        if (!GiftClaimStorage.HasClaimed(playerDbId, entry.ItemPrototype))
                            shouldClaim = true;
                    }

                    if (shouldClaim)
                    {
                        ItemPrototype itemProto = GameDatabase.GetPrototype<ItemPrototype>((PrototypeId)entry.ItemPrototype);
                        if (itemProto != null)
                        {
                            for (int i = 0; i < entry.Count; i++)
                                player.Game.LootManager.GiveItem(itemProto.DataRef, LootContext.Drop, player);
                            _ = GiftClaimStorage.SaveClaimAsync(playerDbId, entry.ItemPrototype);
                            giftsDistributed++;
                            //Logger.Info($"Successfully distributed {entry.Count}x {itemProto} to player {email}");
                        }
                    }
                }
            }

            // Distribute player-specific gifts
            if (_playerSpecificItems != null && _playerSpecificItems.Count > 0)
            {
                foreach (var entry in _playerSpecificItems)
                {
                    // Convert email to playerDbId using IDBManager
                    if (!IDBManager.Instance.TryQueryAccountByEmail(entry.Email, out var account))
                        continue; // Email not found in database

                    // Check if this gift is for the current player
                    if ((ulong)account.Id != playerDbId)
                        continue;

                    if (entry.AddedDate > currentTime)
                    {
                        //Logger.Debug($"Player-specific gift {entry.ItemPrototype} not yet available for {email}");
                        continue;
                    }

                    if (entry.EndDate.HasValue && entry.EndDate.Value < currentTime)
                    {
                        //Logger.Debug($"Player-specific gift {entry.ItemPrototype} has expired for {email}");
                        continue;
                    }

                    bool shouldClaim = false;
                    if (entry.IsDaily)
                    {
                        // Daily gifts: check if claimed today
                        if (GiftClaimStorage.CanClaimDaily(playerDbId, entry.ItemPrototype))
                            shouldClaim = true;
                    }
                    else
                    {
                        // One-time gifts: check if ever claimed
                        if (!GiftClaimStorage.HasClaimed(playerDbId, entry.ItemPrototype))
                            shouldClaim = true;
                    }

                    if (shouldClaim)
                    {
                        ItemPrototype itemProto = GameDatabase.GetPrototype<ItemPrototype>((PrototypeId)entry.ItemPrototype);
                        if (itemProto != null)
                        {
                            for (int i = 0; i < entry.Count; i++)
                                player.Game.LootManager.GiveItem(itemProto.DataRef, LootContext.Drop, player);
                            _ = GiftClaimStorage.SaveClaimAsync(playerDbId, entry.ItemPrototype);
                            giftsDistributed++;
                            //Logger.Info($"Successfully distributed player-specific {entry.Count}x {itemProto} to {email}");
                        }
                    }
                }
            }

            if (giftsDistributed > 0)
                Logger.Info($"Distributed {giftsDistributed} gifts to player 0x{playerDbId:X}");
        }
    }
}

