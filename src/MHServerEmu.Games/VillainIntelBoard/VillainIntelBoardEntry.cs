using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.VillainIntelBoard
{
    public enum VillainIntelBoardEntryStatus
    {
        Available,
        Active,
        Defeated,
        RewardCollected,
        Failed
    }

    public sealed class VillainIntelBoardEntry
    {
        public int SlotIndex { get; init; }
        public int Rank { get; init; }
        public string ThemeId { get; init; }
        public string ThemeName { get; init; }
        public string TargetId { get; init; }
        public string TargetName { get; init; }
        public string RegionId { get; init; }
        public string RegionName { get; init; }
        public bool HeroBased { get; init; }
        public PrototypeId TargetProtoRef { get; init; }
        public PrototypeId RegionProtoRef { get; init; }
        public List<string> RewardTags { get; init; } = new();
        public VillainIntelBoardEntryStatus Status { get; set; } = VillainIntelBoardEntryStatus.Available;

        public bool CanAccept => Status == VillainIntelBoardEntryStatus.Available || Status == VillainIntelBoardEntryStatus.Failed;
        public bool CanCollect => Status == VillainIntelBoardEntryStatus.Defeated;
    }
}
