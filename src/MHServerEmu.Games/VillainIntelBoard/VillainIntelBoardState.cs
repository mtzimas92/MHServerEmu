namespace MHServerEmu.Games.VillainIntelBoard
{
    public sealed class VillainIntelBoardState
    {
        public List<VillainIntelBoardEntry> Entries { get; } = new();
        public int ReviewIndex { get; set; }

        public bool NeedsReroll(int desiredSize)
        {
            return Entries.Count == 0 ||
                   Entries.All(entry => entry.Status == VillainIntelBoardEntryStatus.RewardCollected);
        }
    }
}
