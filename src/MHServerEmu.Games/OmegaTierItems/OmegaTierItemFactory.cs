using MHServerEmu.Core.Collections;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.System.Random;
using System.Text.Json;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.PatchManager;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.GameData.Tables;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.OmegaTierItems
{
    public static class OmegaTierItemFactory
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";
        private const string ArmorSimpleAT2CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleAT2.prototype";
        private const string ArmorSimpleAT3CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleAT3.prototype";
        private const string ArmorSimpleBT2CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleBT2.prototype";
        private const string ArmorSimpleBT3CategoryName = "Entity/Items/Affixes/AffixCategories/ArmorSimpleBT3.prototype";
        private const string ArmorOmegaCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorOmega.prototype";
        private const string RingOmegaAffixName = "Entity/Items/Affixes/RingAffixes/RingLoot20/BuiltInHP/RingHealthOmega.prototype";
        private const string RingOffenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingOffenseT3.prototype";
        private const string RingDefenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingDefenseT3.prototype";

        public static bool ShouldUseFactory(PrototypeId itemProtoRef, PrototypeId rarityProtoRef)
        {
            if (IsOmegaRarity(rarityProtoRef))
                return true;

            return OmegaTierItemTuning.Load().HasItemOverride(itemProtoRef, rarityProtoRef);
        }

        public static bool TryApplyConfiguredUniqueOverrideForOmegaDifficulty(ItemResolver resolver, ItemSpec itemSpec, LootRollSettings settings = null)
        {
            if (resolver == null || itemSpec == null || itemSpec.IsValid == false)
                return false;

            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();
            if (tuning?.Enabled != true || tuning.HasItemOverride(itemSpec.ItemProtoRef, itemSpec.RarityProtoRef) == false)
                return false;

            if (IsSupportedOverrideLootContext(resolver.LootContext) == false ||
                IsUniqueItemSpec(itemSpec) == false ||
                IsOmegaDifficulty(resolver, settings) == false)
            {
                return false;
            }

            ItemPrototype itemProto = itemSpec.ItemProtoRef.As<ItemPrototype>();
            if (itemProto == null)
                return false;

            AvatarPrototype avatarProto = resolver.Player?.CurrentAvatar?.AvatarPrototype;
            PrototypeId rollFor = itemSpec.EquippableBy != PrototypeId.Invalid
                ? itemSpec.EquippableBy
                : resolver.ResolveAvatarPrototype(avatarProto, forceUsable: true, usablePercent: 1f).DataRef;
            EquipmentInvUISlot slot = itemProto.GetInventorySlotForAgent(avatarProto);

            using var filterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments filterArgs);
            DropFilterArguments.Initialize(filterArgs, itemProto, rollFor, Math.Max(itemSpec.ItemLevel, 1), itemSpec.RarityProtoRef, 0, slot, resolver.LootContext);

            TryApplyItemOverrides(resolver, filterArgs, itemSpec, rollFor, tuning);
            return true;
        }

        public static void ApplyConfiguredRuntimeProperties(Item item)
        {
            if (item?.ItemPrototype == null || item.Properties == null)
                return;

            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();
            if (tuning?.Enabled != true || tuning.ItemOverrides.Count == 0)
                return;

            PrototypeId itemProtoRef = item.ItemPrototype.DataRef;
            PrototypeId rarityProtoRef = item.Properties[PropertyEnum.ItemRarity];
            foreach (OmegaTierItemOverrideTuning itemOverride in tuning.ItemOverrides)
            {
                if (itemOverride?.Matches(itemProtoRef, rarityProtoRef) != true)
                    continue;

                RemoveConfiguredProcPowers(item, itemOverride);
                foreach (OmegaTierBuiltInPropertyTuning builtInProperty in itemOverride.BuiltInProperties)
                    TryApplyBuiltInProperty(item, itemOverride, builtInProperty);

                foreach (OmegaTierProcKeywordPropertyTuning procKeywordProperty in itemOverride.ProcKeywordProperties)
                    TryApplyProcKeywordProperty(item, itemOverride, procKeywordProperty);
            }
        }

        public static ItemSpec CreateItemSpec(
            Game game,
            PrototypeId itemProtoRef,
            PrototypeId rarityProtoRef,
            LootContext lootContext,
            Player player,
            int level,
            bool logFailures = true)
        {
            ItemPrototype itemProto = itemProtoRef.As<ItemPrototype>();
            if (itemProto == null || game == null)
                return null;

            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();

            if (rarityProtoRef == PrototypeId.Invalid)
            {
                ItemSpec baseItemSpec = game.LootManager?.CreateItemSpec(itemProtoRef, lootContext, player, level);
                if (baseItemSpec == null)
                    return null;

                TryApplyItemOverrides(game, baseItemSpec, lootContext, player, tuning);
                return baseItemSpec;
            }

            if (GameDatabase.DataDirectory.PrototypeIsAbstract(itemProtoRef))
            {
                if (logFailures)
                    Logger.Warn($"CreateItemSpec(): cannot create abstract item {itemProtoRef.GetNameFormatted()}");
                return null;
            }

            var resolver = new ItemResolver();
            resolver.Initialize(game.Random);
            resolver.SetContext(lootContext, player);

            AvatarPrototype avatarProto = player?.CurrentAvatar?.AvatarPrototype;

            PrototypeId rollFor = resolver.ResolveAvatarPrototype(avatarProto, forceUsable: true, usablePercent: 1f).DataRef;
            int resolvedLevel = Math.Max(level, 1);
            EquipmentInvUISlot slot = itemProto.GetInventorySlotForAgent(avatarProto);

            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            bool requestedOmega = IsOmegaRarity(rarityProtoRef);

            using var filterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments filterArgs);
            DropFilterArguments.Initialize(filterArgs, itemProto, rollFor, resolvedLevel, rarityProtoRef, 0, slot, lootContext);

            RestrictionTestFlags nonRarityRestrictions = RestrictionTestFlags.All & ~(RestrictionTestFlags.Rarity | RestrictionTestFlags.Level);
            if (itemProto.MakeRestrictionsDroppable(filterArgs, nonRarityRestrictions, out _) == false ||
                itemProto.IsDroppableForRestrictions(filterArgs, RestrictionTestFlags.Rarity) == false)
            {
                if (logFailures)
                    Logger.Warn($"CreateItemSpec(): {itemProtoRef.GetNameFormatted()} cannot drop as {rarityProtoRef.GetNameFormatted()} at level {filterArgs.Level}");
                return null;
            }

            ItemSpec itemSpec = new(
                filterArgs.ItemProto.DataRef,
                filterArgs.Rarity,
                filterArgs.Level,
                0,
                Array.Empty<AffixSpec>(),
                resolver.Random.Next());

            MutationResults mutationResults = LootUtilities.UpdateAffixes(resolver, filterArgs, AffixCountBehavior.Roll, itemSpec, null);
            if (mutationResults.HasFlag(MutationResults.Error))
            {
                if (logFailures)
                    Logger.Warn($"CreateItemSpec(): failed to roll affixes for {itemProtoRef.GetNameFormatted()} as {rarityProtoRef.GetNameFormatted()}");
                return null;
            }

            TryApplyPreferredAffixes(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef, tuning);
            TryApplyOmegaArmorAffix(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef, tuning);
            TryApplyOmegaRingAffix(resolver, filterArgs, itemSpec, requestedOmega, omegaRarityRef, tuning);
            TryReplaceDisabledAffixes(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef, tuning);
            TryApplyItemOverrides(resolver, filterArgs, itemSpec, rollFor, tuning);

            if (requestedOmega && itemSpec.RarityProtoRef == rarityProtoRef)
            {
                Logger.Info($"Omega reward created: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={slot} rarity={rarityProtoRef.GetNameFormatted()} affixes={itemSpec.AffixSpecs.Count}");
                LogOmegaAffixAudit(itemSpec, slot);
            }

            return itemSpec;
        }

#if GAME_VERSION_1_52 || GAME_VERSION_1_53
        private static void TryApplyOmegaArmorAffix(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            bool requestedOmega,
            PrototypeId omegaRarityRef,
            OmegaTierItemTuning tuning)
        {
            if (requestedOmega == false || resolver == null || filterArgs == null || itemSpec == null)
                return;

            if (omegaRarityRef == PrototypeId.Invalid || IsArmorSlotOneThroughFive(filterArgs.ItemProto as ItemPrototype, filterArgs.Slot) == false)
                return;

            PrototypeId armorOmegaCategoryRef = GameDatabase.GetPrototypeRefByName(ArmorOmegaCategoryName);
            AffixCategoryPrototype armorOmegaCategory = GameDatabase.GetPrototype<AffixCategoryPrototype>(armorOmegaCategoryRef);
            if (armorOmegaCategory == null)
                return;

            IReadOnlyList<AffixPrototype> affixes = GameDataTables.Instance.LootPickingTable.GetAffixesByCategory(armorOmegaCategory);
            if (affixes == null || affixes.Count == 0)
                return;

            using var omegaFilterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments omegaFilterArgs);
            DropFilterArguments.Initialize(
                omegaFilterArgs,
                filterArgs.ItemProto,
                filterArgs.RollFor,
                filterArgs.Level,
                omegaRarityRef,
                filterArgs.Rank,
                filterArgs.Slot,
                filterArgs.LootContext);

            List<AffixPrototype> candidates = new();
            foreach (AffixPrototype affixProto in affixes)
            {
                if (affixProto == null || affixProto.Weight <= 0)
                    continue;

                if (tuning?.IsAffixDisabled(affixProto) == true)
                    continue;

                if (affixProto.AllowAttachment(omegaFilterArgs) == false)
                    continue;

                candidates.Add(affixProto);
            }

            if (candidates.Count == 0)
            {
                Logger.Warn($"Omega reward could not find an ArmorOmega affix candidate for {itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot}");
                return;
            }

            AffixPrototype pickedAffixProto = candidates[resolver.Random.Next(0, candidates.Count)];
            MutationResults addResult = LootUtilities.AddAffix(resolver, omegaFilterArgs, itemSpec, pickedAffixProto);
            if (addResult.HasFlag(MutationResults.Error))
            {
                Logger.Warn($"Omega reward failed to add ArmorOmega affix {pickedAffixProto.DataRef.GetNameFormatted()} to {itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot}");
                return;
            }

            Logger.Info($"Omega reward extra affix applied: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={pickedAffixProto.DataRef.GetNameFormatted()} effectiveRarity={itemSpec.RarityProtoRef.GetNameFormatted()}");
        }

        private static void TryApplyOmegaRingAffix(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            bool requestedOmega,
            PrototypeId omegaRarityRef,
            OmegaTierItemTuning tuning)
        {
            if (requestedOmega == false || resolver == null || filterArgs == null || itemSpec == null)
                return;

            if (omegaRarityRef == PrototypeId.Invalid || filterArgs.Slot != EquipmentInvUISlot.Ring)
                return;

            using var omegaFilterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments omegaFilterArgs);
            DropFilterArguments.Initialize(
                omegaFilterArgs,
                filterArgs.ItemProto,
                filterArgs.RollFor,
                filterArgs.Level,
                omegaRarityRef,
                filterArgs.Rank,
                filterArgs.Slot,
                filterArgs.LootContext);

            AffixPrototype pickedAffixProto = PickOmegaRingExtraAffix(resolver, filterArgs, omegaFilterArgs, itemSpec, tuning);
            if (pickedAffixProto == null)
            {
                Logger.Warn($"Omega reward could not find a ring extra affix candidate for {itemSpec.ItemProtoRef.GetNameFormatted()}");
                return;
            }

            DropFilterArguments addArgs = pickedAffixProto.AllowAttachment(omegaFilterArgs) ? omegaFilterArgs : filterArgs;
            MutationResults addResult = LootUtilities.AddAffix(resolver, addArgs, itemSpec, pickedAffixProto);
            if (addResult.HasFlag(MutationResults.Error))
            {
                Logger.Warn($"Omega reward failed to add ring extra affix {pickedAffixProto.DataRef.GetNameFormatted()} to {itemSpec.ItemProtoRef.GetNameFormatted()}");
                return;
            }

            Logger.Info($"Omega reward extra ring affix applied: item={itemSpec.ItemProtoRef.GetNameFormatted()} affix={pickedAffixProto.DataRef.GetNameFormatted()} effectiveRarity={itemSpec.RarityProtoRef.GetNameFormatted()}");
        }

        private static bool IsArmorSlotOneThroughFive(ItemPrototype itemProto, EquipmentInvUISlot slot)
        {
            if (itemProto is not ArmorPrototype armorProto)
                return false;

            if (slot == EquipmentInvUISlot.Invalid)
                slot = armorProto.DefaultEquipmentSlot;

            return slot >= EquipmentInvUISlot.Gear01 && slot <= EquipmentInvUISlot.Gear05;
        }

        private static void LogOmegaAffixAudit(ItemSpec itemSpec, EquipmentInvUISlot slot)
        {
            if (itemSpec == null)
                return;

            int t2Count = 0;
            int t3Count = 0;
            List<string> affixSummaries = new(itemSpec.AffixSpecs.Count);

            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                AffixPrototype affixProto = affixSpec?.AffixProto;
                if (affixProto == null)
                    continue;

                string tier = "Other";
                if (HasAnyCategory(affixProto, ArmorSimpleAT3CategoryName, ArmorSimpleBT3CategoryName, RingOffenseT3CategoryName, RingDefenseT3CategoryName))
                {
                    tier = "T3";
                    t3Count++;
                }
                else if (HasAnyCategory(affixProto, ArmorSimpleAT2CategoryName, ArmorSimpleBT2CategoryName))
                {
                    tier = "T2";
                    t2Count++;
                }

                affixSummaries.Add($"{affixProto.DataRef.GetNameFormatted()}@{affixProto.Position}:{tier}");
            }

            Logger.Info($"Omega reward affix audit: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={slot} rarity={itemSpec.RarityProtoRef.GetNameFormatted()} t3Affixes={t3Count} t2Affixes={t2Count} affixes=[{string.Join(", ", affixSummaries)}]");
        }

        private static bool HasAnyCategory(AffixPrototype affixProto, params string[] categoryNames)
        {
            if (affixProto == null || categoryNames == null)
                return false;

            foreach (string categoryName in categoryNames)
            {
                PrototypeId categoryRef = GameDatabase.GetPrototypeRefByName(categoryName);
                AffixCategoryPrototype categoryProto = GameDatabase.GetPrototype<AffixCategoryPrototype>(categoryRef);
                if (categoryProto != null && affixProto.HasCategory(categoryProto))
                    return true;
            }

            return false;
        }

        private static void TryApplyItemOverrides(
            Game game,
            ItemSpec itemSpec,
            LootContext lootContext,
            Player player,
            OmegaTierItemTuning tuning)
        {
            if (game == null || itemSpec == null || tuning?.Enabled != true || tuning.HasItemOverride(itemSpec.ItemProtoRef, itemSpec.RarityProtoRef) == false)
                return;

            ItemPrototype itemProto = itemSpec.ItemProtoRef.As<ItemPrototype>();
            if (itemProto == null)
                return;

            var resolver = new ItemResolver();
            resolver.Initialize(game.Random);
            resolver.SetContext(lootContext, player);

            AvatarPrototype avatarProto = player?.CurrentAvatar?.AvatarPrototype;
            PrototypeId rollFor = resolver.ResolveAvatarPrototype(avatarProto, forceUsable: true, usablePercent: 1f).DataRef;
            EquipmentInvUISlot slot = itemProto.GetInventorySlotForAgent(avatarProto);

            using var filterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments filterArgs);
            DropFilterArguments.Initialize(filterArgs, itemProto, rollFor, Math.Max(itemSpec.ItemLevel, 1), itemSpec.RarityProtoRef, 0, slot, lootContext);

            TryApplyItemOverrides(resolver, filterArgs, itemSpec, rollFor, tuning);
        }

        private static void TryApplyItemOverrides(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            OmegaTierItemTuning tuning)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || tuning?.Enabled != true || tuning.ItemOverrides.Count == 0)
                return;

            foreach (OmegaTierItemOverrideTuning itemOverride in tuning.ItemOverrides)
            {
                if (itemOverride?.Matches(itemSpec.ItemProtoRef, itemSpec.RarityProtoRef) != true)
                    continue;

                bool changed = false;
                PrototypeId overrideRarityRef = ResolvePrototype(itemOverride.OverrideRarity);
                if (overrideRarityRef != PrototypeId.Invalid && itemSpec.RarityProtoRef != overrideRarityRef)
                {
                    itemSpec.RarityProtoRef = overrideRarityRef;
                    changed = true;
                }

                if (itemOverride.OverrideItemLevel > 0 && itemSpec.ItemLevel != itemOverride.OverrideItemLevel)
                {
                    itemSpec.ItemLevel = itemOverride.OverrideItemLevel;
                    changed = true;
                }

                if (itemOverride.ClearExistingAffixes)
                {
                    itemSpec.SetAffixes(Array.Empty<AffixSpec>());
                    changed = true;
                }

                changed |= RemoveDisabledOverrideAffixes(itemSpec, itemOverride);
                changed |= TryApplyAffixReplacements(resolver, filterArgs, itemSpec, tuning, itemOverride);

                foreach (OmegaTierForcedAffixTuning forcedAffix in itemOverride.ForcedAffixes)
                {
                    if (forcedAffix == null || forcedAffix.Count <= 0)
                        continue;

                    AffixPrototype affixProto = ResolveAffix(forcedAffix.Prototype);
                    if (affixProto == null)
                    {
                        Logger.Warn($"Omega item override forced affix could not resolve: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} affix={forcedAffix.Prototype}");
                        continue;
                    }

                    for (int i = 0; i < forcedAffix.Count; i++)
                    {
                        if (forcedAffix.ReplaceExistingSamePosition)
                            changed |= RemoveAffixesAtPosition(itemSpec, affixProto.Position);

                        if (TryAddAffixSpec(resolver, filterArgs, itemSpec, affixProto, forcedAffix.AllowInvalidAttachment, itemOverride.MaximizeAffixRolls))
                            changed = true;
                    }
                }

                foreach (OmegaTierRandomAffixTuning randomAffix in itemOverride.RandomAffixes)
                    changed |= TryAddRandomOverrideAffixes(resolver, filterArgs, itemSpec, tuning, itemOverride, randomAffix);

                if (changed)
                {
                    itemSpec.OnAffixesRolled(resolver, rollFor);
                    Logger.Info($"Omega item override applied: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} rarity={itemSpec.RarityProtoRef.GetNameFormatted()} affixes={itemSpec.AffixSpecs.Count}");
                }
            }
        }

        private static bool RemoveDisabledOverrideAffixes(ItemSpec itemSpec, OmegaTierItemOverrideTuning itemOverride)
        {
            if (itemSpec == null || itemOverride == null || itemSpec.AffixSpecs.Count == 0)
                return false;

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            bool removed = false;
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                if (affixSpec?.AffixProto != null && itemOverride.IsAffixDisabled(affixSpec.AffixProto))
                {
                    removed = true;
                    continue;
                }

                affixSpecs.Add(new(affixSpec));
            }

            if (removed)
                itemSpec.SetAffixes(affixSpecs);

            return removed;
        }

        private static bool TryApplyAffixReplacements(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            OmegaTierItemTuning tuning,
            OmegaTierItemOverrideTuning itemOverride)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || itemOverride?.AffixReplacements == null ||
                itemOverride.AffixReplacements.Count == 0 || itemSpec.AffixSpecs.Count == 0)
            {
                return false;
            }

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
                affixSpecs.Add(new(affixSpec));

            bool changed = false;
            int replacedCount = 0;
            foreach (OmegaTierAffixReplacementTuning replacement in itemOverride.AffixReplacements)
            {
                if (replacement == null || replacement.Count <= 0)
                    continue;

                AffixPrototype replacementAffixProto = ResolveAffix(replacement.Prototype);
                if (replacementAffixProto == null)
                {
                    Logger.Warn($"Omega item override replacement affix could not resolve: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} affix={replacement.Prototype}");
                    continue;
                }

                for (int i = 0; i < replacement.Count; i++)
                {
                    int replacementIndex = FindOverrideReplacementAffixIndex(affixSpecs, replacementAffixProto, tuning, replacement);
                    if (replacementIndex < 0)
                    {
                        Logger.Warn($"Omega item override replacement had no eligible affix slot: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} affix={replacementAffixProto.DataRef.GetNameFormatted()} affixes={affixSpecs.Count}");
                        continue;
                    }

                    AffixSpec replacementSpec = CreateReplacementAffixSpec(resolver, filterArgs, itemSpec, affixSpecs, replacementIndex, replacementAffixProto, replacement.AllowInvalidAttachment, itemOverride.MaximizeAffixRolls);
                    if (replacementSpec == null || replacementSpec.IsValid == false)
                    {
                        Logger.Warn($"Omega item override replacement failed to roll: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} affix={replacementAffixProto.DataRef.GetNameFormatted()}");
                        continue;
                    }

                    string oldAffixName = affixSpecs[replacementIndex]?.AffixProto?.DataRef.GetNameFormatted() ?? string.Empty;
                    affixSpecs[replacementIndex] = replacementSpec;
                    changed = true;
                    replacedCount++;
                    Logger.Info($"Omega item override replaced affix: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} old={oldAffixName} new={replacementAffixProto.DataRef.GetNameFormatted()} index={replacementIndex}");
                }
            }

            if (changed)
                itemSpec.SetAffixes(affixSpecs);

            if (replacedCount > 0)
                Logger.Info($"Omega item override selective affix replacements applied: override={itemOverride.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} replaced={replacedCount}");

            return changed;
        }

        private static bool TryApplyBuiltInProperty(
            Item item,
            OmegaTierItemOverrideTuning itemOverride,
            OmegaTierBuiltInPropertyTuning builtInProperty)
        {
            if (item == null || itemOverride == null || builtInProperty == null)
                return false;

            if (TryResolveBuiltInPropertyId(itemOverride, builtInProperty, out PropertyId propertyId) == false)
                return false;

            PropertyInfo propertyInfo = GameDatabase.PropertyInfoTable.LookupPropertyInfo(propertyId.Enum);
            if (propertyInfo == null)
            {
                Logger.Warn($"Omega runtime built-in property override has no PropertyInfo: override={itemOverride.Id} item={item.ItemPrototype.DataRef.GetNameFormatted()} property={propertyId.Enum}");
                return false;
            }

            float randomMult = itemOverride.MaximizeBuiltInPropertyRolls
                ? 0.99999988f
                : RollConfiguredBuiltInPropertyMult(item, propertyId);

            switch (propertyInfo.DataType)
            {
                case PropertyDataType.Real:
                    item.Properties[propertyId] = builtInProperty.RollAsInteger
                        ? GenerateTruncatedFloatWithinRange(randomMult, builtInProperty.ValueMin, builtInProperty.ValueMax)
                        : GenerateFloatWithinRange(randomMult, builtInProperty.ValueMin, builtInProperty.ValueMax);
                    return true;

                case PropertyDataType.Integer:
                    item.Properties[propertyId] = GenerateIntWithinRange(randomMult, builtInProperty.ValueMin, builtInProperty.ValueMax);
                    return true;

                case PropertyDataType.Boolean:
                    item.Properties[propertyId] = GenerateIntWithinRange(randomMult, builtInProperty.ValueMin, builtInProperty.ValueMax) != 0;
                    return true;

                default:
                    Logger.Warn($"Omega runtime built-in property override skipped unsupported property type: override={itemOverride.Id} item={item.ItemPrototype.DataRef.GetNameFormatted()} property={propertyInfo.PropertyName} type={propertyInfo.DataType}");
                    return false;
            }
        }

        private static bool TryResolveBuiltInPropertyId(
            OmegaTierItemOverrideTuning itemOverride,
            OmegaTierBuiltInPropertyTuning builtInProperty,
            out PropertyId propertyId)
        {
            propertyId = PropertyId.Invalid;

            if (builtInProperty.Property.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                Logger.Warn($"Omega runtime built-in property override has no property: override={itemOverride.Id}");
                return false;
            }

            try
            {
                propertyId = PatchEntryConverter.ParseJsonPropertyIdSinglePublic(builtInProperty.Property);
            }
            catch (Exception e)
            {
                Logger.Warn($"Omega runtime built-in property override could not parse property: override={itemOverride.Id} property={builtInProperty.Property} exception={e.Message}");
                return false;
            }

            if (propertyId == PropertyId.Invalid)
            {
                Logger.Warn($"Omega runtime built-in property override resolved invalid property: override={itemOverride.Id} property={builtInProperty.Property}");
                return false;
            }

            return true;
        }

        private static bool RemoveConfiguredProcPowers(Item item, OmegaTierItemOverrideTuning itemOverride)
        {
            if (item == null || itemOverride?.DisabledProcPowers == null || itemOverride.DisabledProcPowers.Count == 0)
                return false;

            HashSet<PrototypeId> disabledProcPowerRefs = new();
            foreach (string disabledProcPower in itemOverride.DisabledProcPowers)
            {
                PrototypeId procPowerRef = ResolvePrototype(disabledProcPower);
                if (procPowerRef != PrototypeId.Invalid)
                    disabledProcPowerRefs.Add(procPowerRef);
            }

            if (disabledProcPowerRefs.Count == 0)
                return false;

            List<PropertyId> propertiesToRemove = new();
            using var procPropertiesHandle = PropertyCollectionPool.Get(out PropertyCollection procProperties);
            foreach (PropertyEnum procProperty in Property.ProcPropertyTypesAll)
                procProperties.CopyPropertyRange(item.Properties, procProperty);

            foreach (var kvp in procProperties)
            {
                Property.FromParam(kvp.Key, 1, out PrototypeId procPowerRef);
                if (disabledProcPowerRefs.Contains(procPowerRef))
                    propertiesToRemove.Add(kvp.Key);
            }

            foreach (PropertyId propertyId in propertiesToRemove)
                item.Properties.RemoveProperty(propertyId);

            return propertiesToRemove.Count > 0;
        }

        private static bool TryApplyProcKeywordProperty(
            Item item,
            OmegaTierItemOverrideTuning itemOverride,
            OmegaTierProcKeywordPropertyTuning procKeywordProperty)
        {
            if (item == null || itemOverride == null || procKeywordProperty == null)
                return false;

            if (Enum.TryParse(procKeywordProperty.Trigger, ignoreCase: true, out ProcTriggerType triggerType) == false)
            {
                Logger.Warn($"Omega runtime proc override has invalid trigger: trigger={procKeywordProperty.Trigger}");
                return false;
            }

            PrototypeId procPowerRef = ResolvePrototype(procKeywordProperty.Power);
            PrototypeId keywordRef = ResolvePrototype(procKeywordProperty.Keyword);
            if (procPowerRef == PrototypeId.Invalid || keywordRef == PrototypeId.Invalid)
            {
                Logger.Warn($"Omega runtime proc override could not resolve refs: power={procKeywordProperty.Power} keyword={procKeywordProperty.Keyword}");
                return false;
            }

            PropertyId procPropertyId = new(
                PropertyEnum.ProcKeyword,
                (PropertyParam)(int)triggerType,
                Property.ToParam(PropertyEnum.ProcKeyword, 1, procPowerRef),
                Property.ToParam(PropertyEnum.ProcKeyword, 2, keywordRef));

            float randomMult = RollConfiguredBuiltInPropertyMult(item, procPowerRef, keywordRef, triggerType);
            item.Properties[procPropertyId] = procKeywordProperty.GetChance(randomMult, itemOverride.MaximizeBuiltInPropertyRolls);

            if (procKeywordProperty.ItemLevelOverride.HasValue)
                item.Properties[PropertyEnum.ProcPowerItemLevel, procPowerRef] = procKeywordProperty.ItemLevelOverride.Value;

            if (procKeywordProperty.ItemVariationOverride.HasValue)
                item.Properties[PropertyEnum.ProcPowerItemVariation, procPowerRef] = procKeywordProperty.ItemVariationOverride.Value;

            if (procKeywordProperty.PowerRankOverride.HasValue)
                item.Properties[PropertyEnum.ProcPowerRank, procPowerRef] = procKeywordProperty.PowerRankOverride.Value;

            return true;
        }

        private static float RollConfiguredBuiltInPropertyMult(
            Item item,
            PrototypeId procPowerRef,
            PrototypeId keywordRef,
            ProcTriggerType triggerType)
        {
            int seed = item?.ItemSpec?.Seed ?? 0;

            unchecked
            {
                seed = (seed * 397) ^ (int)triggerType;
                seed = (seed * 397) ^ (int)(ulong)procPowerRef;
                seed = (seed * 397) ^ (int)((ulong)procPowerRef >> 32);
                seed = (seed * 397) ^ (int)(ulong)keywordRef;
                seed = (seed * 397) ^ (int)((ulong)keywordRef >> 32);
            }

            return new GRandom(seed).NextFloat();
        }

        private static float RollConfiguredBuiltInPropertyMult(Item item, PropertyId propertyId)
        {
            int seed = item?.ItemSpec?.Seed ?? 0;

            unchecked
            {
                seed = (seed * 397) ^ (int)propertyId.Raw;
                seed = (seed * 397) ^ (int)(propertyId.Raw >> 32);
            }

            return new GRandom(seed).NextFloat();
        }

        private static float GenerateTruncatedFloatWithinRange(float randomMult, float min, float max)
        {
            float result = ((max - min + 1f) * randomMult) + min;
            float lower = Math.Min(min, max);
            float upper = Math.Max(min, max);
            result = Math.Clamp(result, lower, upper);
            return MathF.Floor(result);
        }

        private static float GenerateFloatWithinRange(float randomMult, float min, float max)
        {
            return ((max - min) * randomMult) + min;
        }

        private static int GenerateIntWithinRange(float randomMult, float min, float max)
        {
            return (int)GenerateTruncatedFloatWithinRange(randomMult, min, max);
        }

        private static bool RemoveAffixesAtPosition(ItemSpec itemSpec, AffixPosition position)
        {
            if (itemSpec == null || itemSpec.AffixSpecs.Count == 0)
                return false;

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            bool removed = false;
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                if (affixSpec?.AffixProto != null && affixSpec.AffixProto.Position == position)
                {
                    removed = true;
                    continue;
                }

                affixSpecs.Add(new(affixSpec));
            }

            if (removed)
                itemSpec.SetAffixes(affixSpecs);

            return removed;
        }

        private static void TryApplyPreferredAffixes(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            bool requestedOmega,
            PrototypeId omegaRarityRef,
            OmegaTierItemTuning tuning)
        {
            if (resolver == null || filterArgs == null || itemSpec == null)
                return;

            if (requestedOmega == false || omegaRarityRef == PrototypeId.Invalid || tuning?.Enabled != true)
                return;

            if (tuning.MaxPreferredAffixesPerItem <= 0 || tuning.PreferredAffixChancePct <= 0f || tuning.PreferredAffixes.Count == 0)
                return;

            using var omegaFilterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments omegaFilterArgs);
            DropFilterArguments.Initialize(
                omegaFilterArgs,
                filterArgs.ItemProto,
                filterArgs.RollFor,
                filterArgs.Level,
                omegaRarityRef,
                filterArgs.Rank,
                filterArgs.Slot,
                filterArgs.LootContext);

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
                affixSpecs.Add(new(affixSpec));

            int nativePreferredCount = CountConfiguredPreferredAffixesPresent(affixSpecs, tuning, filterArgs.Slot);
            int appliedCount = 0;
            for (int attempt = 0; appliedCount < tuning.MaxPreferredAffixesPerItem && attempt < tuning.MaxPreferredAffixesPerItem * 4; attempt++)
            {
                if (resolver.Random.NextFloat() * 100f >= tuning.PreferredAffixChancePct)
                    break;

                AffixPrototype preferredAffixProto = PickPreferredAffix(resolver, omegaFilterArgs, itemSpec, affixSpecs, tuning, filterArgs.Slot);
                if (preferredAffixProto == null)
                    break;

                int replacementIndex = FindReplaceableAffixIndex(affixSpecs, preferredAffixProto, tuning);
                if (replacementIndex < 0)
                {
                    Logger.Warn($"Omega item preferred affix had no replaceable affix slot: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={preferredAffixProto.DataRef.GetNameFormatted()} existingAffixes={affixSpecs.Count}");
                    continue;
                }

                affixSpecs[replacementIndex] = new(preferredAffixProto, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                appliedCount++;
            }

            if (appliedCount <= 0)
            {
                if (nativePreferredCount > 0)
                    Logger.Info($"Omega item preferred affixes already present: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} native={nativePreferredCount}");
                return;
            }

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
            Logger.Info($"Omega item preferred affixes applied: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} native={nativePreferredCount} applied={appliedCount}");
        }

        private static void TryReplaceDisabledAffixes(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            bool requestedOmega,
            PrototypeId omegaRarityRef,
            OmegaTierItemTuning tuning)
        {
            if (requestedOmega == false || omegaRarityRef == PrototypeId.Invalid || tuning?.Enabled != true ||
                resolver == null || filterArgs == null || itemSpec == null || itemSpec.AffixSpecs.Count == 0)
            {
                return;
            }

            using var omegaFilterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments omegaFilterArgs);
            DropFilterArguments.Initialize(
                omegaFilterArgs,
                filterArgs.ItemProto,
                filterArgs.RollFor,
                filterArgs.Level,
                omegaRarityRef,
                filterArgs.Rank,
                filterArgs.Slot,
                filterArgs.LootContext);

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
                affixSpecs.Add(new(affixSpec));

            int replacedCount = 0;
            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixPrototype existingAffix = affixSpecs[i]?.AffixProto;
                if (existingAffix == null || tuning.IsAffixDisabled(existingAffix) == false)
                    continue;

                AffixPrototype replacementAffix = PickPreferredAffix(resolver, omegaFilterArgs, itemSpec, affixSpecs, tuning, filterArgs.Slot, existingAffix.Position);
                if (replacementAffix == null)
                {
                    Logger.Warn($"Omega reward disabled affix could not be replaced safely: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={existingAffix.DataRef.GetNameFormatted()}");
                    continue;
                }

                affixSpecs[i] = new(replacementAffix, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                replacedCount++;
            }

            if (replacedCount <= 0)
                return;

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
            Logger.Info($"Omega reward disabled affixes replaced: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} replaced={replacedCount}");
        }

        private static AffixPrototype PickOmegaRingExtraAffix(ItemResolver resolver, DropFilterArguments filterArgs, DropFilterArguments omegaFilterArgs, ItemSpec itemSpec, OmegaTierItemTuning tuning)
        {
            List<AffixPrototype> candidates = new();
            AddRingCategoryCandidates(RingOffenseT3CategoryName, filterArgs, omegaFilterArgs, itemSpec, candidates, tuning);
            AddRingCategoryCandidates(RingDefenseT3CategoryName, filterArgs, omegaFilterArgs, itemSpec, candidates, tuning);

            if (candidates.Count > 0)
                return candidates[resolver.Random.Next(0, candidates.Count)];

            PrototypeId ringOmegaAffixRef = GameDatabase.GetPrototypeRefByName(RingOmegaAffixName);
            AffixPrototype ringOmegaAffixProto = GameDatabase.GetPrototype<AffixPrototype>(ringOmegaAffixRef);
            if (ringOmegaAffixProto == null || ringOmegaAffixProto.Weight <= 0)
                return null;

            if (ContainsAffix(itemSpec.AffixSpecs, ringOmegaAffixRef))
                return null;

            return ringOmegaAffixProto.AllowAttachment(omegaFilterArgs) || ringOmegaAffixProto.AllowAttachment(filterArgs)
                ? ringOmegaAffixProto
                : null;
        }

        private static void AddRingCategoryCandidates(string categoryName, DropFilterArguments filterArgs, DropFilterArguments omegaFilterArgs, ItemSpec itemSpec, List<AffixPrototype> candidates, OmegaTierItemTuning tuning)
        {
            PrototypeId categoryRef = GameDatabase.GetPrototypeRefByName(categoryName);
            AffixCategoryPrototype categoryProto = GameDatabase.GetPrototype<AffixCategoryPrototype>(categoryRef);
            if (categoryProto == null)
                return;

            IReadOnlyList<AffixPrototype> affixes = GameDataTables.Instance.LootPickingTable.GetAffixesByCategory(categoryProto);
            if (affixes == null || affixes.Count == 0)
                return;

            foreach (AffixPrototype affixProto in affixes)
            {
                if (affixProto == null || affixProto.Weight <= 0)
                    continue;

                if (tuning?.IsAffixDisabled(affixProto) == true)
                    continue;

                if (ContainsAffix(itemSpec.AffixSpecs, affixProto.DataRef))
                    continue;

                if (affixProto.AllowAttachment(omegaFilterArgs) == false && affixProto.AllowAttachment(filterArgs) == false)
                    continue;

                candidates.Add(affixProto);
            }
        }

        private static bool ContainsAffix(IEnumerable<AffixSpec> affixSpecs, PrototypeId affixRef)
        {
            foreach (AffixSpec affixSpec in affixSpecs)
            {
                if (affixSpec?.AffixProto?.DataRef == affixRef)
                    return true;
            }

            return false;
        }

        private static AffixPrototype PickPreferredAffix(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            List<AffixSpec> currentAffixSpecs,
            OmegaTierItemTuning tuning,
            EquipmentInvUISlot slot,
            AffixPosition? requiredPosition = null)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || tuning?.PreferredAffixes == null)
                return null;

            List<(AffixPrototype AffixProto, int Weight)> candidates = new();
            int totalWeight = 0;
            foreach (OmegaTierPreferredAffixTuning preferredAffix in tuning.PreferredAffixes)
            {
                if (preferredAffix == null || preferredAffix.Weight <= 0 || preferredAffix.AllowsSlot(slot) == false)
                    continue;

                PrototypeId affixRef = GameDatabase.GetPrototypeRefByName(preferredAffix.Prototype);
                AffixPrototype affixProto = GameDatabase.GetPrototype<AffixPrototype>(affixRef);
                if (affixRef == PrototypeId.Invalid || affixProto == null)
                {
                    Logger.Warn($"Omega item preferred affix could not resolve: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={slot} affix={preferredAffix.Prototype}");
                    continue;
                }

                if (requiredPosition.HasValue && affixProto.Position != requiredPosition.Value)
                    continue;

                if (ContainsEquivalentPreferredAffix(currentAffixSpecs, affixProto) || tuning.IsAffixDisabled(affixProto))
                    continue;

                if (affixProto.AllowAttachment(filterArgs) == false)
                    continue;

                candidates.Add((affixProto, preferredAffix.Weight));
                totalWeight += preferredAffix.Weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0)
                return null;

            int roll = resolver.Random.Next(0, totalWeight);
            foreach ((AffixPrototype affixProto, int weight) in candidates)
            {
                if (roll < weight)
                    return affixProto;

                roll -= weight;
            }

            return candidates[^1].AffixProto;
        }

        private static int CountConfiguredPreferredAffixesPresent(List<AffixSpec> affixSpecs, OmegaTierItemTuning tuning, EquipmentInvUISlot slot)
        {
            if (affixSpecs == null || tuning?.PreferredAffixes == null)
                return 0;

            int count = 0;
            foreach (OmegaTierPreferredAffixTuning preferredAffix in tuning.PreferredAffixes)
            {
                if (preferredAffix == null || preferredAffix.AllowsSlot(slot) == false)
                    continue;

                AffixPrototype preferredAffixProto = ResolveAffix(preferredAffix.Prototype);
                if (preferredAffixProto != null && ContainsEquivalentPreferredAffix(affixSpecs, preferredAffixProto))
                    count++;
            }

            return count;
        }

        private static bool ContainsEquivalentPreferredAffix(IEnumerable<AffixSpec> affixSpecs, AffixPrototype preferredAffixProto)
        {
            if (affixSpecs == null || preferredAffixProto == null)
                return false;

            foreach (AffixSpec affixSpec in affixSpecs)
            {
                AffixPrototype existingAffixProto = affixSpec?.AffixProto;
                if (existingAffixProto == null)
                    continue;

                if (existingAffixProto.DataRef == preferredAffixProto.DataRef)
                    return true;

                if (existingAffixProto.Position == preferredAffixProto.Position &&
                    HasMatchingPropertyEntry(existingAffixProto, preferredAffixProto))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMatchingPropertyEntry(AffixPrototype existingAffixProto, AffixPrototype preferredAffixProto)
        {
            if (existingAffixProto?.PropertyEntries == null || preferredAffixProto?.PropertyEntries == null)
                return false;

            foreach (PropertyPickInRangeEntryPrototype existingEntry in existingAffixProto.PropertyEntries)
            {
                PropertyId existingProp = existingEntry?.Prop ?? PropertyId.Invalid;
                if (existingProp == PropertyId.Invalid)
                    continue;

                foreach (PropertyPickInRangeEntryPrototype preferredEntry in preferredAffixProto.PropertyEntries)
                {
                    PropertyId preferredProp = preferredEntry?.Prop ?? PropertyId.Invalid;
                    if (preferredProp == PropertyId.Invalid)
                        continue;

                    if (existingProp.Raw == preferredProp.Raw)
                        return true;

                    if (existingProp.HasParams == false &&
                        preferredProp.HasParams == false &&
                        existingProp.Enum == preferredProp.Enum)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryAddRandomOverrideAffixes(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            OmegaTierItemTuning tuning,
            OmegaTierItemOverrideTuning itemOverride,
            OmegaTierRandomAffixTuning randomAffix)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || randomAffix == null || randomAffix.Count <= 0)
                return false;

            if (Enum.TryParse(randomAffix.Position, ignoreCase: true, out AffixPosition position) == false ||
                position == AffixPosition.None)
            {
                Logger.Warn($"Omega item override random affix position is invalid: override={itemOverride?.Id} item={itemSpec.ItemProtoRef.GetNameFormatted()} position={randomAffix.Position}");
                return false;
            }

            IReadOnlyList<AffixPrototype> pool = GameDataTables.Instance.LootPickingTable.GetAffixesByPosition(position);
            if (pool == null || pool.Count == 0)
                return false;

            List<AffixPrototype> candidates = new();
            foreach (AffixPrototype affixProto in pool)
            {
                if (affixProto == null || affixProto.Weight <= 0)
                    continue;

                if (ContainsAffix(itemSpec.AffixSpecs, affixProto.DataRef))
                    continue;

                if (tuning?.IsAffixDisabled(affixProto) == true || itemOverride?.IsAffixDisabled(affixProto) == true)
                    continue;

                if (randomAffix.AllowInvalidAttachment == false && affixProto.AllowAttachment(filterArgs) == false)
                    continue;

                candidates.Add(affixProto);
            }

            bool changed = false;
            int added = 0;
            while (added < randomAffix.Count && candidates.Count > 0)
            {
                int candidateIndex = resolver.Random.Next(0, candidates.Count);
                AffixPrototype affixProto = candidates[candidateIndex];
                candidates.RemoveAt(candidateIndex);

                if (TryAddAffixSpec(resolver, filterArgs, itemSpec, affixProto, randomAffix.AllowInvalidAttachment) == false)
                    continue;

                added++;
                changed = true;
            }

            return changed;
        }

        private static bool TryAddAffixSpec(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            AffixPrototype affixProto,
            bool allowInvalidAttachment,
            bool maximizeRolls = false)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || affixProto == null)
                return false;

            if (allowInvalidAttachment == false && affixProto.AllowAttachment(filterArgs) == false)
                return false;

            HashSet<ScopedAffixRef> affixSet = BuildScopedAffixSet(itemSpec);
            using var pickerHandle = PickerPool<AffixPrototype>.Get(resolver.Random, out Picker<AffixPrototype> picker);
            picker.Add(affixProto, Math.Max(affixProto.Weight, 1));

            AffixSpec affixSpec = new();
            MutationResults result = affixSpec.RollAffix(resolver.Random, filterArgs.RollFor, itemSpec, picker, affixSet);
            if (result.HasFlag(MutationResults.Error) == false && affixSpec.IsValid)
            {
                if (maximizeRolls)
                    affixSpec.Seed = CreateMaxRollAffixSeed(resolver.Random, affixProto);

                return itemSpec.AddAffixSpec(affixSpec);
            }

            if (allowInvalidAttachment == false)
                return false;

            int seed = maximizeRolls
                ? CreateMaxRollAffixSeed(resolver.Random, affixProto)
                : resolver.Random.Next(1, int.MaxValue);
            return itemSpec.AddAffixSpec(new(affixProto, PrototypeId.Invalid, seed));
        }

        private static AffixSpec CreateReplacementAffixSpec(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            List<AffixSpec> affixSpecs,
            int replacementIndex,
            AffixPrototype affixProto,
            bool allowInvalidAttachment,
            bool maximizeRolls = false)
        {
            if (resolver == null || filterArgs == null || itemSpec == null || affixSpecs == null || affixProto == null)
                return null;

            if (allowInvalidAttachment == false && affixProto.AllowAttachment(filterArgs) == false)
                return null;

            HashSet<ScopedAffixRef> affixSet = BuildScopedAffixSet(affixSpecs, replacementIndex);
            using var pickerHandle = PickerPool<AffixPrototype>.Get(resolver.Random, out Picker<AffixPrototype> picker);
            picker.Add(affixProto, Math.Max(affixProto.Weight, 1));

            AffixSpec affixSpec = new();
            MutationResults result = affixSpec.RollAffix(resolver.Random, filterArgs.RollFor, itemSpec, picker, affixSet);
            if (result.HasFlag(MutationResults.Error) == false && affixSpec.IsValid)
            {
                if (maximizeRolls)
                    affixSpec.Seed = CreateMaxRollAffixSeed(resolver.Random, affixProto);

                return affixSpec;
            }

            return allowInvalidAttachment
                ? new(affixProto, PrototypeId.Invalid, maximizeRolls ? CreateMaxRollAffixSeed(resolver.Random, affixProto) : resolver.Random.Next(1, int.MaxValue))
                : null;
        }

        private static int CreateMaxRollAffixSeed(GRandom random, AffixPrototype affixProto)
        {
            const int LowMantissaMask = 0x7FFFFF;
            const int LowMantissaModulo = 0x800000;
            const int MultiplierLowBits = 698769069 & LowMantissaMask;
            const int InitialCarry = 666;

            int rollCount = Math.Max(1, affixProto?.PropertyEntries?.Length ?? 0);
            int inverse = ModularInverse(MultiplierLowBits, LowMantissaModulo);
            int seedLowBits = (int)(((long)((LowMantissaMask - InitialCarry) & LowMantissaMask) * inverse) & LowMantissaMask);

            if (rollCount == 1)
                return seedLowBits == 0 ? LowMantissaModulo : seedLowBits;

            int bestSeed = seedLowBits == 0 ? LowMantissaModulo : seedLowBits;
            float bestScore = -1f;
            int startOffset = random?.Next(0, 256) ?? 0;
            for (int i = 0; i < 256; i++)
            {
                int offset = (startOffset + i) % 256;
                int seed = seedLowBits + (offset * LowMantissaModulo);
                if (seed <= 0)
                    continue;

                GRandom candidateRandom = new(seed);
                float score = 1f;
                for (int roll = 0; roll < rollCount; roll++)
                    score = Math.Min(score, candidateRandom.NextFloat());

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSeed = seed;
                }
            }

            return bestSeed;
        }

        private static int ModularInverse(int value, int modulo)
        {
            int t = 0;
            int newT = 1;
            int r = modulo;
            int newR = value;

            while (newR != 0)
            {
                int quotient = r / newR;
                (t, newT) = (newT, t - quotient * newT);
                (r, newR) = (newR, r - quotient * newR);
            }

            return t < 0 ? t + modulo : t;
        }

        private static HashSet<ScopedAffixRef> BuildScopedAffixSet(ItemSpec itemSpec)
        {
            HashSet<ScopedAffixRef> affixSet = new();
            if (itemSpec == null)
                return affixSet;

            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                if (affixSpec?.AffixProto == null)
                    continue;

                affixSet.Add(new(affixSpec.AffixProto.DataRef, affixSpec.ScopeProtoRef));
            }

            return affixSet;
        }

        private static HashSet<ScopedAffixRef> BuildScopedAffixSet(List<AffixSpec> affixSpecs, int indexToSkip)
        {
            HashSet<ScopedAffixRef> affixSet = new();
            if (affixSpecs == null)
                return affixSet;

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                if (i == indexToSkip)
                    continue;

                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                affixSet.Add(new(affixSpec.AffixProto.DataRef, affixSpec.ScopeProtoRef));
            }

            return affixSet;
        }

        private static int FindOverrideReplacementAffixIndex(
            List<AffixSpec> affixSpecs,
            AffixPrototype replacementAffixProto,
            OmegaTierItemTuning tuning,
            OmegaTierAffixReplacementTuning replacement)
        {
            if (affixSpecs == null || replacementAffixProto == null || replacement == null)
                return -1;

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixPrototype existingAffixProto = affixSpecs[i]?.AffixProto;
                if (CanReplaceOverrideAffix(existingAffixProto, replacementAffixProto, tuning, replacement, requireSamePosition: true))
                    return i;
            }

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixPrototype existingAffixProto = affixSpecs[i]?.AffixProto;
                if (CanReplaceOverrideAffix(existingAffixProto, replacementAffixProto, tuning, replacement, requireSamePosition: false))
                    return i;
            }

            return -1;
        }

        internal static bool CanReplaceOverrideAffix(
            AffixPrototype existingAffixProto,
            AffixPrototype replacementAffixProto,
            OmegaTierItemTuning tuning,
            OmegaTierAffixReplacementTuning replacement,
            bool requireSamePosition)
        {
            if (existingAffixProto == null || replacementAffixProto == null || replacement == null)
                return false;

            if (existingAffixProto.DataRef == replacementAffixProto.DataRef)
                return false;

            if (replacement.PreservesAffix(existingAffixProto.DataRef) || replacement.PreservesPosition(existingAffixProto.Position))
                return false;

            if (replacement.PreserveProcAffixes && IsProcAffix(existingAffixProto))
                return false;

            if (replacement.PreserveConfiguredPreferredAffixes && IsConfiguredPreferredAffix(existingAffixProto.DataRef, tuning))
                return false;

            if (replacement.ReplaceAffixes.Count > 0 && replacement.MatchesReplaceAffix(existingAffixProto.DataRef) == false)
                return false;

            if (replacement.AllowsPosition(existingAffixProto.Position) == false)
                return false;

            if (requireSamePosition && existingAffixProto.Position != replacementAffixProto.Position)
                return false;

            if (replacement.ReplacePositions.Count == 0 &&
                existingAffixProto.Position != replacementAffixProto.Position &&
                existingAffixProto.Position != AffixPosition.Prefix &&
                existingAffixProto.Position != AffixPosition.Suffix &&
                existingAffixProto.Position != AffixPosition.Unique)
            {
                return false;
            }

            return true;
        }

        internal static bool IsProcAffix(AffixPrototype affixProto)
        {
            if (affixProto == null)
                return false;

            if (affixProto.Properties != null)
            {
                foreach (PropertyEnum procProperty in Property.ProcPropertyTypesAll)
                {
                    foreach (var _ in affixProto.Properties.IteratePropertyRange(procProperty))
                        return true;
                }

                foreach (var kvp in affixProto.Properties)
                {
                    if (IsProcSupportProperty(kvp.Key.Enum))
                        return true;
                }
            }

            if (affixProto.PropertyEntries != null)
            {
                foreach (PropertyPickInRangeEntryPrototype propertyEntry in affixProto.PropertyEntries)
                {
                    if (propertyEntry != null &&
                        (Property.ProcPropertyTypesAll.Contains(propertyEntry.Prop.Enum) || IsProcSupportProperty(propertyEntry.Prop.Enum)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsProcSupportProperty(PropertyEnum propertyEnum)
        {
            return propertyEnum == PropertyEnum.ProcChanceOverride ||
                propertyEnum == PropertyEnum.ProcPowerItemLevel ||
                propertyEnum == PropertyEnum.ProcPowerItemVariation ||
                propertyEnum == PropertyEnum.ProcPowerInvStackCount ||
                propertyEnum == PropertyEnum.ProcPowerRank ||
                propertyEnum == PropertyEnum.ProcCasterOverride ||
                propertyEnum == PropertyEnum.ProcTargetOverride;
        }

        internal static AffixPrototype ResolveAffix(string affixName)
        {
            if (string.IsNullOrWhiteSpace(affixName))
                return null;

            PrototypeId affixRef = GameDatabase.GetPrototypeRefByName(affixName.Trim());
            return affixRef.As<AffixPrototype>();
        }

        internal static PrototypeId ResolvePrototype(string prototypeName)
        {
            return string.IsNullOrWhiteSpace(prototypeName)
                ? PrototypeId.Invalid
                : GameDatabase.GetPrototypeRefByName(prototypeName.Trim());
        }

        internal static bool IsOmegaRarity(PrototypeId rarityProtoRef)
        {
            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            return omegaRarityRef != PrototypeId.Invalid && rarityProtoRef == omegaRarityRef;
        }

        private static bool IsSupportedOverrideLootContext(LootContext lootContext)
        {
            return lootContext.HasFlag(LootContext.Drop) ||
                lootContext.HasFlag(LootContext.MissionReward) ||
                lootContext.HasFlag(LootContext.MysteryChest);
        }

        private static bool IsUniqueItemSpec(ItemSpec itemSpec)
        {
            if (itemSpec == null)
                return false;

            if (itemSpec.RarityProtoRef == GameDatabase.LootGlobalsPrototype?.RarityUnique)
                return true;

            string itemName = itemSpec.ItemProtoRef.GetName();
            return itemName != null && itemName.Contains("/UniquePrototypes/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOmegaDifficulty(ItemResolver resolver, LootRollSettings settings)
        {
            PrototypeId difficultyTierRef = settings?.DifficultyTier ?? PrototypeId.Invalid;
            if (difficultyTierRef == PrototypeId.Invalid)
                difficultyTierRef = resolver?.Region?.DifficultyTierRef ?? PrototypeId.Invalid;

            if (difficultyTierRef == PrototypeId.Invalid)
                return false;

            if (difficultyTierRef == (PrototypeId)424700179461639950)
                return true;

            string difficultyName = difficultyTierRef.GetName();
            return difficultyName != null && difficultyName.Contains("Omega", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindReplaceableAffixIndex(List<AffixSpec> affixSpecs, AffixPrototype preferredAffixProto, OmegaTierItemTuning tuning)
        {
            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                if (IsConfiguredPreferredAffix(affixSpec.AffixProto.DataRef, tuning))
                    continue;

                if (affixSpec.AffixProto.Position == preferredAffixProto.Position)
                    return i;
            }

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                if (IsConfiguredPreferredAffix(affixSpec.AffixProto.DataRef, tuning))
                    continue;

                if (affixSpec.AffixProto.Position == AffixPosition.Prefix ||
                    affixSpec.AffixProto.Position == AffixPosition.Suffix)
                    return i;
            }

            return -1;
        }

        private static bool IsConfiguredPreferredAffix(PrototypeId affixRef, OmegaTierItemTuning tuning)
        {
            if (affixRef == PrototypeId.Invalid || tuning?.PreferredAffixes == null)
                return false;

            foreach (OmegaTierPreferredAffixTuning preferredAffix in tuning.PreferredAffixes)
            {
                if (preferredAffix == null || string.IsNullOrWhiteSpace(preferredAffix.Prototype))
                    continue;

                if (GameDatabase.GetPrototypeRefByName(preferredAffix.Prototype) == affixRef)
                    return true;
            }

            return false;
        }

#endif
    }
}
