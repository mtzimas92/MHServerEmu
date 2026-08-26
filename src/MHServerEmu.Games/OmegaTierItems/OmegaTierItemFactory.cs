using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;

namespace MHServerEmu.Games.OmegaTierItems
{
    public static class OmegaTierItemFactory
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";

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

            TryApplyPreferredGear24Affixes(resolver, filterArgs, itemSpec, rollFor);

            return itemSpec;
        }

#if GAME_VERSION_1_52 || GAME_VERSION_1_53
        private static void TryApplyPreferredGear24Affixes(ItemResolver resolver, DropFilterArguments filterArgs, ItemSpec itemSpec, PrototypeId rollFor)
        {
            if (resolver == null || filterArgs == null || itemSpec == null)
                return;

            PrototypeId omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            if (omegaRarityRef == PrototypeId.Invalid || filterArgs.Rarity != omegaRarityRef)
                return;

            if (filterArgs.Slot != EquipmentInvUISlot.Gear02 && filterArgs.Slot != EquipmentInvUISlot.Gear04)
                return;

            ItemPrototype itemProto = itemSpec.ItemProtoRef.As<ItemPrototype>();
            AffixLimitsPrototype affixLimits = itemProto?.GetAffixLimits(filterArgs.Rarity, filterArgs.LootContext);
            if (affixLimits?.CategorizedAffixes.IsNullOrEmpty() != false)
                return;

            List<AffixSpec> affixSpecs = new(itemSpec.AffixSpecs.Count);
            foreach (AffixSpec affixSpec in itemSpec.AffixSpecs)
                affixSpecs.Add(new(affixSpec));

            int appliedCount = 0;
            foreach (string preferredAffixName in PreferredGear24AffixNames)
            {
                PrototypeId preferredAffixRef = GameDatabase.GetPrototypeRefByName(preferredAffixName);
                AffixPrototype preferredAffixProto = GameDatabase.GetPrototype<AffixPrototype>(preferredAffixRef);
                if (preferredAffixRef == PrototypeId.Invalid || preferredAffixProto == null)
                    continue;

                if (ContainsAffix(affixSpecs, preferredAffixRef))
                    continue;

                if (preferredAffixProto.AllowAttachment(filterArgs) == false)
                    continue;

                AffixCategoryPrototype replacementCategory = FindReplacementCategory(preferredAffixProto, affixLimits);
                if (replacementCategory == null)
                    continue;

                int replacementIndex = FindReplaceableAffixIndex(affixSpecs, replacementCategory);
                if (replacementIndex < 0)
                    continue;

                affixSpecs[replacementIndex] = new(preferredAffixProto, PrototypeId.Invalid, resolver.Random.Next(1, int.MaxValue));
                appliedCount++;
            }

            if (appliedCount <= 0)
                return;

            itemSpec.SetAffixes(affixSpecs);
            itemSpec.OnAffixesRolled(resolver, rollFor);
            Logger.Info($"Omega item preferred affixes applied: item={itemSpec.ItemProtoRef.GetNameFormatted()} slot={filterArgs.Slot} applied={appliedCount}");
        }

        private static bool ContainsAffix(List<AffixSpec> affixSpecs, PrototypeId affixRef)
        {
            foreach (AffixSpec affixSpec in affixSpecs)
            {
                if (affixSpec?.AffixProto?.DataRef == affixRef)
                    return true;
            }

            return false;
        }

        private static AffixCategoryPrototype FindReplacementCategory(AffixPrototype affixProto, AffixLimitsPrototype affixLimits)
        {
            foreach (CategorizedAffixEntryPrototype entry in affixLimits.CategorizedAffixes)
            {
                if (entry?.Category == null || entry.MinAffixes <= 0)
                    continue;

                if (affixProto.HasCategory(entry.Category))
                    return entry.Category;
            }

            return null;
        }

        private static int FindReplaceableAffixIndex(List<AffixSpec> affixSpecs, AffixCategoryPrototype category)
        {
            for (int i = 0; i < affixSpecs.Count; i++)
            {
                AffixSpec affixSpec = affixSpecs[i];
                if (affixSpec?.AffixProto == null)
                    continue;

                if (IsPreferredGear24Affix(affixSpec.AffixProto.DataRef))
                    continue;

                if (affixSpec.AffixProto.HasCategory(category))
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
