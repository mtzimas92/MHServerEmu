using MHServerEmu.Core.Logging;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Loot;
using System.Text.Json;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Games.GameData.Prototypes;  // Add this for ItemPrototype
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities;
using MHServerEmu.DatabaseAccess;



namespace MHServerEmu.Games.Gifting
{
    public class GiftItemEntry
    {
        public ulong ItemPrototype { get; set; }  // Store as ulong
        public int Count { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime? EndDate { get; set; } = null;  // Optional; omit from JSON if no expiration
        public bool IsDaily { get; set; } = false;  // Optional; omit from JSON for one-time gifts (default: false)
    }

    public class PlayerSpecificGiftEntry
    {
        public string Email { get; set; }  // Player email to receive the gift
        public ulong ItemPrototype { get; set; }
        public int Count { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime? EndDate { get; set; } = null;  // Optional; omit from JSON if no expiration
        public bool IsDaily { get; set; } = false;  // Optional; omit from JSON for one-time gifts (default: false)
    }

    public static class GiftItemDistributor
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly string PendingItemsPath = Path.Combine(FileHelper.DataDirectory, "PendingItems.json");
        private static readonly string PlayerSpecificItemsPath = Path.Combine(FileHelper.DataDirectory, "PlayerSpecificItems.json");
        private static List<GiftItemEntry> _cachedItems;
        private static List<PlayerSpecificGiftEntry> _playerSpecificItems;
        private static Dictionary<ulong, List<PlayerSpecificGiftEntry>> _playerSpecificItemsByDbId;

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
                _cachedItems = JsonSerializer.Deserialize<List<GiftItemEntry>>(jsonContent) ?? new List<GiftItemEntry>();
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
            _playerSpecificItemsByDbId = new Dictionary<ulong, List<PlayerSpecificGiftEntry>>();

            if (File.Exists(PlayerSpecificItemsPath))
            {
                Logger.Info($"Loading player-specific gifts from {PlayerSpecificItemsPath}");
                string jsonContent = File.ReadAllText(PlayerSpecificItemsPath);
                _playerSpecificItems = JsonSerializer.Deserialize<List<PlayerSpecificGiftEntry>>(jsonContent) ?? new List<PlayerSpecificGiftEntry>();
                Logger.Info($"Loaded {_playerSpecificItems.Count} player-specific gift entries");

                BuildPlayerSpecificGiftIndex();
            }
            else
            {
                _playerSpecificItems = new List<PlayerSpecificGiftEntry>();
                Logger.Info("Created new empty player-specific gift list");
            }
        }

        private static void BuildPlayerSpecificGiftIndex()
        {
            var emailToPlayerDbIdCache = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            int unresolvedEmails = 0;

            foreach (var entry in _playerSpecificItems)
            {
                if (string.IsNullOrWhiteSpace(entry.Email))
                {
                    unresolvedEmails++;
                    continue;
                }

                if (!emailToPlayerDbIdCache.TryGetValue(entry.Email, out ulong playerDbId))
                {
                    if (!IDBManager.Instance.TryQueryAccountByEmail(entry.Email, out var account))
                    {
                        emailToPlayerDbIdCache[entry.Email] = 0;
                        unresolvedEmails++;
                        continue;
                    }

                    playerDbId = (ulong)account.Id;
                    emailToPlayerDbIdCache[entry.Email] = playerDbId;
                }

                if (playerDbId == 0)
                    continue;

                if (_playerSpecificItemsByDbId.TryGetValue(playerDbId, out var list) == false)
                {
                    list = new List<PlayerSpecificGiftEntry>();
                    _playerSpecificItemsByDbId[playerDbId] = list;
                }

                list.Add(entry);
            }

            Logger.Info($"Indexed {_playerSpecificItemsByDbId.Count} players for player-specific gifts (unresolved entries: {unresolvedEmails})");
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
                            GiftClaimStorage.SaveClaim(playerDbId, entry.ItemPrototype);
                            giftsDistributed++;
                            //Logger.Info($"Successfully distributed {entry.Count}x {itemProto} to player {email}");
                        }
                    }
                }
            }

            // Distribute player-specific gifts
            if (_playerSpecificItemsByDbId != null
                && _playerSpecificItemsByDbId.TryGetValue(playerDbId, out var playerSpecificGifts)
                && playerSpecificGifts.Count > 0)
            {
                foreach (var entry in playerSpecificGifts)
                {
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
                            GiftClaimStorage.SaveClaim(playerDbId, entry.ItemPrototype);
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
