using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;

namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftItemPresentation
    {
        public const string StandardPresentationPrototypeName = "DangerRoomScenarioCrateUniqueCableFight";
        public const string StandardPresentationPrototypePath = "Entity/Items/Consumables/Prototypes/DangerRoom/DangerRoomScenarioCrateUniqueCableFight.prototype";
        public const string StandardPresentationDisplayName = "Cosmic Rift Scenario";
        public const ulong StandardPresentationPrototypeId = 17067585073904428862UL;
        public const string EndlessPresentationPrototypeName = "TestHearthStone";
        public const string EndlessPresentationPrototypePath = "Entity/Items/Consumables/Prototypes/Test/TestHearthStone.prototype";
        public const string EndlessPresentationDisplayName = "Rift Gauntlet Scenario";
        public const ulong EndlessPresentationPrototypeId = 16713492285336591108UL;
        public const string BossGauntletPresentationPrototypeName = "TestStunKit";
        public const string BossGauntletPresentationPrototypePath = "Entity/Items/Consumables/Prototypes/Test/TestStunKit.prototype";
        public const string BossGauntletPresentationDisplayName = "Boss Gauntlet Scenario";
        public const ulong BossGauntletPresentationPrototypeId = 12266096679867324785UL;
        public const ulong PresentationVisualRarityPrototypeId = 6033964048325414744UL;

        public const string PresentationPrototypeName = StandardPresentationPrototypeName;
        public const string PresentationPrototypePath = StandardPresentationPrototypePath;
        public const string PresentationDisplayName = StandardPresentationDisplayName;
        public const ulong PresentationPrototypeId = StandardPresentationPrototypeId;

        public static ItemSpec ApplyLauncherPresentation(ItemSpec itemSpec, MythicRiftMode mode = MythicRiftMode.Standard)
        {
            if (itemSpec == null)
                return null;

            PrototypeId presentationProtoRef = ResolvePresentationPrototypeRef(mode);
            if (presentationProtoRef.As<ItemPrototype>() == null)
                return itemSpec;

            PrototypeId visualRarityProtoRef = ResolvePresentationVisualRarityRef();
            if (visualRarityProtoRef.As<RarityPrototype>() == null)
                return itemSpec;

            return new ItemSpec(
                presentationProtoRef,
                visualRarityProtoRef,
                itemSpec.ItemLevel,
                itemSpec.CreditsAmount,
                itemSpec.AffixSpecs,
                itemSpec.Seed,
                itemSpec.EquippableBy)
            {
                StackCount = itemSpec.StackCount
            };
        }

        public static PrototypeId ResolvePresentationPrototypeRef(MythicRiftMode mode = MythicRiftMode.Standard)
        {
            string prototypePath = mode switch
            {
                MythicRiftMode.Endless => EndlessPresentationPrototypePath,
                MythicRiftMode.BossGauntlet => BossGauntletPresentationPrototypePath,
                _ => StandardPresentationPrototypePath
            };
            string prototypeName = mode switch
            {
                MythicRiftMode.Endless => EndlessPresentationPrototypeName,
                MythicRiftMode.BossGauntlet => BossGauntletPresentationPrototypeName,
                _ => StandardPresentationPrototypeName
            };
            ulong prototypeId = mode switch
            {
                MythicRiftMode.Endless => EndlessPresentationPrototypeId,
                MythicRiftMode.BossGauntlet => BossGauntletPresentationPrototypeId,
                _ => StandardPresentationPrototypeId
            };

            PrototypeId prototypeRef = GameDatabase.GetPrototypeRefByName(prototypePath);
            if (prototypeRef.As<ItemPrototype>() != null)
                return prototypeRef;

            prototypeRef = GameDatabase.GetPrototypeRefByName(prototypeName);
            if (prototypeRef.As<ItemPrototype>() != null)
                return prototypeRef;

            prototypeRef = (PrototypeId)prototypeId;
            return prototypeRef.As<ItemPrototype>() != null
                ? prototypeRef
                : PrototypeId.Invalid;
        }

        public static PrototypeId ResolvePresentationVisualRarityRef()
        {
            PrototypeId rarityProtoRef = (PrototypeId)PresentationVisualRarityPrototypeId;
            return rarityProtoRef.As<RarityPrototype>() != null
                ? rarityProtoRef
                : PrototypeId.Invalid;
        }
    }
}
