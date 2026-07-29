namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftBossWaveSelector
    {
        public static IReadOnlyList<MythicRiftContentEntry> BuildDistinctRoster(
            MythicRiftContentEntry primaryBoss,
            IEnumerable<MythicRiftContentEntry> eligibleBosses,
            int requestedCount,
            Func<int, int> nextIndex,
            IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            List<MythicRiftContentEntry> roster = new();
            int targetCount = Math.Max(requestedCount, 1);

            AddIfDistinct(roster, primaryBoss, excludedBossFamilies);
            if (eligibleBosses == null || nextIndex == null)
                return roster;

            List<MythicRiftContentEntry> remaining = eligibleBosses
                .Where(candidate => candidate?.HasValidBossSource == true)
                .Where(candidate => IsDistinct(roster, candidate, excludedBossFamilies))
                .ToList();

            while (roster.Count < targetCount && remaining.Count > 0)
            {
                int index = Math.Clamp(nextIndex(remaining.Count), 0, remaining.Count - 1);
                MythicRiftContentEntry selected = remaining[index];
                remaining.RemoveAt(index);
                AddIfDistinct(roster, selected, excludedBossFamilies);
                remaining.RemoveAll(candidate => IsDistinct(roster, candidate, excludedBossFamilies) == false);
            }

            if (roster.Count < targetCount && excludedBossFamilies?.Count > 0)
            {
                remaining = eligibleBosses
                    .Where(candidate => candidate?.HasValidBossSource == true)
                    .Where(candidate => IsDistinct(roster, candidate))
                    .ToList();

                while (roster.Count < targetCount && remaining.Count > 0)
                {
                    int index = Math.Clamp(nextIndex(remaining.Count), 0, remaining.Count - 1);
                    MythicRiftContentEntry selected = remaining[index];
                    remaining.RemoveAt(index);
                    AddIfDistinct(roster, selected);
                    remaining.RemoveAll(candidate => IsDistinct(roster, candidate) == false);
                }
            }

            return roster;
        }

        private static void AddIfDistinct(List<MythicRiftContentEntry> roster, MythicRiftContentEntry candidate, IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            if (candidate?.HasValidBossSource == true && IsDistinct(roster, candidate, excludedBossFamilies))
                roster.Add(candidate);
        }

        private static bool IsDistinct(IEnumerable<MythicRiftContentEntry> roster, MythicRiftContentEntry candidate, IReadOnlyCollection<string> excludedBossFamilies = null)
        {
            if (candidate == null)
                return false;

            string candidateFamily = NormalizeBossFamily(candidate.BossFamily);
            if (string.IsNullOrWhiteSpace(candidateFamily) == false &&
                excludedBossFamilies?.Any(excluded => string.Equals(NormalizeBossFamily(excluded), candidateFamily, StringComparison.OrdinalIgnoreCase)) == true)
            {
                return false;
            }

            return roster.All(existing =>
                string.Equals(existing.Id, candidate.Id, StringComparison.OrdinalIgnoreCase) == false &&
                existing.BossProtoRef != candidate.BossProtoRef &&
                AreSameBossFamily(existing, candidate) == false);
        }

        private static bool AreSameBossFamily(MythicRiftContentEntry left, MythicRiftContentEntry right)
        {
            string leftFamily = NormalizeBossFamily(left?.BossFamily);
            string rightFamily = NormalizeBossFamily(right?.BossFamily);
            return string.IsNullOrWhiteSpace(leftFamily) == false &&
                   string.Equals(leftFamily, rightFamily, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeBossFamily(string family)
        {
            return string.IsNullOrWhiteSpace(family) ? string.Empty : family.Trim().ToLowerInvariant();
        }
    }
}
