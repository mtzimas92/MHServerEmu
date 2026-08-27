using Gazillion;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Inventories;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Dialog;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.UI;

namespace MHServerEmu.Games.Entities
{
    public partial class Player
    {
        private const string OmegaForgeRarityPrototypeName = "Entity/Items/Rarity/R6Omega.prototype";
        private const ulong OmegaForgeGearPage1Locale = 18000000000000040100UL;
        private const ulong OmegaForgeGearPage2Locale = 18000000000000040101UL;
        private const ulong OmegaForgeGearPage3Locale = 18000000000000040102UL;
        private const ulong OmegaForgeBonusLocale = 18000000000000040103UL;
        private const ulong OmegaForgeSpecificPage1Locale = 18000000000000040104UL;
        private const ulong OmegaForgeSpecificPage2Locale = 18000000000000040105UL;
        private const ulong OmegaForgeSpecificPage3Locale = 18000000000000040106UL;
        private const ulong OmegaForgeGear1ButtonLocale = 18000000000000040107UL;
        private const ulong OmegaForgeGear2ButtonLocale = 18000000000000040108UL;
        private const ulong OmegaForgeGear3ButtonLocale = 18000000000000040109UL;
        private const ulong OmegaForgeGear4ButtonLocale = 18000000000000040110UL;
        private const ulong OmegaForgeGear5ButtonLocale = 18000000000000040111UL;
        private const ulong OmegaForgeMoreButtonLocale = 18000000000000040112UL;
        private const ulong OmegaForgeBackButtonLocale = 18000000000000040113UL;
        private const ulong OmegaForgeCancelButtonLocale = 18000000000000040114UL;
        private const ulong OmegaForgeRandomButtonLocale = 18000000000000040115UL;
        private const ulong OmegaForgeSpecificButtonLocale = 18000000000000040116UL;
        private const ulong OmegaForgeFightingButtonLocale = 18000000000000040117UL;
        private const ulong OmegaForgeStrengthButtonLocale = 18000000000000040118UL;
        private const ulong OmegaForgeEnergyButtonLocale = 18000000000000040119UL;
        private const ulong OmegaForgeSpeedButtonLocale = 18000000000000040120UL;
        private const ulong OmegaForgeIntelligenceButtonLocale = 18000000000000040121UL;
        private const ulong OmegaForgeDurabilityButtonLocale = 18000000000000040122UL;
        private const ulong OmegaForgeActionLocale = 18000000000000040123UL;
        private const ulong OmegaForgeChallengeButtonLocale = 18000000000000040124UL;
        private const ulong OmegaForgeEnchantButtonLocale = 18000000000000040125UL;
        private const ulong OmegaForgeRecipeLocale = 18000000000000040126UL;
        private const ulong OmegaForgeNoRecipesLocale = 18000000000000040127UL;
        private const ulong OmegaForgeApplyRecipeButtonLocale = 18000000000000040128UL;

        private const string OmegaForgeEnchantmentsCategoryName = "UI/LocalizedInfo/CraftingPanel/TabLabelEnchantments.prototype";
        private const string OmegaForgeRunewordsCategoryName = "UI/LocalizedInfo/CraftingPanel/TabLabelRunewords.prototype";

        private static PrototypeId _omegaForgeRarityProtoRef = PrototypeId.Invalid;
        private static PrototypeId _omegaForgeEnchantmentsCategoryRef = PrototypeId.Invalid;
        private static PrototypeId _omegaForgeRunewordsCategoryRef = PrototypeId.Invalid;

        private readonly HashSet<ulong> _mythicRiftOmegaForgePromptedVendorIds = new();
        private readonly Dictionary<ulong, ulong> _mythicRiftOmegaForgeActiveDialogIds = new();
        private readonly Dictionary<ulong, EquipmentInvUISlot> _mythicRiftOmegaForgeSelectedGearSlots = new();

        private enum OmegaForgeAction
        {
            ChallengeBonus,
            EnchantOrRuneword
        }

        private enum OmegaForgeBonus
        {
            Random,
            Durability,
            Energy,
            Fighting,
            Intelligence,
            Speed,
            Strength
        }

        private static readonly Dictionary<OmegaForgeBonus, string> OmegaForgeRecipeNames = new()
        {
            [OmegaForgeBonus.Random] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusRandom.prototype",
            [OmegaForgeBonus.Durability] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusDurability.prototype",
            [OmegaForgeBonus.Energy] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusEnergy.prototype",
            [OmegaForgeBonus.Fighting] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusFighting.prototype",
            [OmegaForgeBonus.Intelligence] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusIntelligence.prototype",
            [OmegaForgeBonus.Speed] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusSpeed.prototype",
            [OmegaForgeBonus.Strength] = "Entity/Items/Crafting/Recipes/Tab3Gear/AddChallengeBonusStrength.prototype"
        };

        private static readonly (EquipmentInvUISlot Slot, ulong ButtonLocale)[] OmegaForgeGearChoices =
        [
            (EquipmentInvUISlot.Gear01, OmegaForgeGear1ButtonLocale),
            (EquipmentInvUISlot.Gear02, OmegaForgeGear2ButtonLocale),
            (EquipmentInvUISlot.Gear03, OmegaForgeGear3ButtonLocale),
            (EquipmentInvUISlot.Gear04, OmegaForgeGear4ButtonLocale),
            (EquipmentInvUISlot.Gear05, OmegaForgeGear5ButtonLocale)
        ];

        private static readonly (OmegaForgeBonus Bonus, ulong ButtonLocale)[] OmegaForgeBonusChoices =
        [
            (OmegaForgeBonus.Random, OmegaForgeRandomButtonLocale),
            (OmegaForgeBonus.Fighting, OmegaForgeFightingButtonLocale),
            (OmegaForgeBonus.Strength, OmegaForgeStrengthButtonLocale),
            (OmegaForgeBonus.Energy, OmegaForgeEnergyButtonLocale),
            (OmegaForgeBonus.Speed, OmegaForgeSpeedButtonLocale),
            (OmegaForgeBonus.Intelligence, OmegaForgeIntelligenceButtonLocale),
            (OmegaForgeBonus.Durability, OmegaForgeDurabilityButtonLocale)
        ];

        private static readonly string[] OmegaForgeRandomTokenNames =
        [
            "Entity/Items/CurrencyItems/RaidCurrency/ChallengeBonusTokenRandomEye.prototype",
            "Entity/Items/CurrencyItems/RaidCurrency/ChallengeBonusTokenRandomHeart.prototype",
            "Entity/Items/CurrencyItems/SeasonalLE/Recurring/ChallengeBonusTokenARMORDrive.prototype",
            "Entity/Items/CurrencyItems/SeasonalLE/Recurring/RandomChallengeBonusTokenDR.prototype"
        ];

        private static readonly string[] OmegaForgeSpecificTokenNames =
        [
            "Entity/Items/CurrencyItems/RaidCurrency/ChallengeBonusTokenSpecificHeart.prototype",
            "Entity/Items/CurrencyItems/RaidCurrency/ChallengeBonusTokenSpecificEye.prototype",
            "Entity/Items/CurrencyItems/RaidCurrency/SpecificChallengeBonusTokenDR.prototype"
        ];

        private bool TryUseOmegaForgeRecipeVendorItem(int avatarIndex, ulong itemId, ulong vendorId)
        {
            Item recipeItem = Game?.EntityManager.GetEntity<Item>(itemId);
            if (recipeItem?.ItemPrototype is not CraftingRecipePrototype recipeProto || recipeItem.IsScheduledToDestroy)
                return false;

            WorldEntity vendor = Game.EntityManager.GetEntity<WorldEntity>(vendorId);
            if (vendor == null || vendor.IsVendor == false)
                return false;

            if (Game.MythicRiftManager?.TryResolveCompletionOmegaForgeRun(vendor, DatabaseUniqueId, out _) != true)
                return false;

            bool isChallengeBonusRecipe = IsOmegaForgeChallengeBonusRecipe(recipeProto.DataRef, out _);
            bool isEnchantOrRunewordRecipe = IsOmegaForgeEnchantOrRunewordRecipe(recipeProto);
            if (isChallengeBonusRecipe == false && isEnchantOrRunewordRecipe == false)
                return false;

            Avatar avatar = GetActiveAvatarByIndex(avatarIndex);
            if (avatar == null || avatar.InInteractRange(vendor, InteractionMethod.Buy) == false || vendor.Id != DialogTargetId)
                return false;

            PrototypeId vendorTypeProtoRef = vendor.Properties[PropertyEnum.VendorType];
            VendorTypePrototype vendorTypeProto = vendorTypeProtoRef.As<VendorTypePrototype>();
            if (vendorTypeProto?.ContainsInventory(recipeItem.InventoryLocation.InventoryRef) != true)
                return false;

            if (TryGetSelectedOmegaForgeGearSlot(vendor, out EquipmentInvUISlot slot) == false)
            {
                PostOmegaForgeGearSelectionPrompt(vendor, 0);
                Logger.Info($"[OmegaCraftingTrace] recipe vendor item deferred until gear slot selected playerDbId=0x{DatabaseUniqueId:X} vendor={vendor.PrototypeDataRef.GetNameFormatted()} recipe={recipeProto.DataRef.GetNameFormatted()} itemId=0x{recipeItem.Id:X}");
                return true;
            }

            Logger.Info($"[OmegaCraftingTrace] crafting selected recipe from vendor item playerDbId=0x{DatabaseUniqueId:X} vendor={vendor.PrototypeDataRef.GetNameFormatted()} recipe={recipeProto.DataRef.GetNameFormatted()} slot={slot} itemId=0x{recipeItem.Id:X}");
            TryCraftOmegaForgeSelectedRecipe(vendor, slot, recipeProto.DataRef);
            return true;
        }

        private bool TryHandleOmegaForgeRecipeCraftRequest(Item recipeItem, WorldEntity vendor)
        {
            if (recipeItem?.ItemPrototype is not CraftingRecipePrototype recipeProto || vendor == null)
                return false;

            if (Game?.MythicRiftManager?.TryResolveCompletionOmegaForgeRun(vendor, DatabaseUniqueId, out _) != true)
                return false;

            bool isChallengeBonusRecipe = IsOmegaForgeChallengeBonusRecipe(recipeProto.DataRef, out _);
            bool isEnchantOrRunewordRecipe = IsOmegaForgeEnchantOrRunewordRecipe(recipeProto);
            if (isChallengeBonusRecipe == false && isEnchantOrRunewordRecipe == false)
                return false;

            if (TryGetSelectedOmegaForgeGearSlot(vendor, out EquipmentInvUISlot slot) == false)
            {
                PostOmegaForgeGearSelectionPrompt(vendor, 0);
                Logger.Info($"[OmegaCraftingTrace] craft request deferred until gear slot selected playerDbId=0x{DatabaseUniqueId:X} vendor={vendor.PrototypeDataRef.GetNameFormatted()} recipe={recipeProto.DataRef.GetNameFormatted()}");
                return true;
            }

            Logger.Info($"[OmegaCraftingTrace] crafting selected recipe from craft request playerDbId=0x{DatabaseUniqueId:X} vendor={vendor.PrototypeDataRef.GetNameFormatted()} recipe={recipeProto.DataRef.GetNameFormatted()} slot={slot}");
            TryCraftOmegaForgeSelectedRecipe(vendor, slot, recipeProto.DataRef);
            return true;
        }

        private static void AddOmegaForgeChallengeRecipePrototypeRefs(List<PrototypeId> recipeProtoRefs)
        {
            if (recipeProtoRefs == null)
                return;

            foreach (string recipeName in OmegaForgeRecipeNames.Values)
            {
                PrototypeId recipeProtoRef = GameDatabase.GetPrototypeRefByName(recipeName);
                if (recipeProtoRef != PrototypeId.Invalid && recipeProtoRefs.Contains(recipeProtoRef) == false)
                    recipeProtoRefs.Add(recipeProtoRef);
            }
        }

        private void TryPostMythicRiftOmegaForgePrompt(WorldEntity vendor)
        {
            if (vendor == null || Game?.MythicRiftManager?.IsCompletionOmegaForgeVendor(vendor) != true)
                return;

            if (Game.MythicRiftManager.TryResolveCompletionOmegaForgeRun(vendor, DatabaseUniqueId, out _) != true)
                return;

            if (HasActiveOmegaForgeDialog(vendor))
                return;

            if (TryGetSelectedOmegaForgeGearSlot(vendor, out _))
                return;

            if (_mythicRiftOmegaForgePromptedVendorIds.Add(vendor.Id))
            {
                Game.ChatManager?.SendChatFromCustomSystem(
                    this,
                    "[Mythic Rift] Omega Forge available. Choose the equipped Omega gear slot first, then select the crafting or enchantment recipe from this vendor's UI.",
                    showSender: false);
            }

            PostOmegaForgeGearSelectionPrompt(vendor, 0);
        }

        private bool TryGetSelectedOmegaForgeGearSlot(WorldEntity vendor, out EquipmentInvUISlot slot)
        {
            slot = default;
            return vendor != null && _mythicRiftOmegaForgeSelectedGearSlots.TryGetValue(vendor.Id, out slot);
        }

        private void PostOmegaForgeGearSelectionPrompt(WorldEntity vendor, int page)
        {
            if (vendor == null)
                return;

            page = Math.Clamp(page, 0, OmegaForgeGearChoices.Length - 1);
            var choice = OmegaForgeGearChoices[page];
            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = page switch
            {
                0 or 1 => (LocaleStringId)OmegaForgeGearPage1Locale,
                2 or 3 => (LocaleStringId)OmegaForgeGearPage2Locale,
                _ => (LocaleStringId)OmegaForgeGearPage3Locale
            };
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, (LocaleStringId)choice.ButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    Item sourceItem = FindEquippedOmegaForgeGearItem(choice.Slot);
                    if (sourceItem == null)
                    {
                        Game.ChatManager?.SendChatFromCustomSystem(
                            this,
                            $"[Mythic Rift] Omega Forge: no equipped Omega gear found in {choice.Slot}.",
                            showSender: false);
                        Logger.Info($"[OmegaCraftingTrace] gear selection failed playerDbId=0x{DatabaseUniqueId:X} vendor={vendor.PrototypeDataRef.GetNameFormatted()} reason=no-equipped-omega slot={choice.Slot}");
                        ClearActiveOmegaForgeDialog(vendor);
                        return;
                    }

                    _mythicRiftOmegaForgeSelectedGearSlots[vendor.Id] = choice.Slot;
                    PrimeOmegaForgeCraftingUiSelection(vendor, choice.Slot, sourceItem);
                    Game.ChatManager?.SendChatFromCustomSystem(
                        this,
                        $"[Mythic Rift] Omega Forge target set to {choice.Slot}. Now select a recipe from this vendor's UI.",
                        showSender: false);
                    ClearActiveOmegaForgeDialog(vendor);
                }
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                {
                    PostOmegaForgeGearSelectionPrompt(vendor, (page + 1) % OmegaForgeGearChoices.Length);
                }
                else
                {
                    ClearActiveOmegaForgeDialog(vendor);
                }
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private void PostOmegaForgeActionPage(WorldEntity vendor, int page)
        {
            if (vendor == null)
                return;

            page = Math.Clamp(page, 0, 1);
            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = (LocaleStringId)OmegaForgeActionLocale;
            dialog.AddButton(
                GameDialogResultEnum.eGDR_Option1,
                page == 0 ? (LocaleStringId)OmegaForgeChallengeButtonLocale : (LocaleStringId)OmegaForgeEnchantButtonLocale,
                ButtonStyle.SecondaryPositive,
                false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    OmegaForgeAction action = page == 0 ? OmegaForgeAction.ChallengeBonus : OmegaForgeAction.EnchantOrRuneword;
                    PostOmegaForgeGearPage(vendor, action, 0);
                }
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                {
                    PostOmegaForgeActionPage(vendor, (page + 1) % 2);
                }
                else
                {
                    ClearActiveOmegaForgeDialog(vendor);
                }
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private void PostOmegaForgeGearPage(WorldEntity vendor, OmegaForgeAction action, int page)
        {
            if (vendor == null)
                return;

            page = Math.Clamp(page, 0, OmegaForgeGearChoices.Length - 1);
            var choice = OmegaForgeGearChoices[page];
            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = page switch
            {
                0 or 1 => (LocaleStringId)OmegaForgeGearPage1Locale,
                2 or 3 => (LocaleStringId)OmegaForgeGearPage2Locale,
                _ => (LocaleStringId)OmegaForgeGearPage3Locale
            };
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, (LocaleStringId)choice.ButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    if (action == OmegaForgeAction.ChallengeBonus)
                        PostOmegaForgeBonusPage(vendor, choice.Slot, 0);
                    else
                        PostOmegaForgeRecipePage(vendor, choice.Slot, 0);
                }
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                {
                    PostOmegaForgeGearPage(vendor, action, (page + 1) % OmegaForgeGearChoices.Length);
                }
                else ClearActiveOmegaForgeDialog(vendor);
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private void PostOmegaForgeBonusPage(WorldEntity vendor, EquipmentInvUISlot slot, int page)
        {
            if (vendor == null)
                return;

            page = Math.Clamp(page, 0, OmegaForgeBonusChoices.Length - 1);
            var choice = OmegaForgeBonusChoices[page];
            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = choice.Bonus == OmegaForgeBonus.Random
                ? (LocaleStringId)OmegaForgeBonusLocale
                : page switch
                {
                    1 or 2 => (LocaleStringId)OmegaForgeSpecificPage1Locale,
                    3 or 4 => (LocaleStringId)OmegaForgeSpecificPage2Locale,
                    _ => (LocaleStringId)OmegaForgeSpecificPage3Locale
                };
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, (LocaleStringId)choice.ButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1) TryCraftOmegaForgeChallengeBonus(vendor, slot, choice.Bonus);
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2) PostOmegaForgeBonusPage(vendor, slot, (page + 1) % OmegaForgeBonusChoices.Length);
                else ClearActiveOmegaForgeDialog(vendor);
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private void PostOmegaForgeSelectedRecipeGearPage(WorldEntity vendor, PrototypeId recipeProtoRef, int page)
        {
            if (vendor == null || recipeProtoRef == PrototypeId.Invalid)
                return;

            page = Math.Clamp(page, 0, OmegaForgeGearChoices.Length - 1);
            var choice = OmegaForgeGearChoices[page];
            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = page switch
            {
                0 or 1 => (LocaleStringId)OmegaForgeGearPage1Locale,
                2 or 3 => (LocaleStringId)OmegaForgeGearPage2Locale,
                _ => (LocaleStringId)OmegaForgeGearPage3Locale
            };
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, (LocaleStringId)choice.ButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                    TryCraftOmegaForgeSelectedRecipe(vendor, choice.Slot, recipeProtoRef);
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                    PostOmegaForgeSelectedRecipeGearPage(vendor, recipeProtoRef, (page + 1) % OmegaForgeGearChoices.Length);
                else
                    ClearActiveOmegaForgeDialog(vendor);
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private bool TryCraftOmegaForgeSelectedRecipe(WorldEntity vendor, EquipmentInvUISlot slot, PrototypeId recipeProtoRef)
        {
            CraftingRecipePrototype recipeProto = recipeProtoRef.As<CraftingRecipePrototype>();
            if (recipeProto == null)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega Forge: selected recipe did not resolve.", showSender: false);
                Logger.Warn($"[OmegaCraftingTrace] selected-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=recipe-missing slot={slot} recipe={recipeProtoRef.GetNameFormatted()}");
                return false;
            }

            if (IsOmegaForgeChallengeBonusRecipe(recipeProtoRef, out OmegaForgeBonus bonus))
                return TryCraftOmegaForgeChallengeBonus(vendor, slot, bonus);

            if (IsOmegaForgeEnchantOrRunewordRecipe(recipeProto))
                return TryCraftOmegaForgeLearnedRecipe(vendor, slot, recipeProtoRef);

            Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega Forge: selected recipe is not supported for Omega gear.", showSender: false);
            Logger.Info($"[OmegaCraftingTrace] selected-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=unsupported-recipe slot={slot} recipe={recipeProtoRef.GetNameFormatted()}");
            return false;
        }

        private GameDialogInstance CreateOmegaForgeDialog(WorldEntity vendor)
        {
            GameDialogInstance dialog = Game.GameDialogManager.CreateInstance(DatabaseUniqueId);
            dialog.Options = DialogOptionEnum.ScreenBottom | DialogOptionEnum.InputEnabled;
            dialog.TargetId = vendor?.Id ?? InvalidId;
            dialog.InteractorId = vendor?.Id ?? InvalidId;
            return dialog;
        }

        private static bool IsOmegaForgeResultSlotBlocked(Inventory resultsInv)
        {
            return resultsInv == null || resultsInv.GetEntityInSlot(0) != InvalidId;
        }

        private void PostOmegaForgeDialog(WorldEntity vendor, GameDialogInstance dialog)
        {
            if (vendor == null || dialog == null)
                return;

            _mythicRiftOmegaForgeActiveDialogIds[vendor.Id] = dialog.ServerId;
            Game.GameDialogManager.PostDialogToClient(dialog);
        }

        private bool HasActiveOmegaForgeDialog(WorldEntity vendor)
        {
            if (vendor == null)
                return false;

            if (_mythicRiftOmegaForgeActiveDialogIds.TryGetValue(vendor.Id, out ulong dialogId) == false)
                return false;

            if (Game.GameDialogManager.GetInstance(dialogId) != null)
                return true;

            _mythicRiftOmegaForgeActiveDialogIds.Remove(vendor.Id);
            return false;
        }

        private void ClearActiveOmegaForgeDialog(WorldEntity vendor)
        {
            if (vendor != null)
                _mythicRiftOmegaForgeActiveDialogIds.Remove(vendor.Id);
        }

        private void PrimeOmegaForgeCraftingUiSelection(WorldEntity vendor, EquipmentInvUISlot slot, Item sourceItem)
        {
            if (vendor == null || sourceItem == null)
                return;

            PropertyId ingredientSlotPropertyId = BuildOmegaForgeUiPropertyId(PropertyEnum.UICraftingIngredientSlotEntityId, 0);
            PropertyId cursorSelectedPropertyId = BuildOmegaForgeUiPropertyId(PropertyEnum.UICursorSelectedEntityId, 0);

            Properties[ingredientSlotPropertyId] = sourceItem.Id;
            Properties[cursorSelectedPropertyId] = sourceItem.Id;

            Properties.SyncProperty(ingredientSlotPropertyId, out _);
            Properties.SyncProperty(cursorSelectedPropertyId, out _);

            Logger.Info(
                $"[OmegaCraftingTrace] primed crafting UI playerDbId=0x{DatabaseUniqueId:X} " +
                $"vendor={vendor.PrototypeDataRef.GetNameFormatted()} slot={slot} source={sourceItem.PrototypeDataRef.GetNameFormatted()} " +
                $"sourceId=0x{sourceItem.Id:X} ingredientProp={ingredientSlotPropertyId} cursorProp={cursorSelectedPropertyId}");
        }

        private static PropertyId BuildOmegaForgeUiPropertyId(PropertyEnum propertyEnum, int slot)
        {
            PropertyInfo propertyInfo = GameDatabase.PropertyInfoTable.LookupPropertyInfo(propertyEnum);
            if (propertyInfo != null && propertyInfo.ParamCount > 0)
                return new(propertyEnum, (PropertyParam)slot);

            return new(propertyEnum);
        }

        private bool TryCraftOmegaForgeChallengeBonus(WorldEntity vendor, EquipmentInvUISlot slot, OmegaForgeBonus bonus)
        {
            Item sourceItem = FindEquippedOmegaForgeGearItem(slot);
            if (sourceItem == null)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge: no equipped Omega gear found in {slot}.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] prompt failed playerDbId=0x{DatabaseUniqueId:X} reason=no-equipped-omega slot={slot} bonus={bonus}");
                return false;
            }

            CraftingRecipePrototype recipeProto = ResolveOmegaForgeRecipe(bonus);
            if (recipeProto == null)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge: challenge recipe for {bonus} did not resolve.", showSender: false);
                Logger.Warn($"[OmegaCraftingTrace] prompt failed playerDbId=0x{DatabaseUniqueId:X} reason=recipe-missing slot={slot} bonus={bonus}");
                return false;
            }

            Item tokenItem = FindOmegaForgeTokenForRecipe(recipeProto, sourceItem, bonus);
            if (tokenItem == null)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge: no matching {GetOmegaForgeTokenLabel(bonus)} challenge token found.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] prompt failed playerDbId=0x{DatabaseUniqueId:X} reason=no-token source={sourceItem.PrototypeDataRef.GetNameFormatted()} slot={slot} bonus={bonus}");
                return false;
            }

            Inventory resultsInv = GetInventory(InventoryConvenienceLabel.CraftingResults);
            if (IsOmegaForgeResultSlotBlocked(resultsInv))
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega Forge: clear the crafting result slot first.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] prompt failed playerDbId=0x{DatabaseUniqueId:X} reason=result-slot-blocked source={sourceItem.PrototypeDataRef.GetNameFormatted()} token={tokenItem.PrototypeDataRef.GetNameFormatted()} slot={slot} bonus={bonus}");
                return false;
            }

            CraftingResult result = CraftOmegaForgeRecipe(recipeProto, sourceItem, tokenItem, vendor, resultsInv, out Item outputItem);
            if (result != CraftingResult.Success)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge failed: {result}.", showSender: false);
                return false;
            }

            Game.ChatManager?.SendChatFromCustomSystem(
                this,
                $"[Mythic Rift] Omega Forge applied {bonus} challenge bonus to {outputItem.PrototypeDataRef.GetNameFormatted()}.",
                showSender: false);

            return true;
        }

        private void PostOmegaForgeRecipePage(WorldEntity vendor, EquipmentInvUISlot slot, int page)
        {
            if (vendor == null)
                return;

            using var recipeRefsHandle = ListPool<PrototypeId>.Get(out List<PrototypeId> recipeRefs);
            BuildAvailableOmegaForgeRecipeRefs(slot, recipeRefs);
            if (recipeRefs.Count == 0)
            {
                GameDialogInstance emptyDialog = CreateOmegaForgeDialog(vendor);
                emptyDialog.Message.LocaleString = (LocaleStringId)OmegaForgeNoRecipesLocale;
                emptyDialog.AddButton(GameDialogResultEnum.eGDR_Option1, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
                emptyDialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeCancelButtonLocale, ButtonStyle.SecondaryNegative, false);
                emptyDialog.OnResponse = (_, response) =>
                {
                    if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1) PostOmegaForgeGearPage(vendor, OmegaForgeAction.EnchantOrRuneword, 0);
                    else ClearActiveOmegaForgeDialog(vendor);
                };

                PostOmegaForgeDialog(vendor, emptyDialog);
                return;
            }

            page = Math.Clamp(page, 0, recipeRefs.Count - 1);
            PrototypeId selectedRecipeProtoRef = recipeRefs[page];
            int recipeCount = recipeRefs.Count;
            CraftingRecipePrototype recipeProto = selectedRecipeProtoRef.As<CraftingRecipePrototype>();
            LocaleStringId recipeName = recipeProto?.DisplayName ?? LocaleStringId.Blank;
            if (recipeName == LocaleStringId.Blank)
                recipeName = (LocaleStringId)OmegaForgeApplyRecipeButtonLocale;

            GameDialogInstance dialog = CreateOmegaForgeDialog(vendor);
            dialog.Message.LocaleString = (LocaleStringId)OmegaForgeRecipeLocale;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, recipeName, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, (LocaleStringId)OmegaForgeMoreButtonLocale, ButtonStyle.SecondaryPositive, false);
            dialog.OnResponse = (_, response) =>
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1) TryCraftOmegaForgeLearnedRecipe(vendor, slot, selectedRecipeProtoRef);
                else if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2) PostOmegaForgeRecipePage(vendor, slot, (page + 1) % recipeCount);
                else ClearActiveOmegaForgeDialog(vendor);
            };

            PostOmegaForgeDialog(vendor, dialog);
        }

        private void BuildAvailableOmegaForgeRecipeRefs(EquipmentInvUISlot slot, List<PrototypeId> recipeRefs)
        {
            recipeRefs.Clear();

            Item sourceItem = FindEquippedOmegaForgeGearItem(slot);
            if (sourceItem == null)
                return;

            Inventory learnedRecipeInv = GetInventory(InventoryConvenienceLabel.CraftingRecipesLearned);
            if (learnedRecipeInv == null)
                return;

            EntityManager entityManager = Game.EntityManager;
            foreach (var entry in learnedRecipeInv)
            {
                if (entry.Id == InvalidId)
                    continue;

                Item recipeItem = entityManager.GetEntity<Item>(entry.Id);
                if (recipeItem?.ItemPrototype is not CraftingRecipePrototype recipeProto || recipeItem.IsScheduledToDestroy)
                    continue;

                if (IsOmegaForgeEnchantOrRunewordRecipe(recipeProto) == false)
                    continue;

                if (TryBuildOmegaForgeRecipeIngredientIds(recipeProto, sourceItem, out _, out _) == false)
                    continue;

                recipeRefs.Add(recipeProto.DataRef);
            }
        }

        private bool TryCraftOmegaForgeLearnedRecipe(WorldEntity vendor, EquipmentInvUISlot slot, PrototypeId recipeProtoRef)
        {
            Item sourceItem = FindEquippedOmegaForgeGearItem(slot);
            if (sourceItem == null)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge: no equipped Omega gear found in {slot}.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] learned-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=no-equipped-omega slot={slot} recipe={recipeProtoRef.GetNameFormatted()}");
                return false;
            }

            CraftingRecipePrototype recipeProto = recipeProtoRef.As<CraftingRecipePrototype>();
            if (recipeProto == null || IsOmegaForgeEnchantOrRunewordRecipe(recipeProto) == false || HasLearnedCraftingRecipe(recipeProtoRef) == false)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega Forge: that enchantment recipe is not learned.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] learned-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=recipe-not-learned slot={slot} recipe={recipeProtoRef.GetNameFormatted()}");
                return false;
            }

            if (TryBuildOmegaForgeRecipeIngredientIds(recipeProto, sourceItem, out List<ulong> ingredientIds, out CraftingResult ingredientFailure) == false)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge: missing or invalid recipe ingredients ({ingredientFailure}).", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] learned-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=ingredients result={ingredientFailure} source={sourceItem.PrototypeDataRef.GetNameFormatted()} recipe={recipeProtoRef.GetNameFormatted()}");
                return false;
            }

            Inventory resultsInv = GetInventory(InventoryConvenienceLabel.CraftingResults);
            if (IsOmegaForgeResultSlotBlocked(resultsInv))
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega Forge: clear the crafting result slot first.", showSender: false);
                Logger.Info($"[OmegaCraftingTrace] learned-recipe failed playerDbId=0x{DatabaseUniqueId:X} reason=result-slot-blocked source={sourceItem.PrototypeDataRef.GetNameFormatted()} recipe={recipeProtoRef.GetNameFormatted()}");
                return false;
            }

            CraftingResult result = CraftOmegaForgeRecipe(recipeProto, ingredientIds, vendor, resultsInv, allowChallengeTokenOverride: false, out Item outputItem);
            if (result != CraftingResult.Success)
            {
                Game.ChatManager?.SendChatFromCustomSystem(this, $"[Mythic Rift] Omega Forge recipe failed: {result}.", showSender: false);
                return false;
            }

            Game.ChatManager?.SendChatFromCustomSystem(
                this,
                $"[Mythic Rift] Omega Forge crafted {recipeProto.DataRef.GetNameFormatted()} on {outputItem.PrototypeDataRef.GetNameFormatted()}.",
                showSender: false);

            return true;
        }

        private bool TryHandleOmegaChallengeBonusCraftFallback(
            CraftingRecipePrototype recipeProto,
            List<ulong> ingredientIds,
            WorldEntity vendor,
            Inventory resultsInv,
            out CraftingResult craftingResult)
        {
            craftingResult = CraftingResult.CraftingFailed;
            if (recipeProto == null || IsOmegaForgeChallengeBonusRecipe(recipeProto.DataRef, out OmegaForgeBonus bonus) == false)
                return false;

            if (TryResolveOmegaForgeChallengeIngredients(recipeProto, ingredientIds, bonus, out Item sourceItem, out Item tokenItem) == false)
                return false;

            if (IsOmegaForgeResultSlotBlocked(resultsInv))
            {
                craftingResult = CraftingResult.CraftingFailed;
                Game.ChatManager?.SendChatFromCustomSystem(this, "[Mythic Rift] Omega challenge craft: clear the crafting result slot first.", showSender: false);
                return true;
            }

            craftingResult = CraftOmegaForgeRecipe(recipeProto, sourceItem, tokenItem, vendor, resultsInv, out _);
            return true;
        }

        private CraftingResult CraftOmegaForgeRecipe(CraftingRecipePrototype recipeProto, Item sourceItem, Item tokenItem, WorldEntity vendor, Inventory resultsInv, out Item outputItem)
        {
            outputItem = null;
            if (sourceItem == null || tokenItem == null)
                return CraftingResult.CraftingFailed;

            List<ulong> ingredientIds = [sourceItem.Id, tokenItem.Id];
            return CraftOmegaForgeRecipe(recipeProto, ingredientIds, vendor, resultsInv, allowChallengeTokenOverride: true, out outputItem);
        }

        private CraftingResult CraftOmegaForgeRecipe(CraftingRecipePrototype recipeProto, List<ulong> ingredientIds, WorldEntity vendor, Inventory resultsInv, bool allowChallengeTokenOverride, out Item outputItem)
        {
            outputItem = null;
            if (recipeProto == null || ingredientIds == null || ingredientIds.Count == 0 || resultsInv == null)
                return CraftingResult.CraftingFailed;

            Item sourceItem = Game.EntityManager.GetEntity<Item>(ingredientIds[0]);
            if (sourceItem == null || sourceItem.GetOwnerOfType<Player>() != this)
                return CraftingResult.IngredientInvalid;

            if (sourceItem.ItemPrototype is not ArmorPrototype || IsOmegaForgeRarity(sourceItem) == false || IsEquippedOnCurrentAvatar(sourceItem) == false)
                return CraftingResult.IngredientInvalid;

            for (int i = 1; i < ingredientIds.Count; i++)
            {
                ulong ingredientId = ingredientIds[i];
                if (ingredientId == InvalidId)
                    continue;

                Item ingredientItem = Game.EntityManager.GetEntity<Item>(ingredientId);
                if (ingredientItem == null || ingredientItem.GetOwnerOfType<Player>() != this)
                    return CraftingResult.IngredientInvalid;
            }

            CraftingResult ingredientResult = ValidateOmegaForgeRecipeIngredients(recipeProto, ingredientIds, sourceItem);
            if (ingredientResult != CraftingResult.Success &&
                (allowChallengeTokenOverride == false || ingredientIds.Count < 2 || IsAllowedOmegaForgeToken(Game.EntityManager.GetEntity<Item>(ingredientIds[1]), recipeProto.DataRef) == false))
            {
                return ingredientResult;
            }

            using var currencyCostHandle = PropertyCollectionPool.Get(out PropertyCollection currencyCost);
            if (recipeProto.GetCraftingCost(this, ingredientIds, out uint creditsCost, out uint legendaryMarksCost, currencyCost) == false)
                return CraftingResult.InsufficientIngredients;

            CurrencyGlobalsPrototype currencyGlobals = GameDatabase.CurrencyGlobalsPrototype;
            if (creditsCost > 0 && Properties[PropertyEnum.Currency, currencyGlobals.Credits] < creditsCost)
                return CraftingResult.InsufficientCredits;

            if (legendaryMarksCost > 0 && Properties[PropertyEnum.Currency, currencyGlobals.LegendaryMarks] < legendaryMarksCost)
                return CraftingResult.InsufficientLegendaryMarks;

            foreach (var kvp in currencyCost.IteratePropertyRange(PropertyEnum.Currency))
            {
                uint cost = kvp.Value;
                if (Properties[kvp.Key] < cost)
                    return CraftingResult.InsufficientIngredients;
            }

            using var resolverHandle = ItemResolverPool.Get(out ItemResolver resolver);
            resolver.Initialize(Game.Random);
            resolver.SetContext(LootContext.Crafting, this);

            using var ingredientsHandle = ListPool<Item>.Get(out List<Item> ingredients);
            using var autoPopulatedIngredientsHandle = DictionaryPool<Item, int>.Get(out Dictionary<Item, int> autoPopulatedIngredients);
            using var outputItemsHandle = ListPool<Item>.Get(out List<Item> outputItems);

            if (CraftPrepareIngredients(recipeProto, ingredientIds, resolver, ingredients, autoPopulatedIngredients) == false)
                return CraftingResult.InsufficientIngredients;

            using var settingsHandle = LootRollSettingsPool.Get(out LootRollSettings settings);
            settings.Player = this;
            settings.UsableAvatar = CurrentAvatar?.AvatarPrototype;
            settings.Level = CurrentAvatar?.CharacterLevel ?? 1;

            const int MaxRollAttempts = 10;

            LootRollResult rollResult = LootRollResult.NoRoll;
            for (int i = 0; i < MaxRollAttempts; i++)
            {
                rollResult = recipeProto.RecipeOutput.RollLootTable(settings, resolver);
                if (rollResult == LootRollResult.Success)
                    break;

                Logger.Trace($"[OmegaCraftingTrace] loot roll failed attempt={i + 1}/{MaxRollAttempts} playerDbId=0x{DatabaseUniqueId:X} recipe={recipeProto.DataRef.GetNameFormatted()} source={sourceItem.PrototypeDataRef.GetNameFormatted()} result={rollResult}");
                resolver.SetContext(LootContext.Crafting, this);
            }

            if (rollResult != LootRollResult.Success)
            {
                Logger.Warn($"[OmegaCraftingTrace] craft failed playerDbId=0x{DatabaseUniqueId:X} reason=loot-roll recipe={recipeProto.DataRef.GetNameFormatted()} source={sourceItem.PrototypeDataRef.GetNameFormatted()} result={rollResult}");
                return CraftingResult.LootRollFailed;
            }

            using var summaryHandle = LootResultSummaryPool.Get(out LootResultSummary summary);
            resolver.FillLootResultSummary(summary);

            const LootType LootTypeFilter = LootType.Item | LootType.LootMutation | LootType.VendorXP | LootType.CallbackNode;
            if ((summary.Types & ~LootTypeFilter) != LootType.None)
                return CraftingResult.LootRollFailed;

            if (CraftCreateOutputItemsFromSummary(recipeProto, ingredients, summary, resultsInv, outputItems) == false || outputItems.Count == 0)
            {
                foreach (Item item in outputItems)
                    item?.Destroy();

                Logger.Warn($"[OmegaCraftingTrace] craft failed playerDbId=0x{DatabaseUniqueId:X} reason=create-output recipe={recipeProto.DataRef.GetNameFormatted()} source={sourceItem.PrototypeDataRef.GetNameFormatted()}");
                return CraftingResult.CraftingFailed;
            }

            outputItem = outputItems[0];
            CraftConsumeIngredients(ingredients, autoPopulatedIngredients);
            CraftPayCost(creditsCost, legendaryMarksCost, currencyCost);
            CraftProcessVendorLoot(summary, vendor);

            Logger.Info($"[OmegaCraftingTrace] craft success playerDbId=0x{DatabaseUniqueId:X} recipe={recipeProto.DataRef.GetNameFormatted()} source={sourceItem.PrototypeDataRef.GetNameFormatted()} output={outputItem.PrototypeDataRef.GetNameFormatted()} outputId=0x{outputItem.Id:X}");
            return CraftingResult.Success;
        }

        private CraftingResult ValidateOmegaForgeRecipeIngredients(CraftingRecipePrototype recipeProto, List<ulong> ingredientIds, Item sourceItem)
        {
            if (recipeProto?.RecipeInputs.IsNullOrEmpty() != false || ingredientIds == null)
                return CraftingResult.CraftingFailed;

            if (recipeProto.RecipeInputs.Length != ingredientIds.Count)
                return CraftingResult.CraftingFailed;

            using var usedStackCountsHandle = DictionaryPool<ulong, int>.Get(out Dictionary<ulong, int> usedStackCounts);
            for (int slot = 0; slot < recipeProto.RecipeInputs.Length; slot++)
            {
                CraftingResult slotResult = ValidateOmegaForgeRecipeIngredient(recipeProto, ingredientIds, slot, usedStackCounts, sourceItem);
                if (slotResult != CraftingResult.Success)
                    return slotResult;
            }

            return CraftingResult.Success;
        }

        private CraftingResult ValidateOmegaForgeRecipeIngredient(CraftingRecipePrototype recipeProto, List<ulong> ingredientIds, int slot, Dictionary<ulong, int> usedStackCounts, Item sourceItem)
        {
            if (slot == 0 &&
                sourceItem != null &&
                ingredientIds != null &&
                ingredientIds.Count > 0 &&
                ingredientIds[0] == sourceItem.Id &&
                sourceItem.ItemPrototype is ArmorPrototype &&
                IsOmegaForgeRarity(sourceItem) &&
                IsEquippedOnCurrentAvatar(sourceItem))
            {
                return CraftingResult.Success;
            }

            return recipeProto.ValidateIngredient(this, ingredientIds, slot, usedStackCounts);
        }

        private bool TryBuildOmegaForgeRecipeIngredientIds(CraftingRecipePrototype recipeProto, Item sourceItem, out List<ulong> ingredientIds, out CraftingResult failure)
        {
            ingredientIds = null;
            failure = CraftingResult.CraftingFailed;

            CraftingInputPrototype[] recipeInputs = recipeProto?.RecipeInputs;
            if (recipeInputs.IsNullOrEmpty() || sourceItem == null)
                return false;

            ingredientIds = new(recipeInputs.Length);
            for (int i = 0; i < recipeInputs.Length; i++)
                ingredientIds.Add(InvalidId);

            ingredientIds[0] = sourceItem.Id;
            using var usedStackCountsHandle = DictionaryPool<ulong, int>.Get(out Dictionary<ulong, int> usedStackCounts);

            failure = ValidateOmegaForgeRecipeIngredient(recipeProto, ingredientIds, 0, usedStackCounts, sourceItem);
            if (failure != CraftingResult.Success)
                return false;

            for (int slot = 1; slot < recipeInputs.Length; slot++)
            {
                CraftingInputPrototype inputProto = recipeInputs[slot];
                if (inputProto == null)
                    return false;

                if (inputProto.AutoPopulatedIngredientPrototype != null)
                {
                    failure = ValidateOmegaForgeRecipeIngredient(recipeProto, ingredientIds, slot, usedStackCounts, sourceItem);
                    if (failure != CraftingResult.Success)
                        return false;

                    continue;
                }

                if (TryFindOmegaForgeRecipeIngredient(recipeProto, ingredientIds, slot, usedStackCounts, out failure) == false)
                    return false;
            }

            failure = ValidateOmegaForgeRecipeIngredients(recipeProto, ingredientIds, sourceItem);
            return failure == CraftingResult.Success;
        }

        private bool TryFindOmegaForgeRecipeIngredient(CraftingRecipePrototype recipeProto, List<ulong> ingredientIds, int slot, Dictionary<ulong, int> usedStackCounts, out CraftingResult failure)
        {
            failure = CraftingResult.InsufficientIngredients;

            foreach (Inventory inventory in new InventoryIterator(this, InventoryIterationFlags.CraftingIngredients))
            {
                foreach (var entry in inventory)
                {
                    Item ingredient = Game.EntityManager.GetEntity<Item>(entry.Id);
                    if (ingredient == null || ingredient.IsScheduledToDestroy || ingredient.Id == ingredientIds[0])
                        continue;

                    int usedCount = usedStackCounts != null && usedStackCounts.TryGetValue(ingredient.Id, out int count) ? count : 0;
                    if (usedCount >= ingredient.CurrentStackSize)
                        continue;

                    ingredientIds[slot] = ingredient.Id;
                    CraftingResult result = ValidateOmegaForgeRecipeIngredient(recipeProto, ingredientIds, slot, usedStackCounts, Game.EntityManager.GetEntity<Item>(ingredientIds[0]));
                    if (result == CraftingResult.Success)
                    {
                        failure = CraftingResult.Success;
                        return true;
                    }

                    ingredientIds[slot] = InvalidId;
                    failure = result;
                }
            }

            return false;
        }

        private Item FindEquippedOmegaForgeGearItem(EquipmentInvUISlot slot)
        {
            Avatar avatar = CurrentAvatar;
            AvatarPrototype avatarProto = avatar?.AvatarPrototype;
            if (avatar == null || avatarProto?.EquipmentInventories.IsNullOrEmpty() != false)
                return null;

            foreach (AvatarEquipInventoryAssignmentPrototype assignment in avatarProto.EquipmentInventories)
            {
                if (assignment?.Inventory == null || assignment.UISlot != slot)
                    continue;

                Inventory inventory = avatar.GetInventoryByRef(assignment.Inventory.DataRef);
                if (inventory == null)
                    continue;

                ulong equippedItemId = inventory.GetEntityInSlot(0);
                Item item = equippedItemId != InvalidId ? Game.EntityManager.GetEntity<Item>(equippedItemId) : null;
                if (item != null &&
                    item.IsScheduledToDestroy == false &&
                    item.InventoryLocation.ContainerId == avatar.Id &&
                    item.InventoryLocation.InventoryRef == inventory.PrototypeDataRef &&
                    item.InventoryLocation.Slot == 0 &&
                    IsOmegaForgeRarity(item))
                {
                    return item;
                }
            }

            return null;
        }

        private bool IsEquippedOnCurrentAvatar(Item item)
        {
            Avatar avatar = CurrentAvatar;
            return item != null &&
                   avatar != null &&
                   item.InventoryLocation.ContainerId == avatar.Id &&
                   item.InventoryLocation.Slot == 0 &&
                   item.InventoryLocation.InventoryRef != PrototypeId.Invalid;
        }

        private Item FindOmegaForgeTokenForRecipe(CraftingRecipePrototype recipeProto, Item sourceItem, OmegaForgeBonus bonus)
        {
            string[] tokenNames = bonus == OmegaForgeBonus.Random ? OmegaForgeRandomTokenNames : OmegaForgeSpecificTokenNames;
            foreach (string tokenName in tokenNames)
            {
                PrototypeId tokenProtoRef = GameDatabase.GetPrototypeRefByName(tokenName);
                if (tokenProtoRef == PrototypeId.Invalid)
                    continue;

                foreach (Inventory inventory in new InventoryIterator(this, InventoryIterationFlags.CraftingIngredients))
                {
                    foreach (var entry in inventory)
                    {
                        if (entry.ProtoRef != tokenProtoRef)
                            continue;

                        Item tokenItem = Game.EntityManager.GetEntity<Item>(entry.Id);
                        if (tokenItem == null || tokenItem.IsScheduledToDestroy)
                            continue;

                        List<ulong> ingredientIds = [sourceItem.Id, tokenItem.Id];
                        if (recipeProto.ValidateIngredients(this, ingredientIds) == CraftingResult.Success ||
                            IsAllowedOmegaForgeToken(tokenItem, recipeProto.DataRef))
                        {
                            return tokenItem;
                        }
                    }
                }
            }

            return null;
        }

        private bool TryResolveOmegaForgeChallengeIngredients(CraftingRecipePrototype recipeProto, List<ulong> ingredientIds, OmegaForgeBonus bonus, out Item sourceItem, out Item tokenItem)
        {
            sourceItem = null;
            tokenItem = null;

            if (ingredientIds == null || ingredientIds.Count < 2)
                return false;

            foreach (ulong ingredientId in ingredientIds)
            {
                if (ingredientId == InvalidId)
                    continue;

                Item item = Game.EntityManager.GetEntity<Item>(ingredientId);
                if (item == null || item.IsScheduledToDestroy)
                    continue;

                if (sourceItem == null && item.ItemPrototype is ArmorPrototype && IsOmegaForgeRarity(item) && IsEquippedOnCurrentAvatar(item))
                    sourceItem = item;
                else if (tokenItem == null && IsAllowedOmegaForgeToken(item, recipeProto.DataRef))
                    tokenItem = item;
            }

            return sourceItem != null && tokenItem != null;
        }

        private static bool IsOmegaForgeEnchantOrRunewordRecipe(CraftingRecipePrototype recipeProto)
        {
            if (recipeProto == null || recipeProto.RecipeInputs.IsNullOrEmpty() || recipeProto.RecipeInputs.Length < 2)
                return false;

            if (_omegaForgeEnchantmentsCategoryRef == PrototypeId.Invalid)
                _omegaForgeEnchantmentsCategoryRef = GameDatabase.GetPrototypeRefByName(OmegaForgeEnchantmentsCategoryName);

            if (_omegaForgeRunewordsCategoryRef == PrototypeId.Invalid)
                _omegaForgeRunewordsCategoryRef = GameDatabase.GetPrototypeRefByName(OmegaForgeRunewordsCategoryName);

            PrototypeId categoryRef = recipeProto.RecipeCategory;
            return categoryRef != PrototypeId.Invalid &&
                   (categoryRef == _omegaForgeEnchantmentsCategoryRef || categoryRef == _omegaForgeRunewordsCategoryRef);
        }

        private static CraftingRecipePrototype ResolveOmegaForgeRecipe(OmegaForgeBonus bonus)
        {
            if (OmegaForgeRecipeNames.TryGetValue(bonus, out string recipeName) == false)
                return null;

            PrototypeId recipeRef = GameDatabase.GetPrototypeRefByName(recipeName);
            return recipeRef.As<CraftingRecipePrototype>();
        }

        private static bool IsOmegaForgeChallengeBonusRecipe(PrototypeId recipeRef, out OmegaForgeBonus bonus)
        {
            foreach (var kvp in OmegaForgeRecipeNames)
            {
                PrototypeId candidateRef = GameDatabase.GetPrototypeRefByName(kvp.Value);
                if (candidateRef == recipeRef)
                {
                    bonus = kvp.Key;
                    return true;
                }
            }

            bonus = OmegaForgeBonus.Random;
            return false;
        }

        private static bool IsAllowedOmegaForgeToken(Item tokenItem, PrototypeId recipeRef)
        {
            if (tokenItem == null)
                return false;

            if (IsOmegaForgeChallengeBonusRecipe(recipeRef, out OmegaForgeBonus bonus) == false)
                return false;

            string[] tokenNames = bonus == OmegaForgeBonus.Random ? OmegaForgeRandomTokenNames : OmegaForgeSpecificTokenNames;
            foreach (string tokenName in tokenNames)
            {
                PrototypeId tokenRef = GameDatabase.GetPrototypeRefByName(tokenName);
                if (tokenRef != PrototypeId.Invalid && tokenItem.PrototypeDataRef == tokenRef)
                    return true;
            }

            return false;
        }

        private static bool IsOmegaForgeRarity(Item item)
        {
            if (item == null)
                return false;

            if (_omegaForgeRarityProtoRef == PrototypeId.Invalid)
                _omegaForgeRarityProtoRef = GameDatabase.GetPrototypeRefByName(OmegaForgeRarityPrototypeName);

            PrototypeId rarityRef = item.ItemSpec?.RarityProtoRef ?? PrototypeId.Invalid;
            if (rarityRef == PrototypeId.Invalid)
                rarityRef = item.Properties[PropertyEnum.ItemRarity];

            return rarityRef == _omegaForgeRarityProtoRef;
        }

        private static string GetOmegaForgeTokenLabel(OmegaForgeBonus bonus)
        {
            return bonus == OmegaForgeBonus.Random ? "random" : "specific";
        }
    }
}
