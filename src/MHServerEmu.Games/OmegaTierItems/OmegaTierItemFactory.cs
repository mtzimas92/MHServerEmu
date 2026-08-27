using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.GameData.Tables;
using MHServerEmu.Games.Loot;

namespace MHServerEmu.Games.OmegaTierItems
{
    public static class OmegaTierItemFactory
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";
        private const string ArmorOmegaCategoryName = "Entity/Items/Affixes/AffixCategories/ArmorOmega.prototype";
        private const string RingOmegaAffixName = "Entity/Items/Affixes/RingAffixes/RingLoot20/BuiltInHP/RingHealthOmega.prototype";
        private const string RingOffenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingOffenseT3.prototype";
        private const string RingDefenseT3CategoryName = "Entity/Items/Affixes/AffixCategories/RingDefenseT3.prototype";

        private static readonly string[] PreferredGear24AffixNames =
        [
            "Entity/Items/Affixes/ArmorAffixes/Loot20/CritChance/CritChanceT3.prototype",
            "Entity/Items/Affixes/ArmorAffixes/Loot20/CritDamage/CritDamageT3.prototype",
            "Entity/Items/Affixes/ArmorAffixes/Loot20/BrutalDamage/BrutalDamageT3.prototype"
        ];

        public static ItemSpec CreateItemSpec(
            Game game,
            PrototypeId itemProtoRef,
            PrototypeId rarityProtoRef,
            LootContext lootContext,
            Player player,
            int level,
            bool logFailures = true)
        {
            if (rarityProtoRef == PrototypeId.Invalid)
                return game?.LootManager?.CreateItemSpec(itemProtoRef, lootContext, player, level);

            ItemPrototype itemProto = itemProtoRef.As<ItemPrototype>();
            if (itemProto == null || game == null)
                return null;

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
            bool requestedOmega = omegaRarityRef != PrototypeId.Invalid && rarityProtoRef == omegaRarityRef;

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

            TryApplyPreferredGear24Affixes(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef);
            TryApplyOmegaArmorAffix(resolver, filterArgs, itemSpec, rollFor, requestedOmega, omegaRarityRef);
            TryApplyOmegaRingAffix(resolver, filterArgs, itemSpec, requestedOmega, omegaRarityRef);

            if (requestedOmega && itemSpec.RarityProtoRef == rarityProtoRef)
                Logger.Info($"Omega reward created: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={slot} rarity={rarityProtoRef.GetNameFormatted()} affixes={itemSpec.AffixSpecs.Count}");

            return itemSpec;
        }

#if GAME_VERSION_1_52 || GAME_VERSION_1_53
        private static void TryApplyOmegaArmorAffix(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            bool requestedOmega,
            PrototypeId omegaRarityRef)
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
            PrototypeId omegaRarityRef)
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

            AffixPrototype pickedAffixProto = PickOmegaRingExtraAffix(resolver, filterArgs, omegaFilterArgs, itemSpec);
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

        private static void TryApplyPreferredGear24Affixes(
            ItemResolver resolver,
            DropFilterArguments filterArgs,
            ItemSpec itemSpec,
            PrototypeId rollFor,
            bool requestedOmega,
            PrototypeId omegaRarityRef)
        {
            if (resolver == null || filterArgs == null || itemSpec == null)
                return;

            if (requestedOmega == false || omegaRarityRef == PrototypeId.Invalid)
                return;

            if (filterArgs.Slot != EquipmentInvUISlot.Gear02 && filterArgs.Slot != EquipmentInvUISlot.Gear04)
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

            int appliedCount = 0;
            foreach (string preferredAffixName in PreferredGear24AffixNames)
            {
                PrototypeId preferredAffixRef = GameDatabase.GetPrototypeRefByName(preferredAffixName);
                AffixPrototype preferredAffixProto = GameDatabase.GetPrototype<AffixPrototype>(preferredAffixRef);
                if (preferredAffixRef == PrototypeId.Invalid || preferredAffixProto == null)
                {
                    Logger.Warn($"Omega item preferred affix could not resolve: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={preferredAffixName}");
                    continue;
                }

                if (ContainsAffix(affixSpecs, preferredAffixRef))
                    continue;

                if (preferredAffixProto.AllowAttachment(omegaFilterArgs) == false)
                {
                    Logger.Warn($"Omega item preferred affix is not attachable: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={preferredAffixRef.GetNameFormatted()} rarity={omegaFilterArgs.Rarity.GetNameFormatted()} effectiveRarity={filterArgs.Rarity.GetNameFormatted()}");
                    continue;
                }

                int replacementIndex = FindReplaceableAffixIndex(affixSpecs, preferredAffixProto);
                if (replacementIndex < 0)
                {
                    Logger.Warn($"Omega item preferred affix had no replaceable affix slot: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} affix={preferredAffixRef.GetNameFormatted()} existingAffixes={affixSpecs.Count}");
                    continue;
                }

                affixSpecs[replacementIndex] = new(preferredAffixProto, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                appliedCount++;
            }

            if (appliedCount <= 0)
                return;

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
            Logger.Info($"Omega item preferred affixes applied: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} applied={appliedCount}");
        }

        private static AffixPrototype PickOmegaRingExtraAffix(ItemResolver resolver, DropFilterArguments filterArgs, DropFilterArguments omegaFilterArgs, ItemSpec itemSpec)
        {
            List<AffixPrototype> candidates = new();
            AddRingCategoryCandidates(RingOffenseT3CategoryName, filterArgs, omegaFilterArgs, itemSpec, candidates);
            AddRingCategoryCandidates(RingDefenseT3CategoryName, filterArgs, omegaFilterArgs, itemSpec, candidates);

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

        private static void AddRingCategoryCandidates(string categoryName, DropFilterArguments filterArgs, DropFilterArguments omegaFilterArgs, ItemSpec itemSpec, List<AffixPrototype> candidates)
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
        private static int FindReplaceableAffixIndex(List<AffixSpec> affixSpecs, AffixPrototype preferredAffixProto)
        {
            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                if (IsPreferredGear24Affix(affixSpec.AffixProto.DataRef))
                    continue;

                if (affixSpec.AffixProto.Position == preferredAffixProto.Position)
                    return i;
            }

            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                if (IsPreferredGear24Affix(affixSpec.AffixProto.DataRef))
                    continue;

                if (affixSpec.AffixProto.Position == AffixPosition.Prefix ||
                    affixSpec.AffixProto.Position == AffixPosition.Suffix)
                    return i;
            }

            return -1;
        }

        private static bool IsPreferredGear24Affix(PrototypeId affixRef)
        {
            foreach (string preferredAffixName in PreferredGear24AffixNames)
            {
                if (GameDatabase.GetPrototypeRefByName(preferredAffixName) == affixRef)
                    return true;
            }

            return false;
        }
#endif
    }
}
