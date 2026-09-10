using MHServerEmu.Core.Serialization;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.MythicRifts
{
    public class MythicRiftPlayerProgress : ISerialize
    {
        private int _standardRiftLeaderboardResetMarker = 0;
        private Dictionary<ulong, int> _standardHighestUnlockedLevelByAvatar = new();
        private Dictionary<ulong, int> _endlessHighestUnlockedLevelByAvatar = new();
        private Dictionary<ulong, int> _endlessCompletedCyclesByAvatar = new();
        private Dictionary<ulong, int> _standardRiftLeaderboardResetMarkerByAvatar = new();
        private Dictionary<ulong, int> _omegaPatrolAccessByAvatar = new();

        public int GetStandardHighestUnlockedLevel(Avatar avatar)
        {
            return GetValueForAvatar(avatar, _standardHighestUnlockedLevelByAvatar, 1, 1);
        }

        public void SetStandardHighestUnlockedLevel(Avatar avatar, int unlockedLevel)
        {
            SetValueForAvatar(avatar, _standardHighestUnlockedLevelByAvatar, unlockedLevel, 1);
        }

        public int GetEndlessHighestUnlockedLevel(Avatar avatar)
        {
            return GetValueForAvatar(avatar, _endlessHighestUnlockedLevelByAvatar, 1, 1);
        }

        public void SetEndlessHighestUnlockedLevel(Avatar avatar, int unlockedLevel)
        {
            SetValueForAvatar(avatar, _endlessHighestUnlockedLevelByAvatar, unlockedLevel, 1);
        }

        public int GetEndlessCompletedCycles(Avatar avatar)
        {
            return GetValueForAvatar(avatar, _endlessCompletedCyclesByAvatar, 0, 0);
        }

        public void SetEndlessCompletedCycles(Avatar avatar, int completedCycles)
        {
            SetValueForAvatar(avatar, _endlessCompletedCyclesByAvatar, completedCycles, 0);
        }

        public bool HasStandardLeaderboardSeasonReset(int seasonMarker)
        {
            return _standardRiftLeaderboardResetMarker == seasonMarker;
        }

        public void SetStandardLeaderboardSeasonReset(int seasonMarker)
        {
            _standardRiftLeaderboardResetMarker = seasonMarker;
        }

        public bool HasStandardLeaderboardSeasonReset(Avatar avatar, int seasonMarker)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            return avatarKey != 0
                && _standardRiftLeaderboardResetMarkerByAvatar.TryGetValue(avatarKey, out int storedMarker)
                && storedMarker == seasonMarker;
        }

        public void SetStandardLeaderboardSeasonReset(Avatar avatar, int seasonMarker)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            if (avatarKey == 0)
                return;

            _standardRiftLeaderboardResetMarkerByAvatar[avatarKey] = seasonMarker;
        }

        public bool HasOmegaPatrolAccess(Avatar avatar)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            return avatarKey != 0
                && _omegaPatrolAccessByAvatar.TryGetValue(avatarKey, out int accessUnlocked)
                && accessUnlocked != 0;
        }

        public void SetOmegaPatrolAccess(Avatar avatar)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            if (avatarKey == 0)
                return;

            _omegaPatrolAccessByAvatar[avatarKey] = 1;
        }

        public bool Serialize(Archive archive)
        {
            bool success = true;

            success &= Serializer.Transfer(archive, ref _standardRiftLeaderboardResetMarker);
            success &= TransferIntDictionary(archive, _standardHighestUnlockedLevelByAvatar);
            success &= TransferIntDictionary(archive, _endlessHighestUnlockedLevelByAvatar);
            success &= TransferIntDictionary(archive, _endlessCompletedCyclesByAvatar);
            success &= TransferIntDictionary(archive, _standardRiftLeaderboardResetMarkerByAvatar);
            if (archive.IsPacking || archive.Version >= ArchiveVersion.AddedRiftAvatarProgress)
                success &= TransferIntDictionary(archive, _omegaPatrolAccessByAvatar);

            return success;
        }

        private static ulong GetAvatarKey(Avatar avatar)
        {
            return avatar != null && avatar.PrototypeDataRef != PrototypeId.Invalid
                ? (ulong)avatar.PrototypeDataRef
                : 0;
        }

        private static int GetValueForAvatar(Avatar avatar, Dictionary<ulong, int> valuesByAvatar, int defaultValue, int minValue)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            if (avatarKey == 0)
                return Math.Max(defaultValue, minValue);

            return valuesByAvatar.TryGetValue(avatarKey, out int value)
                ? Math.Max(value, minValue)
                : Math.Max(defaultValue, minValue);
        }

        private static void SetValueForAvatar(Avatar avatar, Dictionary<ulong, int> valuesByAvatar, int value, int minValue)
        {
            ulong avatarKey = GetAvatarKey(avatar);
            if (avatarKey == 0)
                return;

            valuesByAvatar[avatarKey] = Math.Max(value, minValue);
        }

        private static bool TransferIntDictionary(Archive archive, Dictionary<ulong, int> valuesByAvatar)
        {
            bool success = true;
            uint count = (uint)(valuesByAvatar?.Count ?? 0);
            success &= Serializer.Transfer(archive, ref count);

            if (archive.IsPacking)
            {
                if (valuesByAvatar == null)
                    return success;

                foreach (var kvp in valuesByAvatar)
                {
                    ulong avatarPrototypeRef = kvp.Key;
                    int value = kvp.Value;
                    success &= Serializer.Transfer(archive, ref avatarPrototypeRef);
                    success &= Serializer.Transfer(archive, ref value);
                }

                return success;
            }

            valuesByAvatar.Clear();
            for (uint i = 0; i < count; i++)
            {
                ulong avatarPrototypeRef = 0;
                int value = 0;
                success &= Serializer.Transfer(archive, ref avatarPrototypeRef);
                success &= Serializer.Transfer(archive, ref value);

                if (avatarPrototypeRef != 0)
                    valuesByAvatar[avatarPrototypeRef] = value;
            }

            return success;
        }
    }
}
