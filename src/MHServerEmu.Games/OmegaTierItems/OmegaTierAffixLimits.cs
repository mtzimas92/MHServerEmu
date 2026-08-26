using System.Diagnostics;
using System.Reflection;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;

namespace MHServerEmu.Games.OmegaTierItems
{
    /// <summary>
    /// Normalizes the shipped-but-unreachable R6Omega item tier so it can be used as a real rift reward tier.
    /// </summary>
    public static class OmegaTierAffixLimits
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private const string CosmicRarityName = "Entity/Items/Rarity/R5Cosmic.prototype";
        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";
        private const string ArmorPrototypePrefix = "Entity/Items/Armor/Prototypes/";
        private const string ArmorSimpleAT2CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleAT2.prototype";
        private const string ArmorSimpleAT3CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleAT3.prototype";
        private const string ArmorSimpleBT2CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleBT2.prototype";
        private const string ArmorSimpleBT3CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleBT3.prototype";
        private const string ArmorCosmicCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorCosmic.prototype";
        private const string ArmorOmegaCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorOmega.prototype";
        private const string RingOffenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingOffenseT3.prototype";
        private const string RingDefenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingDefenseT3.prototype";
        private const short ArmorCosmicAffixCount = 1;
        private const short ArmorOmegaAffixCount = 1;
        private const short RingT3AffixCount = 3;

        private static readonly string[] RingPrototypeNames =
        [
            "Entity/Items/Rings/RingOffenseLoot20.prototype",
            "Entity/Items/Rings/RingDefenseLoot20.prototype"
        ];

        private static readonly string[] InvertedBandAffixNames =
        [
            "Entity/Items/Affixes/RingAffixes/RingLoot20/Attributes/DurabilityT3.prototype"
        ];

        private static readonly PropertyInfo MinAffixesProperty =
            typeof(CategorizedAffixEntryPrototype).GetProperty(nameof(CategorizedAffixEntryPrototype.MinAffixes));

        private static readonly PropertyInfo CategoryProperty =
            typeof(CategorizedAffixEntryPrototype).GetProperty(nameof(CategorizedAffixEntryPrototype.Category));

        private static readonly PropertyInfo CategorizedAffixesProperty =
            typeof(AffixLimitsPrototype).GetProperty(nameof(AffixLimitsPrototype.CategorizedAffixes));

        private static readonly PropertyInfo AllowedRaritiesProperty =
            typeof(RarityRestrictionPrototype).GetProperty(nameof(RarityRestrictionPrototype.AllowedRarities));

        private static readonly PropertyInfo LoadIntValueProperty =
            typeof(LoadIntPrototype).GetProperty(nameof(LoadIntPrototype.Value));

        public static void Apply()
        {
#if GAME_VERSION_1_52 || GAME_VERSION_1_53
            Stopwatch stopwatch = Stopwatch.StartNew();

            PrototypeId cosmicRarityRef = GameDatabase.GetPrototypeRefByName(CosmicRarityName);
            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            if (cosmicRarityRef == PrototypeId.Invalid || omegaRarityRef == PrototypeId.Invalid)
            {
                Logger.Warn("Apply(): R5Cosmic or R6Omega not found, skipping Omega item normalization");
                return;
            }

            PrototypeId armorSimpleAT2CategoryRef = GameDatabase.GetPrototypeRefByName(ArmorSimpleAT2CategoryName);
            PrototypeId armorSimpleAT3CategoryRef = GameDatabase.GetPrototypeRefByName(ArmorSimpleAT3CategoryName);
            PrototypeId armorSimpleBT2CategoryRef = GameDatabase.GetPrototypeRefByName(ArmorSimpleBT2CategoryName);
            PrototypeId armorSimpleBT3CategoryRef = GameDatabase.GetPrototypeRefByName(ArmorSimpleBT3CategoryName);
            PrototypeId armorCosmicCategoryRef = GameDatabase.GetPrototypeRefByName(ArmorCosmicCategoryName);
            PrototypeId armorOmegaCategoryRef = GameDatabase.GetPrototypeRefByName(ArmorOmegaCategoryName);
            PrototypeId ringOffenseT3CategoryRef = GameDatabase.GetPrototypeRefByName(RingOffenseT3CategoryName);
            PrototypeId ringDefenseT3CategoryRef = GameDatabase.GetPrototypeRefByName(RingDefenseT3CategoryName);
            if (armorSimpleAT2CategoryRef == PrototypeId.Invalid ||
                armorSimpleAT3CategoryRef == PrototypeId.Invalid ||
                armorSimpleBT2CategoryRef == PrototypeId.Invalid ||
                armorSimpleBT3CategoryRef == PrototypeId.Invalid ||
                armorCosmicCategoryRef == PrototypeId.Invalid ||
                armorOmegaCategoryRef == PrototypeId.Invalid ||
                ringOffenseT3CategoryRef == PrototypeId.Invalid ||
                ringDefenseT3CategoryRef == PrototypeId.Invalid)
            {
                Logger.Warn("Apply(): one or more Omega affix categories were not found, skipping Omega item normalization");
                return;
            }

            int rowsChanged = 0;
            int categoriesPromoted = 0;
            int entriesAdjusted = 0;
            int entriesAdded = 0;

            foreach (PrototypeId itemProtoRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.NoAbstract))
            {
                ItemPrototype itemProto = GameDatabase.GetPrototype<ItemPrototype>(itemProtoRef);
                if (itemProto == null || itemProto.AffixLimits.IsNullOrEmpty())
                    continue;

                foreach (AffixLimitsPrototype row in itemProto.AffixLimits)
                {
                    if (row == null || row.ItemRarity != omegaRarityRef)
                        continue;

                    bool rowChanged = false;
                    rowChanged |= EnsureExistingCategoryCount(row, ringOffenseT3CategoryRef, RingT3AffixCount, ref entriesAdjusted);
                    rowChanged |= EnsureExistingCategoryCount(row, ringDefenseT3CategoryRef, RingT3AffixCount, ref entriesAdjusted);

                    if (IsArmorOmegaCandidate(itemProtoRef, row, armorOmegaCategoryRef) == false)
                    {
                        if (rowChanged)
                            rowsChanged++;

                        continue;
                    }

                    rowChanged |= PromoteCategory(row, armorSimpleAT2CategoryRef, armorSimpleAT3CategoryRef, ref categoriesPromoted);
                    rowChanged |= PromoteCategory(row, armorSimpleBT2CategoryRef, armorSimpleBT3CategoryRef, ref categoriesPromoted);
                    rowChanged |= EnsureCategory(row, armorCosmicCategoryRef, ArmorCosmicAffixCount, ref entriesAdjusted, ref entriesAdded);
                    rowChanged |= EnsureCategory(row, armorOmegaCategoryRef, ArmorOmegaAffixCount, ref entriesAdjusted, ref entriesAdded);

                    if (rowChanged)
                        rowsChanged++;
                }
            }

            TryEnableOmegaOnRings(omegaRarityRef);
            TryEnableOmegaOnCraftingRecipeInputs(cosmicRarityRef, omegaRarityRef);
            TryRepairInvertedAffixBands();

            stopwatch.Stop();
            Logger.Info($"Normalized R6Omega item affix limits on {rowsChanged} rows ({categoriesPromoted} T2 categories promoted, {entriesAdjusted} required category counts adjusted, {entriesAdded} required categories added) in {stopwatch.ElapsedMilliseconds} ms");
#endif
        }

#if GAME_VERSION_1_52 || GAME_VERSION_1_53
        public static bool AllowOmegaCraftingAffixLimitOverride(DropFilterArguments args, ItemSpec itemSpec, AffixPosition position, short affixCountNeeded, short currentCount, short currentLimit)
        {
            if (args == null || itemSpec == null || args.LootContext.HasFlag(LootContext.Crafting) == false)
                return false;

            if (position != AffixPosition.Runeword && position != AffixPosition.Unique)
                return false;

            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            if (omegaRarityRef == PrototypeId.Invalid || args.Rarity != omegaRarityRef)
                return false;

            if (IsArmorSlotOneThroughFive(args.ItemProto as ItemPrototype, args.Slot) == false)
                return false;

            short effectiveLimit = Math.Max(currentLimit, (short)1);
            return currentCount + affixCountNeeded <= effectiveLimit;
        }

        private static bool IsArmorOmegaCandidate(PrototypeId itemProtoRef, AffixLimitsPrototype row, PrototypeId armorOmegaCategoryRef)
        {
            string prototypeName = GameDatabase.GetPrototypeName(itemProtoRef);
            if (prototypeName != null && prototypeName.StartsWith(ArmorPrototypePrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            return FindEntry(row, armorOmegaCategoryRef) != null;
        }

        private static bool IsArmorSlotOneThroughFive(ItemPrototype itemProto, EquipmentInvUISlot slot)
        {
            if (itemProto is not ArmorPrototype armorProto)
                return false;

            if (slot == EquipmentInvUISlot.Invalid)
                slot = armorProto.DefaultEquipmentSlot;

            return slot >= EquipmentInvUISlot.Gear01 && slot <= EquipmentInvUISlot.Gear05;
        }

        private static bool PromoteCategory(AffixLimitsPrototype row, PrototypeId sourceCategoryRef, PrototypeId targetCategoryRef, ref int categoriesPromoted)
        {
            CategorizedAffixEntryPrototype source = FindEntry(row, sourceCategoryRef);
            if (source == null)
                return false;

            CategorizedAffixEntryPrototype target = FindEntry(row, targetCategoryRef);
            if (target != null)
            {
                MinAffixesProperty.SetValue(source, (short)0);
                categoriesPromoted++;
                return true;
            }

            CategoryProperty.SetValue(source, GameDatabase.GetPrototype<AffixCategoryPrototype>(targetCategoryRef));
            categoriesPromoted++;
            return true;
        }

        private static bool EnsureCategory(AffixLimitsPrototype row, PrototypeId categoryRef, short count, ref int entriesAdjusted, ref int entriesAdded)
        {
            CategorizedAffixEntryPrototype existing = FindEntry(row, categoryRef);
            if (existing != null)
            {
                if (existing.MinAffixes == count)
                    return false;

                MinAffixesProperty.SetValue(existing, count);
                entriesAdjusted++;
                return true;
            }

            AppendEntry(row, categoryRef, count);
            entriesAdded++;
            return true;
        }

        private static bool EnsureExistingCategoryCount(AffixLimitsPrototype row, PrototypeId categoryRef, short count, ref int entriesAdjusted)
        {
            CategorizedAffixEntryPrototype existing = FindEntry(row, categoryRef);
            if (existing == null || existing.MinAffixes == count)
                return false;

            MinAffixesProperty.SetValue(existing, count);
            entriesAdjusted++;
            return true;
        }

        private static void TryEnableOmegaOnRings(PrototypeId omegaRarityRef)
        {
            try
            {
                int ringsEnabled = EnableOmegaOnRings(omegaRarityRef);
                if (ringsEnabled > 0)
                    Logger.Info($"Permitted R6Omega on {ringsEnabled} ring drop restriction(s)");
            }
            catch (Exception e)
            {
                Logger.Warn($"TryEnableOmegaOnRings(): failed, Omega rings will remain disabled - {e.Message}");
            }
        }

        private static void TryEnableOmegaOnCraftingRecipeInputs(PrototypeId cosmicRarityRef, PrototypeId omegaRarityRef)
        {
            try
            {
                int recipesChanged = EnableOmegaOnCraftingRecipeInputs(cosmicRarityRef, omegaRarityRef, out int restrictionsChanged);
                if (restrictionsChanged > 0)
                    Logger.Info($"Permitted R6Omega on {restrictionsChanged} crafting recipe input rarity restriction(s) across {recipesChanged} recipe(s)");
            }
            catch (Exception e)
            {
                Logger.Warn($"TryEnableOmegaOnCraftingRecipeInputs(): failed, Omega crafting inputs may remain disabled - {e.Message}");
            }
        }

        private static int EnableOmegaOnCraftingRecipeInputs(PrototypeId cosmicRarityRef, PrototypeId omegaRarityRef, out int restrictionsChanged)
        {
            restrictionsChanged = 0;
            if (AllowedRaritiesProperty == null || cosmicRarityRef == PrototypeId.Invalid || omegaRarityRef == PrototypeId.Invalid)
                return 0;

            int recipesChanged = 0;
            foreach (PrototypeId recipeRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<CraftingRecipePrototype>(PrototypeIterateFlags.NoAbstract))
            {
                CraftingRecipePrototype recipeProto = GameDatabase.GetPrototype<CraftingRecipePrototype>(recipeRef);
                if (recipeProto?.RecipeInputs.IsNullOrEmpty() != false)
                    continue;

                int changedForRecipe = 0;
                foreach (CraftingInputPrototype inputProto in recipeProto.RecipeInputs)
                {
                    if (inputProto is RestrictionSetInputPrototype restrictionInput)
                        changedForRecipe += EnableOmegaOnRestrictions(restrictionInput.Restrictions, cosmicRarityRef, omegaRarityRef);
                }

                if (changedForRecipe <= 0)
                    continue;

                restrictionsChanged += changedForRecipe;
                recipesChanged++;
            }

            return recipesChanged;
        }

        private static int EnableOmegaOnRestrictions(DropRestrictionPrototype[] restrictions, PrototypeId cosmicRarityRef, PrototypeId omegaRarityRef)
        {
            if (restrictions.IsNullOrEmpty())
                return 0;

            int changed = 0;
            foreach (DropRestrictionPrototype restriction in restrictions)
                changed += EnableOmegaOnRestriction(restriction, cosmicRarityRef, omegaRarityRef);

            return changed;
        }

        private static int EnableOmegaOnRestriction(DropRestrictionPrototype restriction, PrototypeId cosmicRarityRef, PrototypeId omegaRarityRef)
        {
            switch (restriction)
            {
                case RarityRestrictionPrototype rarityRestriction:
                    return TryExpandRarityRestriction(rarityRestriction, cosmicRarityRef, omegaRarityRef) ? 1 : 0;

                case ConditionalRestrictionPrototype conditionalRestriction:
                    return EnableOmegaOnRestrictions(conditionalRestriction.Apply, cosmicRarityRef, omegaRarityRef) +
                           EnableOmegaOnRestrictions(conditionalRestriction.Else, cosmicRarityRef, omegaRarityRef);

                case RestrictionListPrototype restrictionList:
                    return EnableOmegaOnRestrictions(restrictionList.Children, cosmicRarityRef, omegaRarityRef);

                default:
                    return 0;
            }
        }

        private static bool TryExpandRarityRestriction(RarityRestrictionPrototype rarityRestriction, PrototypeId allowedRarityRef, PrototypeId rarityToAddRef)
        {
            PrototypeId[] current = rarityRestriction.AllowedRarities;
            if (current.IsNullOrEmpty() || current.Contains(allowedRarityRef) == false || current.Contains(rarityToAddRef))
                return false;

            PrototypeId[] expanded = new PrototypeId[current.Length + 1];
            Array.Copy(current, expanded, current.Length);
            expanded[current.Length] = rarityToAddRef;
            Array.Sort(expanded, CompareRarityTier);

            AllowedRaritiesProperty.SetValue(rarityRestriction, expanded);
            return true;
        }

        private static int EnableOmegaOnRings(PrototypeId omegaRarityRef)
        {
            if (AllowedRaritiesProperty == null || omegaRarityRef == PrototypeId.Invalid)
                return 0;

            int changed = 0;
            foreach (string ringName in RingPrototypeNames)
            {
                PrototypeId ringRef = GameDatabase.GetPrototypeRefByName(ringName);
                ItemPrototype ringProto = GameDatabase.GetPrototype<ItemPrototype>(ringRef);
                if (ringProto?.LootDropRestrictions.IsNullOrEmpty() != false)
                    continue;

                foreach (DropRestrictionPrototype restriction in ringProto.LootDropRestrictions)
                {
                    if (restriction is not RarityRestrictionPrototype rarityRestriction)
                        continue;

                    PrototypeId[] current = rarityRestriction.AllowedRarities;
                    if (current.IsNullOrEmpty())
                        continue;

                    PrototypeId allowedRarityRef = current.Contains(GameDatabase.LootGlobalsPrototype.RarityCosmic)
                        ? GameDatabase.LootGlobalsPrototype.RarityCosmic
                        : current[0];

                    if (TryExpandRarityRestriction(rarityRestriction, allowedRarityRef, omegaRarityRef))
                        changed++;
                }
            }

            return changed;
        }

        private static int CompareRarityTier(PrototypeId left, PrototypeId right)
        {
            int leftTier = GameDatabase.GetPrototype<RarityPrototype>(left)?.Tier ?? 0;
            int rightTier = GameDatabase.GetPrototype<RarityPrototype>(right)?.Tier ?? 0;
            return leftTier.CompareTo(rightTier);
        }

        private static void TryRepairInvertedAffixBands()
        {
            try
            {
                RepairInvertedAffixBands();
            }
            catch (Exception e)
            {
                Logger.Warn($"TryRepairInvertedAffixBands(): failed - {e.Message}");
            }
        }

        private static void RepairInvertedAffixBands()
        {
            if (LoadIntValueProperty == null)
                return;

            foreach (string affixName in InvertedBandAffixNames)
            {
                PrototypeId affixRef = GameDatabase.GetPrototypeRefByName(affixName);
                AffixPrototype affixProto = GameDatabase.GetPrototype<AffixPrototype>(affixRef);
                if (affixProto?.PropertyEntries.IsNullOrEmpty() != false)
                    continue;

                foreach (PropertyPickInRangeEntryPrototype entry in affixProto.PropertyEntries)
                {
                    if (entry?.ValueMin is not LoadIntPrototype min || entry.ValueMax is not LoadIntPrototype max || min.Value <= max.Value)
                        continue;

                    int low = max.Value;
                    int high = min.Value;
                    LoadIntValueProperty.SetValue(min, low);
                    LoadIntValueProperty.SetValue(max, high);
                    Logger.Info($"Repaired inverted affix band on {GameDatabase.GetPrototypeName(affixRef)}: now {low}..{high}");
                }
            }
        }

        private static CategorizedAffixEntryPrototype FindEntry(AffixLimitsPrototype row, PrototypeId categoryRef)
        {
            if (row.CategorizedAffixes.IsNullOrEmpty())
                return null;

            foreach (CategorizedAffixEntryPrototype entry in row.CategorizedAffixes)
            {
                if (entry?.Category != null && entry.Category.DataRef == categoryRef)
                    return entry;
            }

            return null;
        }

        private static void AppendEntry(AffixLimitsPrototype row, PrototypeId categoryRef, short count)
        {
            var entry = (CategorizedAffixEntryPrototype)GameDatabase.PrototypeClassManager
                .AllocatePrototype(typeof(CategorizedAffixEntryPrototype));

            CategoryProperty.SetValue(entry, GameDatabase.GetPrototype<AffixCategoryPrototype>(categoryRef));
            MinAffixesProperty.SetValue(entry, count);

            CategorizedAffixEntryPrototype[] current = row.CategorizedAffixes;
            int length = current?.Length ?? 0;

            var expanded = new CategorizedAffixEntryPrototype[length + 1];
            if (current != null)
                Array.Copy(current, expanded, length);
            expanded[length] = entry;

            CategorizedAffixesProperty.SetValue(row, expanded);
        }
#endif
    }
}
