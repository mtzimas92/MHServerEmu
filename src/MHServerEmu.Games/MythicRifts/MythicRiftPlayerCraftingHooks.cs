using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Inventories;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.Entities
{
    public partial class Player
    {
        private const string MythicRiftCompletionCrafterCosmicRarityPrototypeName = "Entity/Items/Rarity/R5Cosmic.prototype";
        private const string MythicRiftCompletionCrafterUniqueRarityPrototypeName = "Entity/Items/Rarity/R6Unique.prototype";
        private static PrototypeId _mythicRiftCompletionCrafterCosmicRarityProtoRef = PrototypeId.Invalid;
        private static PrototypeId _mythicRiftCompletionCrafterUniqueRarityProtoRef = PrototypeId.Invalid;

        private bool TryHandleMythicRiftCompletionCraft(
            CraftingRecipePrototype recipeProto,
            Item recipeItem,
            List<ulong> ingredientIds,
            WorldEntity vendor,
            Inventory resultsInv,
            bool isRecraft,
            out CraftingResult craftingResult)
        {
            craftingResult = CraftingResult.CraftingFailed;
            if (recipeProto == null || IsMythicRiftCompletionCrafterRecipe(recipeItem, vendor) == false)
                return false;

            if (Game?.MythicRiftManager?.TryResolveCompletionCrafterRun(vendor, DatabaseUniqueId, out MythicRiftRunState runState) != true)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] This completion crafter is not available for your current Rift reward window.", showSender: false);
                return true;
            }

            if (isRecraft == false && resultsInv?.Count > 0)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Clear the crafting result slot before using the completion crafter.", showSender: false);
                return true;
            }

            Item sourceItem = ResolveMythicRiftCompletionCraftSourceItem(ingredientIds, resultsInv, isRecraft);
            if (sourceItem == null)
            {
                craftingResult = CraftingResult.IngredientInvalid;
                return true;
            }

            if (sourceItem.GetOwnerOfType<Player>() != this)
                return true;

            int sourceLevel = GetMythicRiftCompletionCraftItemLevel(sourceItem);
            if (IsMythicRiftCompletionCraftEligibleTarget(sourceItem, sourceLevel) == false)
            {
                craftingResult = CraftingResult.IngredientLevelRestricted;
                Game.ChatManager?.SendChatFromCustomSystem(
                    this,
                    $"[Mythic Rift] Completion crafter accepts only item level {Game.MythicRiftManager.CompletionCrafterMinimumItemLevel}-{Game.MythicRiftManager.CompletionCrafterMaximumItemLevel - 1} Unique or {Game.MythicRiftManager.CompletionCrafterCosmicMinimumItemLevel}-{Game.MythicRiftManager.CompletionCrafterMaximumItemLevel - 1} Cosmic gear in slots 1-5.",
                    showSender: false);
                return true;
            }

            uint outputSlot = resultsInv.GetFreeSlot(null, false);
            if (outputSlot == Inventory.InvalidSlot)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Clear the crafting result slot before using the completion crafter.", showSender: false);
                return true;
            }

            // This recipe's own native CraftingCost fields are never consulted for this flow (see
            // GetCraftingCost()'s callers in Player.cs/Item.cs - neither is ever reached here since this
            // whole method returns before Craft() gets to them), so the cost has to be checked and charged
            // directly here instead. Checked before spending an attempt below so a player who can't afford
            // it doesn't burn one of their 3 attempts for nothing.
            PrototypeId currencyProtoRef = Game.MythicRiftManager.CompletionCrafterCurrencyProtoRef;
            uint currencyCost = Game.MythicRiftManager.GetCompletionCrafterCurrencyCost(recipeItem.PrototypeDataRef);
            int availableCurrency = currencyProtoRef != PrototypeId.Invalid ? Properties[PropertyEnum.Currency, currencyProtoRef] : 0;
            if (currencyProtoRef != PrototypeId.Invalid && currencyCost > 0 && availableCurrency < currencyCost)
            {
                craftingResult = CraftingResult.InsufficientIngredients;   // generic error code, same as the native "other currencies" fallback in Item.cs
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Not enough Champion's Commendations. A successful upgrade costs {currencyCost}; available={availableCurrency}.", showSender: false);
                return true;
            }

            if (Game.MythicRiftManager.TrySpendCompletionCrafterAttempt(this, out bool upgraded, out int attemptsRemaining, out string failureReason) == false)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] {failureReason}", showSender: false);
                return true;
            }

            if (upgraded == false)
            {
                Game.ChatManager?.SendChatFromCustomSystem(
                    this,
                    $"[Mythic Rift] Upgrade attempt failed. Attempts remaining={attemptsRemaining}.",
                    showSender: false);
                return true;
            }

            // Charged only on a successful upgrade - a failed attempt still costs one of the 3 tries
            // above, but not the currency.
            if (currencyProtoRef != PrototypeId.Invalid && currencyCost > 0)
                Properties.AdjustProperty(-(int)currencyCost, new(PropertyEnum.Currency, currencyProtoRef));

            int outputLevel = Math.Min(sourceLevel + 1, Game.MythicRiftManager.CompletionCrafterMaximumItemLevel);
            Item outputItem = CreateMythicRiftCompletionCraftOutput(sourceItem, resultsInv, outputLevel);
            if (outputItem == null)
            {
                craftingResult = CraftingResult.CraftingFailed;
                return true;
            }

            int quantity = sourceItem.IsRelic ? sourceItem.CurrentStackSize : 1;
            sourceItem.DecrementStack(quantity);

            Region region = GetRegion();
            RarityPrototype rarityProto = outputItem.RarityPrototype;
            int count = outputItem.CurrentStackSize;
            region?.PlayerCraftedItemEvent.Invoke(new(this, outputItem, recipeProto.DataRef, count));
            OnScoringEvent(new(ScoringEventType.ItemCrafted, recipeProto, rarityProto, count));

            Game.ChatManager?.SendChatFromCustomSystem(
                this,
                $"[Mythic Rift] Completion crafter upgraded {outputItem.PrototypeDataRef.GetNameFormatted()} from item level {sourceLevel} to {outputLevel}. This run's upgrade is complete.",
                showSender: false);

            Logger.Info($"[MythicRiftCompletionCrafter] Crafted upgrade playerDbId=0x{DatabaseUniqueId:X} runId={runState.Config.RunId} source={sourceItem.PrototypeDataRef.GetNameFormatted()} sourceLevel={sourceLevel} outputLevel={outputLevel}");
            craftingResult = CraftingResult.Success;
            return true;
        }

        private bool CanCraftMythicRiftCompletionRecipe(Item recipeItem, WorldEntity vendor)
        {
            return IsMythicRiftCompletionCrafterRecipe(recipeItem, vendor) &&
                   Game?.MythicRiftManager?.TryResolveCompletionCrafterRun(vendor, DatabaseUniqueId, out _) == true;
        }

        private Item ResolveMythicRiftCompletionCraftSourceItem(List<ulong> ingredientIds, Inventory resultsInv, bool isRecraft)
        {
            if (ingredientIds != null)
            {
                foreach (ulong ingredientId in ingredientIds)
                {
                    if (ingredientId == InvalidId)
                        continue;

                    Item ingredient = Game.EntityManager.GetEntity<Item>(ingredientId);
                    if (ingredient == null)
                        continue;

                    if (Game.MythicRiftManager.IsCompletionCrafterRecipe(ingredient.PrototypeDataRef))
                        continue;

                    return ingredient;
                }
            }

            if (isRecraft && resultsInv != null)
            {
                ulong recraftItemId = resultsInv.GetEntityInSlot(0);
                if (recraftItemId != InvalidId)
                    return Game.EntityManager.GetEntity<Item>(recraftItemId);
            }

            return null;
        }

        private bool IsMythicRiftCompletionCraftEligibleTarget(Item item, int itemLevel)
        {
            if (item == null || item.IsCraftingRecipe || item.ItemPrototype == null)
                return false;

            if (item.ItemPrototype is not ArmorPrototype armorProto)
                return false;

            PrototypeId rarityProtoRef = item.ItemSpec?.RarityProtoRef ?? PrototypeId.Invalid;
            if (rarityProtoRef == PrototypeId.Invalid)
                rarityProtoRef = item.Properties[PropertyEnum.ItemRarity];

            if (TryGetMythicRiftCompletionCraftEligibleRarity(rarityProtoRef, out bool isCosmic) == false)
                return false;

            EquipmentInvUISlot slot = armorProto.GetInventorySlotForAgent(CurrentAvatar?.AvatarPrototype);
            if (slot == EquipmentInvUISlot.Invalid)
                slot = armorProto.DefaultEquipmentSlot;

            if (slot < EquipmentInvUISlot.Gear01 || slot > EquipmentInvUISlot.Gear05)
                return false;

            int minimumItemLevel = isCosmic
                ? Game.MythicRiftManager.CompletionCrafterCosmicMinimumItemLevel
                : Game.MythicRiftManager.CompletionCrafterMinimumItemLevel;

            return itemLevel >= minimumItemLevel &&
                   itemLevel < Game.MythicRiftManager.CompletionCrafterMaximumItemLevel;
        }

        private static bool TryGetMythicRiftCompletionCraftEligibleRarity(PrototypeId rarityProtoRef, out bool isCosmic)
        {
            isCosmic = false;

            if (rarityProtoRef == PrototypeId.Invalid)
                return false;

            if (_mythicRiftCompletionCrafterCosmicRarityProtoRef == PrototypeId.Invalid)
                _mythicRiftCompletionCrafterCosmicRarityProtoRef = GameDatabase.GetPrototypeRefByName(MythicRiftCompletionCrafterCosmicRarityPrototypeName);

            if (_mythicRiftCompletionCrafterUniqueRarityProtoRef == PrototypeId.Invalid)
                _mythicRiftCompletionCrafterUniqueRarityProtoRef = GameDatabase.GetPrototypeRefByName(MythicRiftCompletionCrafterUniqueRarityPrototypeName);

            if (rarityProtoRef == _mythicRiftCompletionCrafterCosmicRarityProtoRef ||
                rarityProtoRef == GameDatabase.LootGlobalsPrototype.RarityCosmic)
            {
                isCosmic = true;
                return true;
            }

            if (rarityProtoRef == _mythicRiftCompletionCrafterUniqueRarityProtoRef ||
                rarityProtoRef == GameDatabase.LootGlobalsPrototype.RarityUnique)
            {
                return true;
            }

            return TryGetMythicRiftCompletionCraftEligibleRarityName(GameDatabase.GetPrototypeName(rarityProtoRef), out isCosmic);
        }

        private static bool TryGetMythicRiftCompletionCraftEligibleRarityName(string rarityPrototypeName, out bool isCosmic)
        {
            isCosmic = false;
            if (string.IsNullOrWhiteSpace(rarityPrototypeName))
                return false;

            string normalizedName = rarityPrototypeName.Trim().Replace('\\', '/');
            if (normalizedName.EndsWith("/R5Cosmic.prototype", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedName, "R5Cosmic.prototype", StringComparison.OrdinalIgnoreCase))
            {
                isCosmic = true;
                return true;
            }

            return normalizedName.EndsWith("/R6Unique.prototype", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedName, "R6Unique.prototype", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetMythicRiftCompletionCraftItemLevel(Item item)
        {
            if (item == null)
                return 0;

            int propertyLevel = item.Properties[PropertyEnum.ItemLevel];
            return propertyLevel > 0 ? propertyLevel : Math.Max(1, item.ItemSpec.ItemLevel);
        }

        private Item CreateMythicRiftCompletionCraftOutput(Item sourceItem, Inventory resultsInv, int outputLevel)
        {
            if (sourceItem == null || resultsInv == null)
                return null;

            ItemSpec outputSpec = new(sourceItem.ItemSpec)
            {
                ItemLevel = outputLevel,
                StackCount = sourceItem.IsRelic ? sourceItem.CurrentStackSize : 1
            };

            uint outputSlot = resultsInv.GetFreeSlot(null, false);
            if (outputSlot == Inventory.InvalidSlot)
                return null;

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = outputSpec.ItemProtoRef;
            settings.ItemSpec = outputSpec;
            settings.InventoryLocation = new(Id, resultsInv.PrototypeDataRef, outputSlot);
            settings.OptionFlags |= EntitySettingsOptionFlags.DoNotAllowStackingOnCreate;

            if (IsInGame == false)
                settings.OptionFlags &= ~EntitySettingsOptionFlags.EnterGame;

            using var propertiesHandle = PropertyCollectionPool.Get(out PropertyCollection properties);
            settings.Properties = properties;
            properties[PropertyEnum.InventoryStackCount] = outputSpec.StackCount;

            Item outputItem = Game.EntityManager.CreateEntity(settings) as Item;
            if (outputItem == null)
                return null;

            if (outputItem.InventoryLocation.InventoryRef != resultsInv.PrototypeDataRef)
            {
                outputItem.Destroy();
                return null;
            }

            outputItem.Properties.CopyProperty(sourceItem.Properties, PropertyEnum.ItemLimitedEdition);
            return outputItem;
        }
    }
}
