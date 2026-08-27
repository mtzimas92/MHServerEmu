using System.Text.Json;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Loot;

namespace MHServerEmu.Games.OmegaTierItems
{
    public sealed class OmegaTierItemTuning
    {
        public const string RelativeConfigPath = "Game/MythicRift/OmegaTierItems.json";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };

        private static OmegaTierItemTuning _cached;
        private static DateTime _cachedWriteTimeUtc;

        public string ProfileName { get; set; } = "default-omega-items";
        public bool Enabled { get; set; } = true;
        public float PreferredAffixChancePct { get; set; } = 100f;
        public int MaxPreferredAffixesPerItem { get; set; } = 2;
        public List<OmegaTierPreferredAffixTuning> PreferredAffixes { get; set; } = new();
        public List<string> DisabledAffixes { get; set; } = new();
        public List<string> DisabledAffixCategories { get; set; } = new();
        public List<OmegaTierItemOverrideTuning> ItemOverrides { get; set; } = new();

        public static string ConfigPath => Path.Combine(FileHelper.DataDirectory, RelativeConfigPath);

        public static OmegaTierItemTuning Load()
        {
            try
            {
                string configPath = ConfigPath;
                DateTime writeTimeUtc = File.Exists(configPath)
                    ? File.GetLastWriteTimeUtc(configPath)
                    : DateTime.MinValue;

                if (_cached != null && writeTimeUtc == _cachedWriteTimeUtc)
                    return _cached;

                OmegaTierItemTuning tuning = File.Exists(configPath)
                    ? FileHelper.DeserializeJson<OmegaTierItemTuning>(configPath, JsonOptions)
                    : null;

                tuning ??= CreateDefault();
                tuning.Normalize();
                _cached = tuning;
                _cachedWriteTimeUtc = writeTimeUtc;
                return _cached;
            }
            catch
            {
                OmegaTierItemTuning tuning = CreateDefault();
                tuning.Normalize();
                _cached = tuning;
                _cachedWriteTimeUtc = DateTime.MinValue;
                return _cached;
            }
        }

        public static OmegaTierItemTuning CreateDefault()
        {
            OmegaTierItemTuning tuning = new()
            {
                ProfileName = "default-omega-items",
                PreferredAffixChancePct = 100f,
                MaxPreferredAffixesPerItem = 2,
                PreferredAffixes =
                [
                    new()
                    {
                        Prototype = "Entity/Items/Affixes/ArmorAffixes/Loot20/CritChance/CritChanceT3.prototype",
                        Weight = 10
                    },
                    new()
                    {
                        Prototype = "Entity/Items/Affixes/ArmorAffixes/Loot20/CritDamage/CritDamageT3.prototype",
                        Weight = 10
                    },
                    new()
                    {
                        Prototype = "Entity/Items/Affixes/ArmorAffixes/Loot20/BrutalDamage/BrutalDamageT3.prototype",
                        Weight = 10
                    }
                ]
            };

            return tuning;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "default-omega-items";

            PreferredAffixChancePct = Math.Clamp(PreferredAffixChancePct, 0f, 100f);
            MaxPreferredAffixesPerItem = Math.Max(MaxPreferredAffixesPerItem, 0);
            PreferredAffixes ??= new();
            DisabledAffixes ??= new();
            DisabledAffixCategories ??= new();
            ItemOverrides ??= new();

            foreach (OmegaTierPreferredAffixTuning preferredAffix in PreferredAffixes)
                preferredAffix?.Normalize();

            foreach (OmegaTierItemOverrideTuning itemOverride in ItemOverrides)
                itemOverride?.Normalize();

            DisabledAffixes = DisabledAffixes
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            DisabledAffixCategories = DisabledAffixCategories
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool IsAffixDisabled(AffixPrototype affixProto)
        {
            if (affixProto == null)
                return false;

            foreach (string affixName in DisabledAffixes)
            {
                PrototypeId disabledAffixRef = GameDatabase.GetPrototypeRefByName(affixName);
                if (disabledAffixRef != PrototypeId.Invalid && disabledAffixRef == affixProto.DataRef)
                    return true;
            }

#if GAME_VERSION_1_52 || GAME_VERSION_1_53
            foreach (string categoryName in DisabledAffixCategories)
            {
                PrototypeId categoryRef = GameDatabase.GetPrototypeRefByName(categoryName);
                AffixCategoryPrototype categoryProto = GameDatabase.GetPrototype<AffixCategoryPrototype>(categoryRef);
                if (categoryProto != null && affixProto.HasCategory(categoryProto))
                    return true;
            }
#endif

            return false;
        }

        public bool HasItemOverride(PrototypeId itemProtoRef, PrototypeId rarityProtoRef)
        {
            if (Enabled == false || itemProtoRef == PrototypeId.Invalid || ItemOverrides.Count == 0)
                return false;

            foreach (OmegaTierItemOverrideTuning itemOverride in ItemOverrides)
            {
                if (itemOverride?.Matches(itemProtoRef, rarityProtoRef) == true)
                    return true;
            }

            return false;
        }
    }

    public sealed class OmegaTierPreferredAffixTuning
    {
        public string Prototype { get; set; }
        public int Weight { get; set; } = 1;
        public List<string> Slots { get; set; } = new();

        public void Normalize()
        {
            Prototype = string.IsNullOrWhiteSpace(Prototype) ? string.Empty : Prototype.Trim();
            Weight = Math.Max(Weight, 0);
            Slots ??= new();
            Slots = Slots
                .Where(slot => string.IsNullOrWhiteSpace(slot) == false)
                .Select(slot => slot.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool AllowsSlot(EquipmentInvUISlot slot)
        {
            if (Slots.Count == 0)
                return true;

            foreach (string slotName in Slots)
            {
                if (Enum.TryParse(slotName, ignoreCase: true, out EquipmentInvUISlot configuredSlot) &&
                    configuredSlot == slot)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class OmegaTierItemOverrideTuning
    {
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public List<string> ItemPrototypes { get; set; } = new();
        public List<string> Rarities { get; set; } = new();
        public bool ClearExistingAffixes { get; set; } = false;
        public List<string> DisabledAffixes { get; set; } = new();
        public List<OmegaTierForcedAffixTuning> ForcedAffixes { get; set; } = new();
        public List<OmegaTierRandomAffixTuning> RandomAffixes { get; set; } = new();

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? "unnamed-item-override" : Id.Trim();
            ItemPrototypes = NormalizeStringList(ItemPrototypes);
            Rarities = NormalizeStringList(Rarities);
            DisabledAffixes = NormalizeStringList(DisabledAffixes);
            ForcedAffixes ??= new();
            RandomAffixes ??= new();

            foreach (OmegaTierForcedAffixTuning forcedAffix in ForcedAffixes)
                forcedAffix?.Normalize();

            foreach (OmegaTierRandomAffixTuning randomAffix in RandomAffixes)
                randomAffix?.Normalize();
        }

        public bool Matches(PrototypeId itemProtoRef, PrototypeId rarityProtoRef)
        {
            if (Enabled == false || itemProtoRef == PrototypeId.Invalid || ItemPrototypes.Count == 0)
                return false;

            bool itemMatches = false;
            foreach (string itemName in ItemPrototypes)
            {
                if (GameDatabase.GetPrototypeRefByName(itemName) == itemProtoRef)
                {
                    itemMatches = true;
                    break;
                }
            }

            if (itemMatches == false)
                return false;

            if (Rarities.Count == 0)
                return true;

            foreach (string rarityName in Rarities)
            {
                if (GameDatabase.GetPrototypeRefByName(rarityName) == rarityProtoRef)
                    return true;
            }

            return false;
        }

        public bool IsAffixDisabled(AffixPrototype affixProto)
        {
            if (affixProto == null)
                return false;

            foreach (string affixName in DisabledAffixes)
            {
                PrototypeId disabledAffixRef = GameDatabase.GetPrototypeRefByName(affixName);
                if (disabledAffixRef != PrototypeId.Invalid && disabledAffixRef == affixProto.DataRef)
                    return true;
            }

            return false;
        }

        private static List<string> NormalizeStringList(List<string> values)
        {
            return values == null
                ? new()
                : values
                    .Where(value => string.IsNullOrWhiteSpace(value) == false)
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }
    }

    public sealed class OmegaTierForcedAffixTuning
    {
        public string Prototype { get; set; }
        public int Count { get; set; } = 1;
        public bool AllowInvalidAttachment { get; set; } = false;
        public bool ReplaceExistingSamePosition { get; set; } = false;

        public void Normalize()
        {
            Prototype = string.IsNullOrWhiteSpace(Prototype) ? string.Empty : Prototype.Trim();
            Count = Math.Max(Count, 0);
        }
    }

    public sealed class OmegaTierRandomAffixTuning
    {
        public string Position { get; set; }
        public int Count { get; set; } = 1;
        public bool AllowInvalidAttachment { get; set; } = false;

        public void Normalize()
        {
            Position = string.IsNullOrWhiteSpace(Position) ? string.Empty : Position.Trim();
            Count = Math.Max(Count, 0);
        }
    }
}
