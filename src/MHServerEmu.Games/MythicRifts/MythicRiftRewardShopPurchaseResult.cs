using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftRewardShopPurchaseResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public string OfferId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int SigilCost { get; init; }
        public int RemainingSigils { get; init; }
        public IReadOnlyList<PrototypeId> GrantedItems { get; init; } = Array.Empty<PrototypeId>();

        public static MythicRiftRewardShopPurchaseResult Failed(string errorMessage)
        {
            return new()
            {
                Success = false,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }
}
