using MHServerEmu.Core.Serialization;
using MHServerEmu.Games.Common;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class OmegaContentRewardProgress : ISerialize
    {
        private Dictionary<ulong, OmegaContentRewardClaim> _claims = new();

        public int GetClaimedAmount(string rewardId, long periodMarker)
        {
            ulong key = GetStableKey(rewardId);
            return _claims.TryGetValue(key, out OmegaContentRewardClaim claim) && claim.PeriodMarker == periodMarker
                ? Math.Max(claim.Amount, 0)
                : 0;
        }

        public void AddClaimedAmount(string rewardId, long periodMarker, int amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId) || amount <= 0)
                return;

            ulong key = GetStableKey(rewardId);
            int currentAmount = GetClaimedAmount(rewardId, periodMarker);
            _claims[key] = new OmegaContentRewardClaim
            {
                PeriodMarker = periodMarker,
                Amount = checked(currentAmount + amount)
            };
        }

        public bool Serialize(Archive archive)
        {
            return Serializer.Transfer(archive, ref _claims);
        }

        private static ulong GetStableKey(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (char character in value?.Trim().ToLowerInvariant() ?? string.Empty)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }
    }

    public sealed class OmegaContentRewardClaim : ISerialize
    {
        public long PeriodMarker;
        public int Amount;

        public bool Serialize(Archive archive)
        {
            bool success = true;
            success &= Serializer.Transfer(archive, ref PeriodMarker);
            success &= Serializer.Transfer(archive, ref Amount);
            return success;
        }
    }
}
