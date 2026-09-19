using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;
using MHServerEmu.Games.Loot.Specs;

namespace MHServerEmu.Games.OmegaTierItems
{
    public static class OmegaGearLootFilter
    {
        private const string OmegaRarityName = "Entity/Items/Rarity/R6Omega.prototype";
        private const string RelativeDirectory = "PlayerLootFilters";
        private static readonly ConcurrentDictionary<ulong, OmegaLootFilterSettings> Settings = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static Dictionary<string, PrototypeId> _rarities;
        private static PrototypeId _omegaRarityRef = PrototypeId.Invalid;

        public static readonly string[] OmegaSlotKeys = ["gear01", "gear02", "gear03", "gear04", "gear05"];
        public static readonly string[] ThresholdKeys = ["ring", "medal", "insignia", "teamup", "catalyst"];

        public static OmegaLootFilterSettings Get(ulong playerDbId) => Settings.GetOrAdd(playerDbId, Load);

        public static void Save(ulong playerDbId)
        {
            string directory = Path.Combine(FileHelper.DataDirectory, RelativeDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(GetPath(directory, playerDbId), JsonSerializer.Serialize(Get(playerDbId), JsonOptions));
        }

        public static void Apply(Player player, LootResultSummary summary)
        {
            if (player == null || summary == null)
                return;

            OmegaLootFilterSettings settings = Get(player.DatabaseUniqueId);
            AgentPrototype avatarProto = player.CurrentAvatar?.AvatarPrototype;
            string avatarName = player.CurrentAvatar?.PrototypeDataRef.GetNameFormatted();
            OmegaLootFilterSection character = settings.GetCharacter(avatarName, create: false);
            summary.ItemSpecs.RemoveAll(itemSpec => ShouldFilter(itemSpec, avatarProto, settings.Global, character));
        }

        public static PrototypeId ResolveRarity(string name)
        {
            EnsureRarities();
            return name != null && _rarities.TryGetValue(name, out PrototypeId rarityRef) ? rarityRef : PrototypeId.Invalid;
        }

        public static IEnumerable<string> GetRarityNames()
        {
            EnsureRarities();
            return _rarities.Keys.OrderBy(name => name);
        }

        private static bool ShouldFilter(ItemSpec itemSpec, AgentPrototype avatarProto, OmegaLootFilterSection global, OmegaLootFilterSection character)
        {
            ItemPrototype itemProto = itemSpec?.ItemProtoRef.As<ItemPrototype>();
            if (itemProto == null)
                return false;

            EquipmentInvUISlot slot = itemProto is ArmorPrototype armorProto
                ? armorProto.DefaultEquipmentSlot
                : itemProto.GetInventorySlotForAgent(avatarProto);
            if (slot == EquipmentInvUISlot.Invalid)
                slot = itemProto.GetInventorySlotForAgent(avatarProto);

            bool isOmegaArmor = itemSpec.RarityProtoRef == GetOmegaRarity() || OmegaTierAffixLimits.HasArmorOmegaAffix(itemSpec);
            if (isOmegaArmor && slot >= EquipmentInvUISlot.Gear01 && slot <= EquipmentInvUISlot.Gear05)
            {
                string slotKey = slot.ToString().ToLowerInvariant();
                return global.OmegaGearSlots.Contains(slotKey) || character?.OmegaGearSlots.Contains(slotKey) == true;
            }

            string key = slot switch
            {
                EquipmentInvUISlot.Ring => "ring",
                EquipmentInvUISlot.Medal => "medal",
                EquipmentInvUISlot.Insignia => "insignia",
                _ => itemProto is TeamUpGearPrototype ? "teamup" : IsCatalyst(itemProto) ? "catalyst" : null
            };

            string thresholdName = GetEffectiveThreshold(global, character, key);
            if (thresholdName != null)
            {
                RarityPrototype itemRarity = itemSpec.RarityProtoRef.As<RarityPrototype>();
                RarityPrototype threshold = ResolveRarity(thresholdName).As<RarityPrototype>();
                if (itemRarity != null && threshold != null && itemRarity.Tier <= threshold.Tier)
                    return true;
            }

            return (global.FilterUruForged || character?.FilterUruForged == true) &&
                   itemSpec.RarityProtoRef == GameDatabase.LootGlobalsPrototype.RarityUruForged;
        }

        private static string GetEffectiveThreshold(OmegaLootFilterSection global, OmegaLootFilterSection character, string key)
        {
            if (key == null)
                return null;

            global.RarityThresholds.TryGetValue(key, out string globalName);
            string characterName = null;
            character?.RarityThresholds.TryGetValue(key, out characterName);
            RarityPrototype globalRarity = ResolveRarity(globalName).As<RarityPrototype>();
            RarityPrototype characterRarity = ResolveRarity(characterName).As<RarityPrototype>();
            if (characterRarity != null && (globalRarity == null || characterRarity.Tier > globalRarity.Tier))
                return characterName;
            return globalName ?? characterName;
        }

        private static bool IsCatalyst(ItemPrototype itemProto)
        {
            if (itemProto is not CostumeCorePrototype)
                return false;
            return itemProto.DataRef.GetName().Contains("Catalyst", StringComparison.OrdinalIgnoreCase);
        }

        private static PrototypeId GetOmegaRarity()
        {
            if (_omegaRarityRef == PrototypeId.Invalid)
                _omegaRarityRef = GameDatabase.GetPrototypeRefByName(OmegaRarityName);
            return _omegaRarityRef;
        }

        private static void EnsureRarities()
        {
            if (_rarities != null)
                return;
            _rarities = new(StringComparer.OrdinalIgnoreCase);
            foreach (PrototypeId rarityRef in DataDirectory.Instance.IteratePrototypesInHierarchy<RarityPrototype>(PrototypeIterateFlags.NoAbstractApprovedOnly))
            {
                string fileName = Path.GetFileNameWithoutExtension(rarityRef.GetName());
                _rarities[fileName] = rarityRef;
                string shortName = Regex.Replace(fileName, @"^R\d+", string.Empty);
                if (string.IsNullOrWhiteSpace(shortName) == false)
                    _rarities[shortName] = rarityRef;
            }
        }

        private static OmegaLootFilterSettings Load(ulong playerDbId)
        {
            string path = GetPath(Path.Combine(FileHelper.DataDirectory, RelativeDirectory), playerDbId);
            OmegaLootFilterSettings settings = File.Exists(path) ? FileHelper.DeserializeJson<OmegaLootFilterSettings>(path) : null;
            settings ??= new();
            settings.Normalize();
            return settings;
        }

        private static string GetPath(string directory, ulong playerDbId) => Path.Combine(directory, $"{playerDbId}.json");
    }

    public sealed class OmegaLootFilterSettings
    {
        public OmegaLootFilterSection Global { get; set; } = new();
        public Dictionary<string, OmegaLootFilterSection> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public OmegaLootFilterSection GetCharacter(string avatarName, bool create)
        {
            if (string.IsNullOrWhiteSpace(avatarName))
                return null;
            if (Characters.TryGetValue(avatarName, out OmegaLootFilterSection section) || create == false)
                return section;
            section = new();
            Characters[avatarName] = section;
            return section;
        }

        public void Normalize()
        {
            Global ??= new();
            Global.Normalize();
            Characters = new Dictionary<string, OmegaLootFilterSection>(Characters ?? [], StringComparer.OrdinalIgnoreCase);
            foreach (OmegaLootFilterSection section in Characters.Values)
                section?.Normalize();
        }
    }

    public sealed class OmegaLootFilterSection
    {
        public HashSet<string> OmegaGearSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RarityThresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool FilterUruForged { get; set; }

        public void Normalize()
        {
            OmegaGearSlots = new HashSet<string>(OmegaGearSlots ?? [], StringComparer.OrdinalIgnoreCase);
            RarityThresholds = new Dictionary<string, string>(RarityThresholds ?? [], StringComparer.OrdinalIgnoreCase);
        }
    }
}
