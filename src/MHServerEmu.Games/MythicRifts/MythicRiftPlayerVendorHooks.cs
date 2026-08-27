using Gazillion;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Games.Entities.Inventories;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.Entities
{
    public partial class Player
    {
        private const string MythicRiftDangerRoomVendorTypeName = "VendorDangerRoomRewards";
        private const string MythicRiftVendorHint =
            "[Mythic Rift] Cosmic Rift Scenario starts infinite-level scaling. Rift Gauntlet Scenario starts the repeating 30-wave challenge. Boss Gauntlet Scenario starts endless boss-only waves with failure loot.";
        private const string MythicRiftPurchaseHint =
            "[Mythic Rift] Cosmic Rift Scenario purchased. Use it from the Danger Room Hub to open the infinite-level Rift.";
        private const string EndlessRiftPurchaseHint =
            "[Mythic Rift] Rift Gauntlet Scenario purchased. Use it from the Danger Room Hub to enter the repeating 30-wave Rift.";
        private const string BossGauntletRiftPurchaseHint =
            "[Mythic Rift] Boss Gauntlet Scenario purchased. Use it from the Danger Room Hub to enter endless boss-only waves.";

        private sealed class MythicRiftCompletionVendorStockEntry
        {
            public string OfferId { get; init; } = string.Empty;
            public PrototypeId RewardItemProtoRef { get; init; }
            public int Cost { get; init; }
            public int ItemLevel { get; init; }
        }

        private readonly HashSet<ulong> _mythicRiftVendorItemIds = new();
        private readonly Dictionary<ulong, MythicRiftCompletionVendorStockEntry> _mythicRiftCompletionVendorOfferItemIds = new();
        private readonly HashSet<ulong> _mythicRiftCompletionCrafterRecipeItemIds = new();
        private ulong _mythicRiftCompletionVendorEntityId;
        private bool _mythicRiftCompletionVendorInventoriesFiltered;
        private bool _mythicRiftCompletionCrafterInventoriesFiltered;
        private bool _mythicRiftVendorHintSent;

        private bool BuyMythicRiftVendorItem(int avatarIndex, Item vendorItem)
        {
            if (vendorItem == null) { Logger.Warn("BuyMythicRiftVendorItem(): vendorItem == null"); return false; }

            ItemPrototype itemProto = vendorItem.ItemPrototype;
            if (itemProto == null) { Logger.Warn("BuyMythicRiftVendorItem(): itemProto == null"); return false; }

            Inventory destinationInventory = GetInventory(itemProto.DestinationFromVendor);
            if (destinationInventory == null) { Logger.Warn("BuyMythicRiftVendorItem(): destinationInventory == null"); return false; }

            uint destinationSlot = destinationInventory.GetFreeSlot(vendorItem, true);
            if (destinationSlot == Inventory.InvalidSlot)
                {
                    Logger.Warn("BuyMythicRiftVendorItem(): destinationSlot == Inventory.InvalidSlot");
                    return false;
                }

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = vendorItem.PrototypeDataRef;
            settings.ItemSpec = new(vendorItem.ItemSpec);
            settings.InventoryLocation = new(Id, destinationInventory.PrototypeDataRef, destinationSlot);

            if (IsInGame == false)
                settings.OptionFlags &= ~EntitySettingsOptionFlags.EnterGame;

            Item clonedItem = Game.EntityManager.CreateEntity(settings) as Item;
            if (clonedItem == null)
                {
                    Logger.Warn($"BuyMythicRiftVendorItem(): Failed to clone item [{vendorItem}]");
                    return false;
                }

            if (Game.MythicRiftLauncherService.TryRegisterTrackedBeaconItem(this, clonedItem) == false)
                {
                    Logger.Warn($"BuyMythicRiftVendorItem(): Failed to register purchased Rift beacon [{clonedItem}]");
                    return false;
                }

            Logger.Info($"[MythicRiftVendor] Registered purchased beacon playerDbId={DatabaseUniqueId} itemId={clonedItem.Id} prototype={clonedItem.PrototypeDataRef.GetNameFormatted()} totalTrackedCharges={Game.MythicRiftLauncherService.GetTotalTrackedBeaconCharges(DatabaseUniqueId)}");

            int count = clonedItem.CurrentStackSize;
            GetRegion()?.PlayerBoughtItemEvent.Invoke(new(this, clonedItem, count));

            Prototype rarityProto = clonedItem.RarityPrototype;
            OnScoringEvent(new(ScoringEventType.ItemBought, clonedItem.Prototype, rarityProto, count));
            OnScoringEvent(new(ScoringEventType.ItemCollected, clonedItem.Prototype, rarityProto, count));

            MythicRiftMode mode = Game.MythicRiftLauncherService.ResolveModeForPrototype(clonedItem.PrototypeDataRef);
            string purchaseHint = mode switch
            {
                MythicRiftMode.Endless => EndlessRiftPurchaseHint,
                MythicRiftMode.BossGauntlet => BossGauntletRiftPurchaseHint,
                _ => MythicRiftPurchaseHint
            };
            Game.ChatManager?.SendChatFromCustomSystem(this, purchaseHint, showSender: false);

            return true;
        }

        private bool TryRegisterPurchasedMythicRiftBeacon(Item item, WorldEntity vendor, string source)
        {
            if (item == null || vendor == null || Game?.MythicRiftLauncherService == null)
                return false;

            if (Game.MythicRiftLauncherService.IsChosenBeaconPrototype(item.PrototypeDataRef) == false)
                return false;

            if (Game.MythicRiftLauncherService.TryRegisterTrackedBeaconItem(this, item) == false)
                {
                    Logger.Warn($"TryRegisterPurchasedMythicRiftBeacon(): Failed to register purchased Rift beacon [{item}] from {source}");
                    return false;
                }

            Logger.Info($"[MythicRiftVendor] Registered purchased beacon via {source} playerDbId={DatabaseUniqueId} vendor={vendor.PrototypeDataRef.GetNameFormatted()} itemId={item.Id} prototype={item.PrototypeDataRef.GetNameFormatted()} totalTrackedCharges={Game.MythicRiftLauncherService.GetTotalTrackedBeaconCharges(DatabaseUniqueId)}");
            return true;
        }

        private bool ShouldInjectMythicRiftVendorStock(WorldEntity vendor)
        {
            if (vendor == null)
                return false;

            if (Game?.MythicRiftManager?.IsCompletionOmegaForgeVendor(vendor) == true)
                return false;

            Region region = vendor.Region ?? GetRegion();
            if (region == null)
                return false;

            if (region.PrototypeDataRef != (PrototypeId)13296910602616641976UL)
                return false;

            if (vendor.IsVendor == false)
                return false;

            // [ScenarioItems] Now that a second vendor (Danger Room Scenario Vendor, VendorDangerRoomScenario)
            // can also exist in this region, this must be gated by vendor type name too - otherwise it would
            // auto-stock the Scenario Vendor with the presentation launcher items on top of its own static
            // Blue/Purple/Cosmic crate stock, duplicating them.
            PrototypeId vendorTypeProtoRef = vendor.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            return ShouldInjectMythicRiftVendorStock(vendorTypeProto);
        }

        private static bool ShouldInjectMythicRiftVendorStock(VendorTypePrototype vendorTypeProto)
        {
            // [ScenarioItems] Retired - the Danger Room Rewards Vendor (VendorDangerRoomRewards) no longer needs
            // dynamically-injected CableFight/TestHearthStone/TestStunKit stock now that the dedicated Danger Room
            // Scenario Vendor sells the equivalent Blue/Purple/Cosmic crates as static loot-table stock instead.
            return false;
        }

        private bool TryInitializeMythicRiftCompletionVendorInventory(PrototypeId inventoryProtoRef)
        {
            if (inventoryProtoRef == PrototypeId.Invalid || Game?.MythicRiftManager == null)
                return false;

            WorldEntity dialogTarget = GetDialogTarget(false);
            if (dialogTarget == null || Game.MythicRiftManager.IsCompletionArtifactVendor(dialogTarget) == false)
                return false;

            PrototypeId vendorTypeProtoRef = dialogTarget.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProto?.ContainsInventory(inventoryProtoRef) != true)
                return false;

            EnsureMythicRiftCompletionVendorStock(dialogTarget);
            return true;
        }

        private bool EnsureMythicRiftCompletionVendorStock(WorldEntity vendor)
        {
            if (vendor == null || Game?.MythicRiftManager?.IsCompletionArtifactVendor(vendor) != true)
                return false;

            PrototypeId vendorTypeProtoRef = vendor.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProto == null)
                return true;

            if (_mythicRiftCompletionVendorEntityId != vendor.Id)
            {
                _mythicRiftCompletionVendorEntityId = vendor.Id;
                _mythicRiftCompletionVendorOfferItemIds.Clear();
                _mythicRiftCompletionVendorInventoriesFiltered = false;
                _initializedVendorTypeProtoRefs.Remove(vendorTypeProtoRef);
            }

            bool stocked = TryAddMythicRiftCompletionVendorOffers(vendorTypeProto, vendorTypeProtoRef);
            if (stocked)
                SendMessage(NetMessageVendorRefresh.CreateBuilder().SetVendorTypeProtoId((ulong)vendorTypeProtoRef).Build());

            return true;
        }

        private bool TryAddMythicRiftCompletionVendorOffers(VendorTypePrototype vendorTypeProto, PrototypeId vendorTypeProtoRef)
        {
            if (vendorTypeProto == null || Game?.MythicRiftManager == null)
                return false;

            using var inventoryListHandle = ListPool<PrototypeId>.Get(out List<PrototypeId> inventoryList);
            if (vendorTypeProto.GetInventories(inventoryList) == false)
                return false;

            CleanupTrackedMythicRiftCompletionVendorOffers();
            FilterMythicRiftCompletionVendorInventories(vendorTypeProto, inventoryList);

            bool addedAny = false;
            foreach (MythicRiftRewardShopOfferTuning offer in Game.MythicRiftManager.RewardShopOffers)
            {
                if (offer?.Enabled != true || offer.HasAnyReward == false || offer.SigilCost <= 0)
                    continue;

                int desiredStockCount = Math.Clamp(Math.Max(offer.ItemRolls * 3, 3), 3, 8);
                int existingStockCount = CountTrackedMythicRiftCompletionVendorOffer(inventoryList, offer.Id);
                for (int stockIndex = existingStockCount; stockIndex < desiredStockCount; stockIndex++)
                {
                    if (Game.MythicRiftManager.TryResolveRewardShopOfferVendorStockItemPrototype(offer, out PrototypeId itemProtoRef) == false)
                    {
                        if (stockIndex == 0)
                            Logger.Warn($"TryAddMythicRiftCompletionVendorOffers(): Failed to resolve concrete vendor item for offer {offer.Id}");

                        break;
                    }

                    MythicRiftCompletionVendorStockEntry stockEntry = new()
                    {
                        OfferId = offer.Id,
                        RewardItemProtoRef = itemProtoRef,
                        Cost = Game.MythicRiftManager.GetCompletionArtifactVendorStockCost(offer),
                        ItemLevel = offer.ItemLevel
                    };

                    if (TryAddMythicRiftCompletionVendorOfferItem(vendorTypeProto, inventoryList, stockEntry))
                        addedAny = true;
                }
            }

            if (addedAny)
            {
                _initializedVendorTypeProtoRefs.Remove(vendorTypeProtoRef);
                Game.ChatManager?.SendChatFromCustomSystem(
                    this,
                    $"[Mythic Rift] Rift artifact vendor loaded. Champion's Commendations={Game.MythicRiftManager.GetRiftSigilCount(this)}.",
                    showSender: false);
            }

            return addedAny || _mythicRiftCompletionVendorInventoriesFiltered;
        }

        private bool TryAddMythicRiftCompletionVendorOfferItem(
            VendorTypePrototype vendorTypeProto,
            List<PrototypeId> inventoryList,
            MythicRiftCompletionVendorStockEntry stockEntry)
        {
            if (inventoryList == null || stockEntry == null)
                return false;

            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                uint slot = inventory.GetFreeSlot(null, false);
                if (slot == Inventory.InvalidSlot)
                    continue;

                PrototypeId itemProtoRef = stockEntry.RewardItemProtoRef;
                ItemSpec itemSpec = Game.LootManager.CreateItemSpec(itemProtoRef, LootContext.Vendor, this, Math.Max(stockEntry.ItemLevel, 1));
                if (itemSpec == null)
                    {
                        Logger.Warn($"TryAddMythicRiftCompletionVendorOfferItem(): Failed to create ItemSpec for {itemProtoRef.GetNameFormatted()}");
                        return false;
                    }

                using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
                settings.EntityRef = itemSpec.ItemProtoRef;
                settings.ItemSpec = itemSpec;

                if (IsInGame == false)
                    settings.OptionFlags &= ~EntitySettingsOptionFlags.EnterGame;

                Item item = Game.EntityManager.CreateEntity(settings) as Item;
                if (item == null)
                    {
                        Logger.Warn("TryAddMythicRiftCompletionVendorOfferItem(): item == null");
                        return false;
                    }

                InventoryResult inventoryResult = item.ChangeInventoryLocation(inventory, slot);
                if (inventoryResult != InventoryResult.Success)
                {
                    item.Destroy();
                    {
                        Logger.Warn($"TryAddMythicRiftCompletionVendorOfferItem(): Failed to add {itemProtoRef.GetNameFormatted()} to {inventory} for reason {inventoryResult}");
                        return false;
                    }
                }

                _mythicRiftCompletionVendorOfferItemIds[item.Id] = stockEntry;
                Logger.Info($"[MythicRiftCompletionVendor] Added offer={stockEntry.OfferId} item={itemProtoRef.GetNameFormatted()} cost={stockEntry.Cost} vendorType={vendorTypeProto.DataRef.GetNameFormatted()} inventory={inventory.PrototypeDataRef.GetNameFormatted()} itemId=0x{item.Id:X}");
                return true;
            }

            Logger.Warn($"TryAddMythicRiftCompletionVendorOfferItem(): No free completion vendor slot for offer {stockEntry.OfferId}");
            return false;
        }

        private void FilterMythicRiftCompletionVendorInventories(VendorTypePrototype vendorTypeProto, List<PrototypeId> inventoryList)
        {
            if (_mythicRiftCompletionVendorInventoriesFiltered && IsMythicRiftCompletionVendorAlreadyIsolated(inventoryList))
                return;

            int clearedInventories = 0;
            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                inventory.DestroyContained();
                clearedInventories++;
            }

            _mythicRiftCompletionVendorOfferItemIds.Clear();
            _mythicRiftCompletionVendorInventoriesFiltered = true;
            _initializedVendorTypeProtoRefs.Remove(vendorTypeProto.DataRef);
            Logger.Trace($"[MythicRiftCompletionVendor] Isolated completion vendor inventories playerDbId={DatabaseUniqueId} vendorType={vendorTypeProto.DataRef.GetNameFormatted()} clearedInventories={clearedInventories}.");
        }

        private bool IsMythicRiftCompletionVendorAlreadyIsolated(List<PrototypeId> inventoryList)
        {
            bool foundTrackedOffer = false;
            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                foreach (var entry in inventory)
                {
                    Item item = Game.EntityManager.GetEntity<Item>(entry.Id);
                    if (item == null || item.IsScheduledToDestroy)
                        continue;

                    if (_mythicRiftCompletionVendorOfferItemIds.ContainsKey(item.Id))
                    {
                        foundTrackedOffer = true;
                        continue;
                    }

                    return false;
                }
            }

            return foundTrackedOffer;
        }

        private int CountTrackedMythicRiftCompletionVendorOffer(List<PrototypeId> inventoryList, string offerId)
        {
            if (inventoryList == null || string.IsNullOrWhiteSpace(offerId))
                return 0;

            int count = 0;
            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                foreach (var entry in inventory)
                {
                    Item item = Game.EntityManager.GetEntity<Item>(entry.Id);
                    if (item != null &&
                        _mythicRiftCompletionVendorOfferItemIds.TryGetValue(item.Id, out MythicRiftCompletionVendorStockEntry existingEntry) &&
                        string.Equals(existingEntry.OfferId, offerId, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private bool IsMythicRiftCompletionVendorOfferItem(Item item, WorldEntity vendor)
        {
            if (item == null || vendor == null || Game?.MythicRiftManager?.IsCompletionArtifactVendor(vendor) != true)
                return false;

            CleanupTrackedMythicRiftCompletionVendorOffers();
            return _mythicRiftCompletionVendorOfferItemIds.ContainsKey(item.Id);
        }

        private bool TryBuyMythicRiftCompletionVendorOffer(Item item, WorldEntity vendor)
        {
            if (IsMythicRiftCompletionVendorOfferItem(item, vendor) == false)
                return false;

            if (_mythicRiftCompletionVendorOfferItemIds.TryGetValue(item.Id, out MythicRiftCompletionVendorStockEntry stockEntry) == false)
            {
                Game.ChatManager?.SendChatFromCustomSystem(
                    this,
                    "[Mythic Rift] Rift artifact vendor purchase failed: this offer is no longer available.",
                    showSender: false);
                return true;
            }

            if (Game.MythicRiftManager.TryPurchaseCompletionArtifactVendorItem(this, vendor, stockEntry.OfferId, stockEntry.RewardItemProtoRef, out MythicRiftRewardShopPurchaseResult result))
                return true;

            Game.ChatManager?.SendChatFromCustomSystem(
                this,
                $"[Mythic Rift] Rift artifact vendor purchase failed: {result.ErrorMessage}",
                showSender: false);
            return true;
        }

        private void CleanupTrackedMythicRiftCompletionVendorOffers()
        {
            if (_mythicRiftCompletionVendorOfferItemIds.Count == 0)
                return;

            List<ulong> staleItemIds = null;
            foreach (ulong itemId in _mythicRiftCompletionVendorOfferItemIds.Keys)
            {
                Item item = Game?.EntityManager.GetEntity<Item>(itemId);
                if (item == null || item.IsScheduledToDestroy)
                    (staleItemIds ??= new()).Add(itemId);
            }

            if (staleItemIds == null)
                return;

            foreach (ulong itemId in staleItemIds)
                _mythicRiftCompletionVendorOfferItemIds.Remove(itemId);
        }

        private bool TryInitializeMythicRiftCompletionCrafterInventory(PrototypeId inventoryProtoRef)
        {
            if (inventoryProtoRef == PrototypeId.Invalid || Game?.MythicRiftManager == null)
                return false;

            WorldEntity dialogTarget = GetDialogTarget(false);
            if (dialogTarget == null || Game.MythicRiftManager.IsCompletionCrafter(dialogTarget) == false)
                return false;

            PrototypeId vendorTypeProtoRef = dialogTarget.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProto?.ContainsInventory(inventoryProtoRef) != true)
                return false;

            EnsureMythicRiftCompletionCrafterStock(dialogTarget);
            return true;
        }

        private bool EnsureMythicRiftCompletionCrafterStock(WorldEntity vendor)
        {
            if (vendor == null || Game?.MythicRiftManager?.IsCompletionCrafter(vendor) != true)
                return false;

            PrototypeId vendorTypeProtoRef = vendor.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProto == null)
                return true;

            bool stocked = TryAddMythicRiftCompletionCrafterRecipe(vendorTypeProto, vendorTypeProtoRef);
            if (stocked)
                SendMessage(NetMessageVendorRefresh.CreateBuilder().SetVendorTypeProtoId((ulong)vendorTypeProtoRef).Build());

            TryPostMythicRiftOmegaForgePrompt(vendor);
            return true;
        }

        private bool TryPostMythicRiftCompletionEnchanterPrompt(WorldEntity vendor)
        {
            if (vendor == null || Game?.MythicRiftManager?.IsCompletionEnchanterOrRewardRoomEnchanter(vendor) != true)
                return false;

            return TryPostMythicRiftOmegaForgePrompt(vendor);
        }

        private bool TryAddMythicRiftCompletionCrafterRecipe(VendorTypePrototype vendorTypeProto, PrototypeId vendorTypeProtoRef)
        {
            if (vendorTypeProto == null || vendorTypeProto.IsCrafter == false)
                return false;

            using var inventoryListHandle = ListPool<PrototypeId>.Get(out List<PrototypeId> inventoryList);
            if (vendorTypeProto.GetInventories(inventoryList) == false)
                return false;

            CleanupTrackedMythicRiftCompletionCrafterRecipes();
            bool filteredInventories = FilterMythicRiftCompletionCrafterInventories(vendorTypeProto, inventoryList);

            using var ingredientSetHandle = HashSetPool<PrototypeId>.Get(out HashSet<PrototypeId> craftingIngredientSet);
            EntityManager entityManager = Game.EntityManager;
            bool addedAny = false;

            using var recipeRefsHandle = ListPool<PrototypeId>.Get(out List<PrototypeId> recipeProtoRefs);
            recipeProtoRefs.AddRange(Game.MythicRiftManager.CompletionCrafterRecipePrototypeRefs);
            AddOmegaForgeChallengeRecipePrototypeRefs(recipeProtoRefs);

            foreach (PrototypeId recipeProtoRef in recipeProtoRefs)
            {
                CraftingRecipePrototype recipeProto = recipeProtoRef.As<CraftingRecipePrototype>();
                if (recipeProtoRef == PrototypeId.Invalid || recipeProto == null)
                {
                    Logger.Warn($"TryAddMythicRiftCompletionCrafterRecipe(): completion recipe did not resolve: {recipeProtoRef.GetNameFormatted()}");
                    continue;
                }

                if (HasTrackedMythicRiftCompletionCrafterRecipe(inventoryList, recipeProtoRef))
                {
                    InitializeCraftingIngredientAvailable(recipeProto, craftingIngredientSet);
                    continue;
                }

                foreach (PrototypeId inventoryProtoRef in inventoryList)
                {
                    Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                    if (inventory == null)
                        continue;

                    uint slot = inventory.GetFreeSlot(null, false);
                    if (slot == Inventory.InvalidSlot)
                        continue;

                    ItemSpec itemSpec = Game.LootManager.CreateItemSpec(recipeProtoRef, LootContext.Vendor, this);
                    if (itemSpec == null)
                        {
                            Logger.Warn($"TryAddMythicRiftCompletionCrafterRecipe(): Failed to create ItemSpec for {recipeProtoRef.GetNameFormatted()}");
                            return false;
                        }

                    using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
                    settings.EntityRef = recipeProtoRef;
                    settings.ItemSpec = itemSpec;
                    settings.InventoryLocation = new(Id, inventory.PrototypeDataRef, slot);
                    settings.OptionFlags |= EntitySettingsOptionFlags.DoNotAllowStackingOnCreate;

                    if (IsInGame == false)
                        settings.OptionFlags &= ~EntitySettingsOptionFlags.EnterGame;

                    Item recipeItem = entityManager.CreateEntity(settings) as Item;
                    if (recipeItem == null)
                        {
                            Logger.Warn("TryAddMythicRiftCompletionCrafterRecipe(): recipeItem == null");
                            return false;
                        }

                    _mythicRiftCompletionCrafterRecipeItemIds.Add(recipeItem.Id);
                    InitializeCraftingIngredientAvailable(recipeProto, craftingIngredientSet);
                    _initializedVendorTypeProtoRefs.Remove(vendorTypeProtoRef);
                    addedAny = true;
                    Logger.Trace($"[MythicRiftCompletionCrafter] Added recipe={recipeProtoRef.GetNameFormatted()} vendorType={vendorTypeProto.DataRef.GetNameFormatted()} inventory={inventory.PrototypeDataRef.GetNameFormatted()} itemId=0x{recipeItem.Id:X}");
                    break;
                }
            }

            UpdateCraftingIngredientAvailableStackCounts(craftingIngredientSet);
            if (addedAny == false && HasAnyTrackedMythicRiftCompletionCrafterRecipe(inventoryList) == false)
                Logger.Warn("TryAddMythicRiftCompletionCrafterRecipe(): No free completion crafter slot or no completion recipes resolved.");

            if (addedAny)
                Logger.Info($"[MythicRiftCompletionCrafter] Stocked completion crafter vendorType={vendorTypeProto.DataRef.GetNameFormatted()} recipeCount={recipeProtoRefs.Count}");

            return addedAny || filteredInventories;
        }

        private bool TryBlockMythicRiftCompletionCrafterReroll(PrototypeId vendorTypeProtoRef, bool isInitializing)
        {
            if (Game?.MythicRiftManager?.IsCompletionCrafterType(vendorTypeProtoRef) != true)
                return false;

            if (Game.MythicRiftManager.IsCompletionCrafter(GetDialogTarget(false)) == false)
                return false;

            if (isInitializing == false)
                SendMessage(NetMessageVendorRefresh.CreateBuilder().SetVendorTypeProtoId((ulong)vendorTypeProtoRef).Build());

            return true;
        }

        private bool TryBlockMythicRiftCompletionVendorReroll(PrototypeId vendorTypeProtoRef, bool isInitializing)
        {
            if (Game?.MythicRiftManager?.IsCompletionArtifactVendorType(vendorTypeProtoRef) != true)
                return false;

            if (Game.MythicRiftManager.IsCompletionArtifactVendor(GetDialogTarget(false)) == false)
                return false;

            if (isInitializing == false)
                SendMessage(NetMessageVendorRefresh.CreateBuilder().SetVendorTypeProtoId((ulong)vendorTypeProtoRef).Build());

            return true;
        }

        private bool FilterMythicRiftCompletionCrafterInventories(VendorTypePrototype vendorTypeProto, List<PrototypeId> inventoryList)
        {
            if (_mythicRiftCompletionCrafterInventoriesFiltered && IsMythicRiftCompletionCrafterAlreadyIsolated(inventoryList))
                return false;

            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                inventory?.DestroyContained();
            }

            _mythicRiftCompletionCrafterRecipeItemIds.Clear();
            _mythicRiftCompletionCrafterInventoriesFiltered = true;
            _initializedVendorTypeProtoRefs.Remove(vendorTypeProto.DataRef);
            return true;
        }

        private bool IsMythicRiftCompletionCrafterAlreadyIsolated(List<PrototypeId> inventoryList)
        {
            bool foundTrackedRecipe = false;
            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                foreach (var entry in inventory)
                {
                    Item item = Game.EntityManager.GetEntity<Item>(entry.Id);
                    if (item == null || item.IsScheduledToDestroy)
                        continue;

                    if (_mythicRiftCompletionCrafterRecipeItemIds.Contains(item.Id))
                    {
                        foundTrackedRecipe = true;
                        continue;
                    }

                    return false;
                }
            }

            return foundTrackedRecipe;
        }

        private bool HasTrackedMythicRiftCompletionCrafterRecipe(Inventory inventory)
        {
            if (inventory == null)
                return false;

            foreach (var entry in inventory)
            {
                Item item = Game.EntityManager.GetEntity<Item>(entry.Id);
                if (item != null &&
                    _mythicRiftCompletionCrafterRecipeItemIds.Contains(item.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasTrackedMythicRiftCompletionCrafterRecipe(List<PrototypeId> inventoryList, PrototypeId recipeProtoRef)
        {
            if (inventoryList == null || recipeProtoRef == PrototypeId.Invalid)
                return false;

            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                foreach (var entry in inventory)
                {
                    Item item = Game.EntityManager.GetEntity<Item>(entry.Id);
                    if (item != null &&
                        item.PrototypeDataRef == recipeProtoRef &&
                        _mythicRiftCompletionCrafterRecipeItemIds.Contains(item.Id))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasAnyTrackedMythicRiftCompletionCrafterRecipe(List<PrototypeId> inventoryList)
        {
            if (inventoryList == null)
                return false;

            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory != null && HasTrackedMythicRiftCompletionCrafterRecipe(inventory))
                    return true;
            }

            return false;
        }

        private void CleanupTrackedMythicRiftCompletionCrafterRecipes()
        {
            _mythicRiftCompletionCrafterRecipeItemIds.RemoveWhere(itemId =>
            {
                Item item = Game?.EntityManager.GetEntity<Item>(itemId);
                return item == null || item.IsScheduledToDestroy;
            });
        }

        private bool IsMythicRiftCompletionCrafterRecipe(Item recipeItem, WorldEntity vendor)
        {
            if (recipeItem == null || vendor == null || Game?.MythicRiftManager?.IsCompletionCrafter(vendor) != true)
                return false;

            CleanupTrackedMythicRiftCompletionCrafterRecipes();
            return _mythicRiftCompletionCrafterRecipeItemIds.Contains(recipeItem.Id) &&
                   Game.MythicRiftManager.IsCompletionCrafterRecipe(recipeItem.PrototypeDataRef);
        }

        private bool TryAddMythicRiftVendorItem(VendorTypePrototype vendorTypeProto)
        {
            if (vendorTypeProto == null) { Logger.Warn("TryAddMythicRiftVendorItem(): vendorTypeProto == null"); return false; }

            PrototypeId standardItemProtoRef = Game.MythicRiftLauncherService.ResolveChosenBeaconPrototypeRef();
            if (standardItemProtoRef == PrototypeId.Invalid)
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): Failed to resolve {MythicRiftLauncherService.CosmicRiftBeaconPrototypeName}");
                    return false;
                }

            PrototypeId endlessItemProtoRef = Game.MythicRiftLauncherService.ResolveEndlessBeaconPrototypeRef();
            if (endlessItemProtoRef == PrototypeId.Invalid)
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): Failed to resolve {MythicRiftLauncherService.EndlessRiftBeaconPrototypeName}");
                    return false;
                }

            PrototypeId bossGauntletItemProtoRef = Game.MythicRiftLauncherService.ResolveBossGauntletBeaconPrototypeRef();
            if (bossGauntletItemProtoRef == PrototypeId.Invalid)
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): Failed to resolve {MythicRiftLauncherService.BossGauntletRiftBeaconPrototypeName}");
                    return false;
                }

            using var inventoryHandle = ListPool<Inventory>.Get(out List<Inventory> inventories);
            GetMythicRiftVendorInventories(vendorTypeProto, inventories);
            if (inventories.Count == 0)
                return false;

            CleanupTrackedMythicRiftVendorItems();

            bool addedAny = false;
            foreach (Inventory inventory in inventories)
            {
                addedAny |= TryAddMythicRiftVendorItemToInventory(vendorTypeProto, standardItemProtoRef, inventory, MythicRiftMode.Standard);
                addedAny |= TryAddMythicRiftVendorItemToInventory(vendorTypeProto, endlessItemProtoRef, inventory, MythicRiftMode.Endless);
                addedAny |= TryAddMythicRiftVendorItemToInventory(vendorTypeProto, bossGauntletItemProtoRef, inventory, MythicRiftMode.BossGauntlet);
            }

            return addedAny;
        }

        private bool TryAddMythicRiftVendorItemToInventory(
            VendorTypePrototype vendorTypeProto,
            PrototypeId itemProtoRef,
            Inventory inventory,
            MythicRiftMode mode)
        {
            if (inventory == null)
                return false;

            PrototypeId presentationProtoRef = MythicRiftItemPresentation.ResolvePresentationPrototypeRef(mode);
            foreach (var entry in inventory)
            {
                Item existingItem = Game.EntityManager.GetEntity<Item>(entry.Id);
                if (existingItem != null &&
                    Game.MythicRiftLauncherService.IsLauncherPrototypeForMode(existingItem.PrototypeDataRef, mode))
                {
                    if (presentationProtoRef != PrototypeId.Invalid && existingItem.PrototypeDataRef != presentationProtoRef)
                    {
                        Logger.Info(
                            $"[MythicRiftVendor] Replacing existing {mode} launcher stock " +
                            $"{existingItem.PrototypeDataRef.GetNameFormatted()} with presentation {presentationProtoRef.GetNameFormatted()}.");
                        existingItem.Destroy();
                        continue;
                    }

                    _mythicRiftVendorItemIds.Add(existingItem.Id);
                    return false;
                }
            }

            uint slot = inventory.GetFreeSlot(null, false);
            if (slot == Inventory.InvalidSlot)
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): No free vendor slot available in {inventory.PrototypeDataRef.GetNameFormatted()}");
                    return false;
                }

            ItemSpec itemSpec = Game.LootManager.CreateItemSpec(itemProtoRef, LootContext.Vendor, this);
            if (itemSpec == null)
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): Failed to create ItemSpec for {itemProtoRef.GetNameFormatted()}");
                    return false;
                }

            itemSpec = MythicRiftItemPresentation.ApplyLauncherPresentation(itemSpec, mode);

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = itemSpec.ItemProtoRef;
            settings.ItemSpec = itemSpec;

            if (IsInGame == false)
                settings.OptionFlags &= ~EntitySettingsOptionFlags.EnterGame;

            Item item = Game.EntityManager.CreateEntity(settings) as Item;
            if (item == null)
                {
                    Logger.Warn("TryAddMythicRiftVendorItem(): item == null");
                    return false;
                }

            InventoryResult inventoryResult = item.ChangeInventoryLocation(inventory, slot);
            if (inventoryResult != InventoryResult.Success)
            {
                item.Destroy();
                {
                    Logger.Warn($"TryAddMythicRiftVendorItem(): Failed to add item to {inventory} for reason {inventoryResult}");
                    return false;
                }
            }

            _mythicRiftVendorItemIds.Add(item.Id);
            Logger.Info($"[MythicRiftVendor] Added {item.PrototypeDataRef.GetNameFormatted()} to vendorType={vendorTypeProto.DataRef.GetNameFormatted()} inventory={inventory.PrototypeDataRef.GetNameFormatted()} technicalBase={itemProtoRef.GetNameFormatted()} riftMode={mode}");
            return true;
        }

        private void GetMythicRiftVendorInventories(VendorTypePrototype vendorTypeProto, List<Inventory> inventories)
        {
            if (vendorTypeProto == null || inventories == null)
                return;

            using var inventoryListHandle = ListPool<PrototypeId>.Get(out List<PrototypeId> inventoryList);
            if (vendorTypeProto.GetInventories(inventoryList) == false)
                return;

            using var fallbackHandle = ListPool<Inventory>.Get(out List<Inventory> fallbackInventories);

            foreach (PrototypeId inventoryProtoRef in inventoryList)
            {
                Inventory inventory = GetInventoryByRef(inventoryProtoRef);
                if (inventory == null)
                    continue;

                fallbackInventories.Add(inventory);

                if (inventory.Prototype?.VendorInvContentsCanBeBought == true)
                    inventories.Add(inventory);
            }

            if (inventories.Count > 0)
                return;

            inventories.AddRange(fallbackInventories);
        }

        private bool IsMythicRiftVendorItem(Item item, WorldEntity vendor)
        {
            if (item == null || vendor == null)
                return false;

            if (ShouldInjectMythicRiftVendorStock(vendor) == false)
                return false;

            if (Game.MythicRiftLauncherService.IsChosenBeaconPrototype(item.PrototypeDataRef))
                return true;

            CleanupTrackedMythicRiftVendorItems();
            return _mythicRiftVendorItemIds.Contains(item.Id);
        }

        private void CleanupTrackedMythicRiftVendorItems()
        {
            _mythicRiftVendorItemIds.RemoveWhere(itemId => Game?.EntityManager.GetEntity<Item>(itemId) == null);
        }

        private void TrySendMythicRiftVendorHint()
        {
            if (_mythicRiftVendorHintSent || Game?.ChatManager == null)
                return;

            _mythicRiftVendorHintSent = true;
            Game.ChatManager.SendChatFromCustomSystem(this, MythicRiftVendorHint, showSender: false);
        }
    }
}
