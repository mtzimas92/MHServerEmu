using MHServerEmu.Core.Collections;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.System.Random;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.PatchManager;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.GameData.Tables;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Properties;
using ReflectionPropertyInfo = System.Reflection.PropertyInfo;

namespace MHServerEmu.Games.OmegaTierItems
{
    public static class OmegaTierItemFactory
    {

        private const string CosmicRarityName = "Entity/Items/Rarity/R5Cosmic.prototype";
        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";
        private const int OmegaDifficultyItemLevel = 72;
        private const string OmegaChallengeAffixName = "Entity/Items/Affixes/LegendaryAffixes/StatAllAttributesBonus.prototype";
        private const string ArmorCosmicCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorCosmic.prototype";
        private const string ArmorOmegaCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorOmega.prototype";
        private const string RingOffensePrototypeName = "Entity/Items/Rings/RingOffenseLoot20.prototype";
        private const string RingDefensePrototypeName = "Entity/Items/Rings/RingDefenseLoot20.prototype";
        private const string RingOffenseT2CategoryName = "Entity/Items/Affixes/AffixCategories/RingOffenseT2.prototype";
        private const string RingDefenseT2CategoryName = "Entity/Items/Affixes/AffixCategories/RingDefenseT2.prototype";
        private const string PropertyPickInRangeEntryName = "Property/PropertyPickInRangeEntry.defaults";
        private const string LoadFloatName = "Eval/Constants/LoadFloat.defaults";

        private static readonly ReflectionPropertyInfo ItemPropertiesBuiltInProperty =
            typeof(ItemPrototype).GetProperty(nameof(ItemPrototype.PropertiesBuiltIn));

        private static readonly ReflectionPropertyInfo PropertyPickPropProperty =
            typeof(PropertyPickInRangeEntryPrototype).GetProperty(nameof(PropertyPickInRangeEntryPrototype.Prop));

        private static readonly ReflectionPropertyInfo PropertyPickValueMinProperty =
            typeof(PropertyPickInRangeEntryPrototype).GetProperty(nameof(PropertyPickInRangeEntryPrototype.ValueMin));

        private static readonly ReflectionPropertyInfo PropertyPickValueMaxProperty =
            typeof(PropertyPickInRangeEntryPrototype).GetProperty(nameof(PropertyPickInRangeEntryPrototype.ValueMax));

        private static readonly ReflectionPropertyInfo PropertyPickRollAsIntegerProperty =
            typeof(PropertyPickInRangeEntryPrototype).GetProperty(nameof(PropertyPickInRangeEntryPrototype.RollAsInteger));

        private static readonly ReflectionPropertyInfo LoadFloatValueProperty =
            typeof(LoadFloatPrototype).GetProperty(nameof(LoadFloatPrototype.Value));

        private static readonly HashSet<PrototypeId> ConfiguredBuiltInPrototypeOverrideRefs = new();
        private static readonly ConditionalWeakTable<ItemSpec, OmegaPromotionMarker> PendingOmegaPromotions = new();
        private static readonly OmegaPromotionMarker PromotionMarker = new();

        private sealed class OmegaPromotionMarker { }

        public static bool ShouldUseFactory(PrototypeId itemProtoRef, PrototypeId rarityProtoRef)
        {
            if (IsOmegaRarity(rarityProtoRef))
                return true;

            return OmegaTierItemTuning.Load().HasItemOverride(itemProtoRef, rarityProtoRef);
        }

        public static bool IsQueuedForOmegaPromotion(ItemSpec itemSpec)
        {
            return itemSpec != null && PendingOmegaPromotions.TryGetValue(itemSpec, out _);
        }

        public static void TryQueueCosmicDropForOmegaDifficulty(ItemResolver resolver, DropFilterArguments filterArgs, ItemSpec itemSpec)
        {
#if GAME_VERSION_1_52 || GAME_VERSION_1_53
            if (resolver == null || filterArgs == null || itemSpec == null || filterArgs.ItemProto is not ItemPrototype itemProto)
                return;

            if (IsSupportedOmegaPromotionLootContext(resolver.LootContext) == false ||
                IsSupportedOmegaPromotionSource(resolver) == false ||
                IsOmegaDifficulty(resolver, null) == false)
            {
                return;
            }

            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();

            EquipmentInvUISlot slot = filterArgs.Slot;
            if (slot == EquipmentInvUISlot.Invalid)
                slot = itemProto.GetInventorySlotForAgent(filterArgs.RollFor.As<AgentPrototype>());

            PrototypeId cosmicRarityRef = GameDatabase.GetPrototypeRefByName(CosmicRarityName);
            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            if (cosmicRarityRef == PrototypeId.Invalid || omegaRarityRef == PrototypeId.Invalid || filterArgs.Rarity != cosmicRarityRef)
                return;

            if (IsUniqueItemPrototype(itemProto))
                return;

            if (IsArmorSlotOneThroughFive(itemProto, slot) == false && slot != EquipmentInvUISlot.Ring)
                return;

            // Preserve the resolved slot for Omega finalization and downstream loot filtering.
            filterArgs.Slot = slot;

            float promotionChancePct = slot == EquipmentInvUISlot.Ring
                ? tuning.OmegaDifficultyRingPromotionChancePct
                : tuning.OmegaDifficultyPromotionChancePct;
            float promotionRoll = resolver.Random.NextFloat() * 100f;
            if (tuning?.Enabled != true ||
                promotionRoll >= promotionChancePct)
                return;

            PendingOmegaPromotions.Remove(itemSpec);
            PendingOmegaPromotions.Add(itemSpec, PromotionMarker);
#endif
        }

        public static bool TryFinalizeOmegaDropForOmegaDifficulty(ItemResolver resolver, ItemSpec itemSpec, LootRollSettings settings, PrototypeId rollFor)
        {
#if GAME_VERSION_1_52 || GAME_VERSION_1_53
            if (resolver == null || itemSpec == null || itemSpec.IsValid == false)
                return false;

            bool promoteToOmega = PendingOmegaPromotions.Remove(itemSpec);
            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();
            if (tuning?.Enabled != true || (promoteToOmega == false && IsOmegaRarity(itemSpec.RarityProtoRef) == false) ||
                IsSupportedOmegaPromotionLootContext(resolver.LootContext) == false ||
                IsSupportedOmegaPromotionSource(resolver) == false ||
                IsOmegaDifficulty(resolver, settings) == false)
            {
                return false;
            }

            ItemPrototype itemProto = itemSpec.ItemProtoRef.As<ItemPrototype>();
            if (itemProto == null || IsUniqueItemPrototype(itemProto))
                return false;

            AvatarPrototype avatarProto = resolver.Player?.CurrentAvatar?.AvatarPrototype;
            PrototypeId resolvedRollFor = rollFor != PrototypeId.Invalid
                ? rollFor
                : resolver.ResolveAvatarPrototype(avatarProto, forceUsable: true, usablePercent: 1f).DataRef;

            AgentPrototype rollForProto = resolvedRollFor.As<AgentPrototype>();
            EquipmentInvUISlot slot = itemProto.GetInventorySlotForAgent(rollForProto ?? avatarProto);
            if (IsArmorSlotOneThroughFive(itemProto, slot) == false && slot != EquipmentInvUISlot.Ring)
                return false;

            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            if (omegaRarityRef == PrototypeId.Invalid)
                return false;

            PrototypeId sourceRarityRef = itemSpec.RarityProtoRef;
            int sourceItemLevel = itemSpec.ItemLevel;
            RemoveNaturalChallengeAffixes(itemSpec, slot);
            itemSpec.RarityProtoRef = omegaRarityRef;
            itemSpec.ItemLevel = OmegaDifficultyItemLevel;

            using var filterArgsHandle = DropFilterArgumentsPool.Get(out DropFilterArguments filterArgs);
            DropFilterArguments.Initialize(filterArgs, itemProto, resolvedRollFor, Math.Max(sourceItemLevel, 1), sourceRarityRef, 0, slot, resolver.LootContext);

            TryReplaceDisabledAffixes(resolver, filterArgs, itemSpec, resolvedRollFor, requestedOmega: true, omegaRarityRef, tuning);
            TryApplyOmegaArmorAffix(resolver, filterArgs, itemSpec, resolvedRollFor, requestedOmega: true, omegaRarityRef, tuning);
            TryApplyOmegaRingAffix(resolver, filterArgs, itemSpec, requestedOmega: true, omegaRarityRef, tuning);
            TryApplyOmegaChallengeAffix(resolver, filterArgs, itemSpec, slot);
            return true;
#else
            return false;
#endif
        }

        public static bool HasConfiguredBuiltInPropertyPrototypeOverride(PrototypeId itemProtoRef, PrototypeId rarityProtoRef)
        {
            if (ConfiguredBuiltInPrototypeOverrideRefs.Contains(itemProtoRef) == false)
                return false;

            return OmegaTierItemTuning.Load().HasItemOverride(itemProtoRef, rarityProtoRef);
        }

        public static void ApplyConfiguredBuiltInPropertyPrototypeOverrides()
        {
            OmegaTierItemTuning tuning = OmegaTierItemTuning.Load();
            if (tuning?.Enabled != true || tuning.ItemOverrides.Count == 0)
                return;

            if (ItemPropertiesBuiltInProperty == null ||
                PropertyPickPropProperty == null ||
                PropertyPickValueMinProperty == null ||
                PropertyPickValueMaxProperty == null ||
                PropertyPickRollAsIntegerProperty == null ||
                LoadFloatValueProperty == null)
            {
                return;
            }

            foreach (OmegaTierItemOverrideTuning itemOverride in tuning.ItemOverrides)
            {
                if (itemOverride?.Enabled != true || HasConfiguredBuiltInPropertyOverride(itemOverride) == false)
                    continue;

                foreach (string itemPrototypeName in itemOverride.ItemPrototypes)
                {
                    PrototypeId itemProtoRef = ResolvePrototype(itemPrototypeName);
                    ItemPrototype itemProto = itemProtoRef.As<ItemPrototype>();
                    if (itemProto == null)
                    {
                        continue;
                    }

                    if (TryApplyConfiguredBuiltInPropertyPrototypeOverride(itemProto, itemOverride, out _) == false)
                        continue;

                    ConfiguredBuiltInPrototypeOverrideRefs.Add(itemProtoRef);
                }
            }
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
                if (HasConfiguredBuiltInPropertyPrototypeOverride(itemProtoRef, rarityProtoRef) == false)
                {
                    foreach (OmegaTierBuiltInPropertyTuning builtInProperty in itemOverride.BuiltInProperties)
                        TryApplyBuiltInProperty(item, itemOverride, builtInProperty);
                }

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
            int level)
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
                return null;

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
                return null;

            ItemSpec itemSpec = new(
                filterArgs.ItemProto.DataRef,
                filterArgs.Rarity,
                filterArgs.Level,
                0,
                Array.Empty<AffixSpec>(),
                resolver.Random.Next());

            MutationResults mutationResults = LootUtilities.UpdateAffixes(resolver, filterArgs, AffixCountBehavior.Roll, itemSpec, null);
            if (mutationResults.HasFlag(MutationResults.Error))
                return null;

            TryReplaceDisabledAffixes(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef, tuning);
            TryApplyOmegaArmorAffix(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef, tuning);
            TryApplyItemOverrides(resolver, filterArgs, itemSpec, rollFor, tuning);

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

            if (TryApplySlotAffixOverride(resolver, filterArgs, omegaFilterArgs, itemSpec, rollFor, tuning))
                return;

            List<AffixPrototype> candidates = new();
            bool hasCuratedOmegaAffixes = AddCuratedOmegaAffixCandidates(filterArgs, omegaFilterArgs, itemSpec.AffixSpecs, candidates, tuning, armorOmegaCategory);
            if (hasCuratedOmegaAffixes == false)
                AddAffixCategoryCandidates(ArmorOmegaCategoryName, filterArgs, omegaFilterArgs, itemSpec.AffixSpecs, candidates, tuning, armorOmegaCategory);

            if (candidates.Count == 0)
            {
                return;
            }

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
                affixSpecs.Add(new(affixSpec));

            AffixPrototype pickedAffixProto = candidates[resolver.Random.Next(0, candidates.Count)];
            int existingOmegaIndex = FindAffixIndexByCategory(affixSpecs, armorOmegaCategory);
            if (existingOmegaIndex >= 0)
            {
                AffixPrototype existingOmegaAffix = affixSpecs[existingOmegaIndex]?.AffixProto;
                if (existingOmegaAffix != null &&
                    tuning?.IsAffixDisabled(existingOmegaAffix) == false &&
                    IsCuratedOmegaAffix(existingOmegaAffix, tuning, filterArgs.Slot))
                {
                    return;
                }

                affixSpecs[existingOmegaIndex] = new(pickedAffixProto, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                itemSpec.SetAffixes(affixSpecs);
                itemSpec.OnAffixesRolled(resolver, rollFor);
                return;
            }

            MutationResults addResult = LootUtilities.AddAffix(resolver, omegaFilterArgs, itemSpec, pickedAffixProto);
            if (addResult.HasFlag(MutationResults.Error))
            {
                return;
            }
        }

        private static bool TryApplySlotAffixOverride(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            DropFilterArguments omegaFilterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            OmegaTierItemTuning tuning)
        {
            OmegaTierSlotAffixOverrideTuning slotOverride = tuning?.GetSlotAffixOverride(filterArgs.Slot);
            if (slotOverride == null)
                return false;

            if (slotOverride.ResolvedAffixes.Count != slotOverride.Affixes.Count || slotOverride.ResolvedAffixes.Count == 0)
                return false;

            PrototypeId cosmicCategoryRef = GameDatabase.GetPrototypeRefByName(ArmorCosmicCategoryName);
            AffixCategoryPrototype cosmicCategory = GameDatabase.GetPrototype<AffixCategoryPrototype>(cosmicCategoryRef);
            AffixPrototype naturalCosmicAffix = cosmicCategory == null
                ? null
                : itemSpec.AffixSpecs
                    .Select(spec => spec?.AffixProto)
                    .FirstOrDefault(affix => affix?.HasCategory(cosmicCategory) == true);

            AffixPrototype cosmicAffix = slotOverride.ResolvedCosmicAffix;
            if (cosmicAffix == null ||
                (cosmicAffix.AllowAttachment(omegaFilterArgs) == false && cosmicAffix.AllowAttachment(filterArgs) == false))
            {
                cosmicAffix = naturalCosmicAffix;
            }

            List<AffixSpec> affixSpecs = new(slotOverride.ResolvedAffixes.Count + (cosmicAffix != null ? 1 : 0));
            foreach (AffixPrototype affixProto in slotOverride.ResolvedAffixes)
                affixSpecs.Add(new(affixProto, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue)));

            if (cosmicAffix != null)
                affixSpecs.Add(new(cosmicAffix, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue)));

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
            return true;
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
                itemSpec.ItemLevel,
                omegaRarityRef,
                filterArgs.Rank,
                filterArgs.Slot,
                filterArgs.LootContext);

            ItemSpec omegaImplicitSpec = new(
                itemSpec.ItemProtoRef,
                omegaRarityRef,
                itemSpec.ItemLevel,
                affixSpecs: Array.Empty<AffixSpec>(),
                seed: resolver.Random.Next(1, int.MaxValue));
            MutationResults omegaImplicitResult = LootUtilities.UpdateAffixes(
                resolver,
                omegaFilterArgs,
                AffixCountBehavior.Roll,
                omegaImplicitSpec,
                null);
            if (omegaImplicitResult.HasFlag(MutationResults.Error))
                return;

            AffixSpec omegaImplicitAffix = omegaImplicitSpec.AffixSpecs
                .FirstOrDefault(spec => spec?.AffixProto?.Position == AffixPosition.Ultimate);
            if (omegaImplicitAffix == null)
                return;

            List<AffixSpec> rebuiltAffixes = [new(omegaImplicitAffix)];

            for (int i = 0; i < 2; i++)
            {
                AffixPrototype offenseAffix = PickOmegaRingAffixFromCategory(
                    resolver, filterArgs, omegaFilterArgs, rebuiltAffixes, tuning, RingOffenseT2CategoryName);
                if (offenseAffix == null)
                    return;

                rebuiltAffixes.Add(new(offenseAffix, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue)));
            }

            AffixPrototype defenseAffix = PickOmegaRingAffixFromCategory(
                resolver, filterArgs, omegaFilterArgs, rebuiltAffixes, tuning, RingDefenseT2CategoryName);
            if (defenseAffix == null)
                return;

            rebuiltAffixes.Add(new(defenseAffix, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue)));
            itemSpec.SetAffixes(rebuiltAffixes);
            itemSpec.OnAffixesRolled(resolver, filterArgs.RollFor);
        }

        private static bool IsArmorSlotOneThroughFive(ItemPrototype itemProto, EquipmentInvUISlot slot)
        {
            if (itemProto is not ArmorPrototype armorProto)
                return false;

            if (slot == EquipmentInvUISlot.Invalid)
                slot = armorProto.DefaultEquipmentSlot;

            return slot >= EquipmentInvUISlot.Gear01 && slot <= EquipmentInvUISlot.Gear05;
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
                    changed |= ClearExistingAffixes(itemSpec, itemOverride);
                }

                changed |= RemoveDisabledOverrideAffixes(itemSpec, itemOverride);
                changed |= TryApplyAffixReplacements(resolver, filterArgs, itemSpec, tuning, itemOverride);

                foreach (OmegaTierForcedAffixTuning forcedAffix in itemOverride.ForcedAffixes)
                {
                    if (forcedAffix == null || forcedAffix.Count <= 0)
                        continue;

                    AffixPrototype affixProto = forcedAffix.ResolvedAffixPrototype ?? ResolveAffix(forcedAffix.Prototype);
                    if (affixProto == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < forcedAffix.Count; i++)
                    {
                        if (forcedAffix.ReplaceExistingSamePosition)
                            changed |= RemoveAffixesAtPosition(itemSpec, affixProto.Position, itemOverride);

                        if (TryAddAffixSpec(resolver, filterArgs, itemSpec, affixProto, forcedAffix.AllowInvalidAttachment, itemOverride.MaximizeAffixRolls))
                            changed = true;
                    }
                }

                foreach (OmegaTierRandomAffixTuning randomAffix in itemOverride.RandomAffixes)
                    changed |= TryAddRandomOverrideAffixes(resolver, filterArgs, itemSpec, tuning, itemOverride, randomAffix);

                if (changed)
                {
                    itemSpec.OnAffixesRolled(resolver, rollFor);
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

        private static bool ClearExistingAffixes(ItemSpec itemSpec, OmegaTierItemOverrideTuning itemOverride)
        {
            if (itemSpec == null || itemOverride == null || itemSpec.AffixSpecs.Count == 0)
                return false;

            if (itemOverride.PreserveExistingAffixes.Count == 0 && itemOverride.PreserveProcAffixes == false)
            {
                itemSpec.SetAffixes(Array.Empty<AffixSpec>());
                return true;
            }

            List<AffixSpec> affixSpecs = new();
            bool removed = false;
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                AffixPrototype affixProto = affixSpec?.AffixProto;
                PrototypeId affixRef = affixProto?.DataRef ?? PrototypeId.Invalid;
                if (itemOverride.PreserveProcAffixes && IsProcAffix(affixProto))
                {
                    affixSpecs.Add(new(affixSpec));
                    continue;
                }

                if (itemOverride.PreservesExistingAffix(affixRef))
                {
                    affixSpecs.Add(new(affixSpec));
                    continue;
                }

                removed = true;
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
            foreach (OmegaTierAffixReplacementTuning replacement in itemOverride.AffixReplacements)
            {
                if (replacement == null || replacement.Count <= 0)
                    continue;

                AffixPrototype replacementAffixProto = replacement.ResolvedAffixPrototype ?? ResolveAffix(replacement.Prototype);
                if (replacementAffixProto == null)
                {
                    continue;
                }

                for (int i = 0; i < replacement.Count; i++)
                {
                    int replacementIndex = FindOverrideReplacementAffixIndex(affixSpecs, replacementAffixProto, tuning, replacement);
                    if (replacementIndex < 0)
                    {
                        continue;
                    }

                    AffixSpec replacementSpec = CreateReplacementAffixSpec(resolver, filterArgs, itemSpec, affixSpecs, replacementIndex, replacementAffixProto, replacement.AllowInvalidAttachment, itemOverride.MaximizeAffixRolls);
                    if (replacementSpec == null || replacementSpec.IsValid == false)
                    {
                        continue;
                    }

                    affixSpecs[replacementIndex] = replacementSpec;
                    changed = true;
                }
            }

            if (changed)
                itemSpec.SetAffixes(affixSpecs);

            return changed;
        }

        private static bool TryApplyConfiguredBuiltInPropertyPrototypeOverride(
            ItemPrototype itemProto,
            OmegaTierItemOverrideTuning itemOverride,
            out int appliedEntries)
        {
            appliedEntries = 0;
            if (itemProto == null || HasConfiguredBuiltInPropertyOverride(itemOverride) == false)
                return false;

            List<PropertyEntryPrototype> propertyEntries = new();
            if (itemProto.PropertiesBuiltIn.HasValue())
            {
                for (int i = 0; i < itemProto.PropertiesBuiltIn.Length; i++)
                {
                    PropertyEntryPrototype propertyEntry = itemProto.PropertiesBuiltIn[i];
                    if (propertyEntry == null)
                        continue;

                    if (itemOverride.ClearBuiltInProperties == false ||
                        itemOverride.PreservesBuiltInPropertyIndex(i) ||
                        (itemOverride.PreserveProcBuiltInProperties && IsProcBuiltInProperty(propertyEntry)))
                    {
                        propertyEntries.Add(propertyEntry);
                    }
                }
            }

            if (itemOverride.ReplaceBuiltInPropertiesFromTemplate)
            {
                PrototypeId templateRef = ResolvePrototype(itemOverride.BuiltInPropertyTemplateItemPrototype);
                ItemPrototype templateProto = templateRef.As<ItemPrototype>();
                if (templateProto?.PropertiesBuiltIn.HasValue() == true)
                {
                    if (itemOverride.BuiltInPropertyTemplateIndexes.Count > 0)
                    {
                        foreach (int index in itemOverride.BuiltInPropertyTemplateIndexes)
                        {
                            if (index >= templateProto.PropertiesBuiltIn.Length)
                                continue;

                            PropertyEntryPrototype propertyEntry = templateProto.PropertiesBuiltIn[index];
                            if (propertyEntry == null)
                                continue;

                            propertyEntries.Add(propertyEntry);
                            appliedEntries++;
                        }
                    }
                    else
                    {
                        foreach (PropertyEntryPrototype propertyEntry in templateProto.PropertiesBuiltIn)
                        {
                            if (propertyEntry == null ||
                                (itemOverride.CopyTemplateProcProperties && IsProcBuiltInProperty(propertyEntry) == false))
                            {
                                continue;
                            }

                            propertyEntries.Add(propertyEntry);
                            appliedEntries++;
                        }
                    }
                }
            }

            foreach (OmegaTierBuiltInPropertyTuning builtInProperty in itemOverride.BuiltInProperties)
            {
                PropertyPickInRangeEntryPrototype propertyEntry = CreateBuiltInPropertyEntry(itemOverride, builtInProperty);
                if (propertyEntry == null)
                    continue;

                propertyEntries.Add(propertyEntry);
                appliedEntries++;
            }

            if (appliedEntries == 0)
                return false;

            ItemPropertiesBuiltInProperty.SetValue(itemProto, propertyEntries.ToArray());
            return true;
        }

        private static bool HasConfiguredBuiltInPropertyOverride(OmegaTierItemOverrideTuning itemOverride)
        {
            return itemOverride != null &&
                ((itemOverride.BuiltInProperties?.Count ?? 0) > 0 ||
                 (itemOverride.ReplaceBuiltInPropertiesFromTemplate &&
                  string.IsNullOrWhiteSpace(itemOverride.BuiltInPropertyTemplateItemPrototype) == false));
        }

        private static PropertyPickInRangeEntryPrototype CreateBuiltInPropertyEntry(
            OmegaTierItemOverrideTuning itemOverride,
            OmegaTierBuiltInPropertyTuning builtInProperty)
        {
            if (TryResolveBuiltInPropertyId(itemOverride, builtInProperty, out PropertyId propertyId) == false)
                return null;

            PropertyPickInRangeEntryPrototype propertyEntry = AllocatePrototypeFromDefaults<PropertyPickInRangeEntryPrototype>(PropertyPickInRangeEntryName);
            if (propertyEntry == null)
            {
                return null;
            }

            PropertyPickPropProperty.SetValue(propertyEntry, propertyId);
            PropertyPickValueMinProperty.SetValue(propertyEntry, CreateLoadFloat(builtInProperty.ValueMin));
            PropertyPickValueMaxProperty.SetValue(propertyEntry, CreateLoadFloat(builtInProperty.ValueMax));
            PropertyPickRollAsIntegerProperty.SetValue(propertyEntry, builtInProperty.RollAsInteger);
            propertyEntry.PostProcess();
            return propertyEntry;
        }

        private static LoadFloatPrototype CreateLoadFloat(float value)
        {
            LoadFloatPrototype eval = AllocatePrototypeFromDefaults<LoadFloatPrototype>(LoadFloatName);
            if (eval == null)
                return null;

            LoadFloatValueProperty.SetValue(eval, value);
            eval.PostProcess();
            return eval;
        }

        private static T AllocatePrototypeFromDefaults<T>(string defaultsPrototypeName) where T : Prototype
        {
            Prototype instance = GameDatabase.PrototypeClassManager.AllocatePrototype(typeof(T));
            if (instance is not T typedInstance)
                return null;

            PrototypeId parentRef = ResolvePrototype(defaultsPrototypeName);
            if (parentRef != PrototypeId.Invalid)
            {
                CalligraphySerializer.CopyPrototypeDataRefFields(typedInstance, parentRef);
                typedInstance.ParentDataRef = parentRef;
            }

            return typedInstance;
        }

        private static bool IsProcBuiltInProperty(PropertyEntryPrototype propertyEntryProto)
        {
            PropertyId propertyId = propertyEntryProto switch
            {
                PropertyPickInRangeEntryPrototype pickInRangeProto => pickInRangeProto.Prop,
                PropertySetEntryPrototype setProto => setProto.Prop,
                _ => PropertyId.Invalid
            };

            return propertyId != PropertyId.Invalid && IsProcProperty(propertyId.Enum);
        }

        private static bool IsProcProperty(PropertyEnum propertyEnum)
        {
            return Property.ProcPropertyTypesAll.Contains(propertyEnum) ||
                propertyEnum == PropertyEnum.ProcChanceOverride ||
                propertyEnum == PropertyEnum.ProcPowerItemLevel ||
                propertyEnum == PropertyEnum.ProcPowerItemVariation ||
                propertyEnum == PropertyEnum.ProcPowerInvStackCount ||
                propertyEnum == PropertyEnum.ProcPowerRank ||
                propertyEnum == PropertyEnum.ProcCasterOverride ||
                propertyEnum == PropertyEnum.ProcTargetOverride;
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
                return false;
            }

            try
            {
                propertyId = PatchEntryConverter.ParseJsonPropertyIdSinglePublic(builtInProperty.Property);
            }
            catch (Exception)
            {
                return false;
            }

            if (propertyId == PropertyId.Invalid)
            {
                return false;
            }

            return true;
        }

        private static bool RemoveConfiguredProcPowers(Item item, OmegaTierItemOverrideTuning itemOverride)
        {
            if (item == null || itemOverride?.DisabledProcPowerRefs == null || itemOverride.DisabledProcPowerRefs.Count == 0)
                return false;

            using var propertiesToRemoveHandle = ListPool<PropertyId>.Get(out List<PropertyId> propertiesToRemove);
            using var procPropertiesHandle = PropertyCollectionPool.Get(out PropertyCollection procProperties);
            foreach (PropertyEnum procProperty in Property.ProcPropertyTypesAll)
                procProperties.CopyPropertyRange(item.Properties, procProperty);

            foreach (var kvp in procProperties)
            {
                Property.FromParam(kvp.Key, 1, out PrototypeId procPowerRef);
                if (itemOverride.DisabledProcPowerRefs.Contains(procPowerRef))
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
                return false;
            }

            PrototypeId procPowerRef = ResolvePrototype(procKeywordProperty.Power);
            PrototypeId keywordRef = ResolvePrototype(procKeywordProperty.Keyword);
            if (procPowerRef == PrototypeId.Invalid || keywordRef == PrototypeId.Invalid)
            {
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

        private static bool RemoveAffixesAtPosition(ItemSpec itemSpec, AffixPosition position, OmegaTierItemOverrideTuning itemOverride = null)
        {
            if (itemSpec == null || itemSpec.AffixSpecs.Count == 0)
                return false;

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            bool removed = false;
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                if (affixSpec?.AffixProto != null && affixSpec.AffixProto.Position == position)
                {
                    if (itemOverride?.PreserveProcAffixes == true && IsProcAffix(affixSpec.AffixProto))
                    {
                        affixSpecs.Add(new(affixSpec));
                        continue;
                    }

                    if (itemOverride?.PreservesExistingAffix(affixSpec.AffixProto.DataRef) == true)
                    {
                        affixSpecs.Add(new(affixSpec));
                        continue;
                    }

                    removed = true;
                    continue;
                }

                affixSpecs.Add(new(affixSpec));
            }

            if (removed)
                itemSpec.SetAffixes(affixSpecs);

            return removed;
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

                AffixSpec disabledAffixSpec = affixSpecs[i];
                affixSpecs[i] = null;
                AffixPrototype replacementAffix = PickDisabledAffixReplacement(resolver, filterArgs, omegaFilterArgs, affixSpecs, tuning, existingAffix);
                if (replacementAffix == null)
                {
                    affixSpecs[i] = disabledAffixSpec;
                    continue;
                }

                affixSpecs[i] = new(replacementAffix, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                replacedCount++;
            }

            if (replacedCount <= 0)
                return;

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
        }

        private static AffixPrototype PickDisabledAffixReplacement(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            DropFilterArguments omegaFilterArgs,
            List<AffixSpec> currentAffixSpecs,
            OmegaTierItemTuning tuning,
            AffixPrototype existingAffix)
        {
            if (resolver == null || filterArgs == null || omegaFilterArgs == null || existingAffix == null)
                return null;

            string categoryName = null;
            if (existingAffix.Position == AffixPosition.Cosmic &&
                IsArmorSlotOneThroughFive(filterArgs.ItemProto as ItemPrototype, filterArgs.Slot))
            {
                categoryName = ArmorCosmicCategoryName;
            }
            else if (IsArmorSlotOneThroughFive(filterArgs.ItemProto as ItemPrototype, filterArgs.Slot))
            {
                PrototypeId armorOmegaCategoryRef = GameDatabase.GetPrototypeRefByName(ArmorOmegaCategoryName);
                AffixCategoryPrototype armorOmegaCategory = GameDatabase.GetPrototype<AffixCategoryPrototype>(armorOmegaCategoryRef);
                if (armorOmegaCategory != null && existingAffix.HasCategory(armorOmegaCategory))
                    categoryName = ArmorOmegaCategoryName;
            }

            if (categoryName == null)
                return null;

            List<AffixPrototype> candidates = new();
            AddAffixCategoryCandidates(categoryName, filterArgs, omegaFilterArgs, currentAffixSpecs, candidates, tuning, requiredPosition: existingAffix.Position);
            if (candidates.Count == 0)
                return null;

            return candidates[resolver.Random.Next(0, candidates.Count)];
        }

        private static AffixPrototype PickOmegaRingAffixFromCategory(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            DropFilterArguments omegaFilterArgs,
            IEnumerable<AffixSpec> currentAffixes,
            OmegaTierItemTuning tuning,
            string categoryName)
        {
            List<AffixPrototype> candidates = new();
            AddAffixCategoryCandidates(
                categoryName,
                filterArgs,
                omegaFilterArgs,
                currentAffixes,
                candidates,
                tuning);

            return candidates.Count == 0
                ? null
                : candidates[resolver.Random.Next(0, candidates.Count)];
        }

        private static bool AddCuratedOmegaAffixCandidates(
            DropFilterArguments filterArgs,
            DropFilterArguments omegaFilterArgs,
            IEnumerable<AffixSpec> currentAffixSpecs,
            List<AffixPrototype> candidates,
            OmegaTierItemTuning tuning,
            AffixCategoryPrototype armorOmegaCategory)
        {
            if (filterArgs == null || omegaFilterArgs == null || candidates == null || tuning?.PreferredAffixes == null)
                return false;

            bool hasConfiguredForSlot = false;
            foreach (OmegaTierPreferredAffixTuning preferredAffix in tuning.PreferredAffixes)
            {
                if (preferredAffix == null || preferredAffix.AllowsSlot(filterArgs.Slot) == false || string.IsNullOrWhiteSpace(preferredAffix.Prototype))
                    continue;

                hasConfiguredForSlot = true;
                AffixPrototype affixProto = preferredAffix.ResolvedAffixPrototype ?? ResolveAffix(preferredAffix.Prototype);
                if (affixProto == null || affixProto.Weight <= 0)
                    continue;

                if (tuning.IsAffixDisabled(affixProto) || ContainsEquivalentPreferredAffix(currentAffixSpecs, affixProto))
                    continue;

                if (affixProto.AllowAttachment(omegaFilterArgs) == false && affixProto.AllowAttachment(filterArgs) == false)
                    continue;

                candidates.Add(affixProto);
            }

            return hasConfiguredForSlot;
        }

        private static void AddAffixCategoryCandidates(
            string categoryName,
            DropFilterArguments filterArgs,
            DropFilterArguments omegaFilterArgs,
            IEnumerable<AffixSpec> currentAffixSpecs,
            List<AffixPrototype> candidates,
            OmegaTierItemTuning tuning,
            AffixCategoryPrototype categoryProto = null,
            AffixPosition? requiredPosition = null)
        {
            if (candidates == null)
                return;

            if (categoryProto == null)
            {
                PrototypeId categoryRef = GameDatabase.GetPrototypeRefByName(categoryName);
                categoryProto = GameDatabase.GetPrototype<AffixCategoryPrototype>(categoryRef);
            }

            if (categoryProto == null)
                return;

            IReadOnlyList<AffixPrototype> affixes = GameDataTables.Instance.LootPickingTable.GetAffixesByCategory(categoryProto);
            if (affixes == null || affixes.Count == 0)
                return;

            foreach (AffixPrototype affixProto in affixes)
            {
                if (affixProto == null || affixProto.Weight <= 0)
                    continue;

                if (requiredPosition.HasValue && affixProto.Position != requiredPosition.Value)
                    continue;

                if (tuning?.IsAffixDisabled(affixProto) == true)
                    continue;

                if (ContainsEquivalentPreferredAffix(currentAffixSpecs, affixProto))
                    continue;

                if (affixProto.AllowAttachment(omegaFilterArgs) == false && affixProto.AllowAttachment(filterArgs) == false)
                    continue;

                candidates.Add(affixProto);
            }
        }

        private static int FindAffixIndexByCategory(List<AffixSpec> affixSpecs, AffixCategoryPrototype categoryProto)
        {
            if (affixSpecs == null || categoryProto == null)
                return -1;

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixPrototype affixProto = affixSpecs[i]?.AffixProto;
                if (affixProto != null && affixProto.HasCategory(categoryProto))
                    return i;
            }

            return -1;
        }

        private static bool IsCuratedOmegaAffix(AffixPrototype affixProto, OmegaTierItemTuning tuning, EquipmentInvUISlot slot)
        {
            if (affixProto == null || tuning?.PreferredAffixes == null)
                return false;

            foreach (OmegaTierPreferredAffixTuning preferredAffix in tuning.PreferredAffixes)
            {
                if (preferredAffix == null || preferredAffix.AllowsSlot(slot) == false)
                    continue;

                PrototypeId preferredAffixRef = preferredAffix.ResolvedAffixPrototype?.DataRef ?? ResolvePrototype(preferredAffix.Prototype);
                if (preferredAffixRef != PrototypeId.Invalid && affixProto.DataRef == preferredAffixRef)
                    return true;
            }

            return false;
        }

        private static bool IsConfiguredPreferredAffix(PrototypeId affixRef, OmegaTierItemTuning tuning)
        {
            return affixRef != PrototypeId.Invalid && tuning?.PreferredAffixRefs.Contains(affixRef) == true;
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

        private static bool TryApplyOmegaChallengeAffix(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            EquipmentInvUISlot slot)
        {
            if (itemSpec == null || IsArmorSlotOneThroughFive(itemSpec.ItemProtoRef.As<ItemPrototype>(), slot) == false)
                return false;

            PrototypeId affixProtoRef = GameDatabase.GetPrototypeRefByName(OmegaChallengeAffixName);
            if (affixProtoRef == PrototypeId.Invalid || ContainsAffix(itemSpec.AffixSpecs, affixProtoRef))
                return false;

            AffixPrototype affixProto = affixProtoRef.As<AffixPrototype>();
            return affixProto != null &&
                TryAddAffixSpec(resolver, filterArgs, itemSpec, affixProto, allowInvalidAttachment: true);
        }

        private static void RemoveNaturalChallengeAffixes(ItemSpec itemSpec, EquipmentInvUISlot slot)
        {
            if (itemSpec == null || IsArmorSlotOneThroughFive(itemSpec.ItemProtoRef.As<ItemPrototype>(), slot) == false)
                return;

            List<AffixSpec> keptAffixes = new(itemSpec.AffixSpecs.Count);
            int removed = 0;
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
            {
                if (affixSpec?.AffixProto?.Position == AffixPosition.Unique)
                {
                    removed++;
                    continue;
                }

                keptAffixes.Add(new(affixSpec));
            }

            if (removed > 0)
                itemSpec.SetAffixes(keptAffixes);
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

            if (replacement.ReplaceAffixIndexes.Count > 0)
            {
                foreach (int index in replacement.ReplaceAffixIndexes)
                {
                    if (index < 0 || index >= affixSpecs.Count)
                        continue;

                    AffixPrototype existingAffixProto = affixSpecs[index]?.AffixProto;
                    if (CanReplaceOverrideAffix(existingAffixProto, replacementAffixProto, tuning, replacement, requireSamePosition: false, requireConfiguredIndex: true))
                        return index;
                }

                return -1;
            }

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
            bool requireSamePosition,
            bool requireConfiguredIndex = false)
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

            if (requireConfiguredIndex == false &&
                replacement.ReplacePositions.Count == 0 &&
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

        private static bool IsSupportedOmegaPromotionLootContext(LootContext lootContext)
        {
            if (lootContext.HasFlag(LootContext.MysteryChest))
                return false;

            if (lootContext.HasFlag(LootContext.MissionReward))
                return true;

            return lootContext.HasFlag(LootContext.Drop);
        }

        private static bool IsSupportedOmegaPromotionSource(ItemResolver resolver)
        {
            if (resolver?.LootContext.HasFlag(LootContext.MissionReward) == true)
                return RiftAccessTeleportService.IsOmegaPatrolRegion(resolver.Region);

            AgentPrototype sourceProto = resolver?.SourceEntityPrototypeRef.As<AgentPrototype>();
            return sourceProto != null && sourceProto is not AvatarPrototype;
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

        private static bool IsUniqueItemPrototype(ItemPrototype itemProto)
        {
            if (itemProto == null)
                return false;

            string itemName = itemProto.DataRef.GetName();
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

#endif
    }
}
