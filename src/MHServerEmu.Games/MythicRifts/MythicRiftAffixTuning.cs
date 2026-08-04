using System.Text.Json;
using MHServerEmu.Core.Helpers;

namespace MHServerEmu.Games.MythicRifts
{
    public sealed class MythicRiftAffixTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/CosmicRiftAffixes.json";

        public static JsonSerializerOptions JsonOptions => MythicRiftRewardTuning.JsonOptions;

        public string ProfileName { get; set; } = "default-affixes";
        public bool Enabled { get; set; } = true;
        public int SecondRegionAffixStartLevel { get; set; } = 30;
        public int ThirdRegionAffixStartLevel { get; set; } = 70;
        public int SecondBossAffixStartWave { get; set; } = 30;
        public int ThirdBossAffixStartWave { get; set; } = 70;
        public string DefaultAffixTablePrototypeName { get; set; } = "Regions/Affixes/RegionAffixTable.defaults";

        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static MythicRiftAffixTuning CreateDefault()
        {
            MythicRiftAffixTuning tuning = new();
            tuning.Normalize();
            return tuning;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "default-affixes";

            SecondRegionAffixStartLevel = Math.Max(SecondRegionAffixStartLevel, 1);
            ThirdRegionAffixStartLevel = Math.Max(ThirdRegionAffixStartLevel, SecondRegionAffixStartLevel);
            SecondBossAffixStartWave = Math.Max(SecondBossAffixStartWave, 1);
            ThirdBossAffixStartWave = Math.Max(ThirdBossAffixStartWave, SecondBossAffixStartWave);

            if (string.IsNullOrWhiteSpace(DefaultAffixTablePrototypeName))
                DefaultAffixTablePrototypeName = "Regions/Affixes/RegionAffixTable.defaults";
        }

        public int GetAffixCount(int levelOrWave, bool useBossScopedAffixes)
        {
            if (Enabled == false)
                return 0;

            int secondAffixStart = useBossScopedAffixes ? SecondBossAffixStartWave : SecondRegionAffixStartLevel;
            int thirdAffixStart = useBossScopedAffixes ? ThirdBossAffixStartWave : ThirdRegionAffixStartLevel;

            int affixCount = 1;
            if (levelOrWave >= secondAffixStart)
                affixCount++;
            if (levelOrWave >= thirdAffixStart)
                affixCount++;

            return affixCount;
        }
    }
}
