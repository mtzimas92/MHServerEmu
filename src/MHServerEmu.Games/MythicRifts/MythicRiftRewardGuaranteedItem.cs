using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Loot;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftRewardGuaranteedItem
    {
        public string Id { get; init; }
        public PrototypeId ItemProtoRef { get; init; }
        public bool IsAgentReward { get; init; }
        public int Quantity { get; init; }
        public int ItemLevel { get; init; } = 1;
        public PrototypeId RarityProtoRef { get; init; }
        public string Delivery { get; init; } = "ground";
        public IReadOnlyList<PrototypeId> CandidateItemProtoRefs { get; init; }
        public IReadOnlyList<EquipmentInvUISlot> CandidateAllowedEquipmentSlots { get; init; }
        public EquipmentInvUISlot CandidateRequestedEquipmentSlot { get; init; } = EquipmentInvUISlot.Invalid;
    }
}
