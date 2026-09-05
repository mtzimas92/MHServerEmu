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
                        Weight = 10,
                        Slots = ["Gear02", "Gear04"]
                    },
                    new()
                    {
                        Prototype = "Entity/Items/Affixes/ArmorAffixes/Loot20/CritDamage/CritDamageT3.prototype",
                        Weight = 10,
                        Slots = ["Gear02", "Gear04"]
                    },
                    new()
                    {
                        Prototype = "Entity/Items/Affixes/ArmorAffixes/Loot20/BrutalDamage/BrutalDamageT3.prototype",
                        Weight = 10,
                        Slots = ["Gear02", "Gear04"]
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
        public bool PreserveProcAffixes { get; set; } = false;
        public List<string> PreserveExistingAffixes { get; set; } = new();
        public List<string> DisabledAffixes { get; set; } = new();
        public List<OmegaTierForcedAffixTuning> ForcedAffixes { get; set; } = new();
        public List<OmegaTierAffixReplacementTuning> AffixReplacements { get; set; } = new();
        public List<OmegaTierRandomAffixTuning> RandomAffixes { get; set; } = new();
        public string OverrideRarity { get; set; }
        public int OverrideItemLevel { get; set; }
        public bool MaximizeAffixRolls { get; set; }
        public bool ClearBuiltInProperties { get; set; }
        public bool PreserveProcBuiltInProperties { get; set; } = true;
        public List<int> PreserveBuiltInPropertyIndexes { get; set; } = new();
        public bool MaximizeBuiltInPropertyRolls { get; set; }
        public bool ReplaceBuiltInPropertiesFromTemplate { get; set; }
        public string BuiltInPropertyTemplateItemPrototype { get; set; }
        public bool CopyTemplateProcProperties { get; set; }
        public bool DisableTriggeredItemActions { get; set; }
        public List<string> DisabledProcPowers { get; set; } = new();
        public List<OmegaTierBuiltInPropertyTuning> BuiltInProperties { get; set; } = new();
        public List<OmegaTierProcKeywordPropertyTuning> ProcKeywordProperties { get; set; } = new();

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? "unnamed-item-override" : Id.Trim();
            ItemPrototypes = NormalizeStringList(ItemPrototypes);
            Rarities = NormalizeStringList(Rarities);
            PreserveExistingAffixes = NormalizeStringList(PreserveExistingAffixes);
            DisabledAffixes = NormalizeStringList(DisabledAffixes);
            OverrideRarity = string.IsNullOrWhiteSpace(OverrideRarity) ? string.Empty : OverrideRarity.Trim();
            OverrideItemLevel = Math.Max(OverrideItemLevel, 0);
            BuiltInPropertyTemplateItemPrototype = string.IsNullOrWhiteSpace(BuiltInPropertyTemplateItemPrototype) ? string.Empty : BuiltInPropertyTemplateItemPrototype.Trim();
            DisabledProcPowers = NormalizeStringList(DisabledProcPowers);
            PreserveBuiltInPropertyIndexes ??= new();
            PreserveBuiltInPropertyIndexes = PreserveBuiltInPropertyIndexes
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            ForcedAffixes ??= new();
            AffixReplacements ??= new();
            RandomAffixes ??= new();
            BuiltInProperties ??= new();
            ProcKeywordProperties ??= new();

            foreach (OmegaTierForcedAffixTuning forcedAffix in ForcedAffixes)
                forcedAffix?.Normalize();

            foreach (OmegaTierAffixReplacementTuning affixReplacement in AffixReplacements)
                affixReplacement?.Normalize();

            foreach (OmegaTierRandomAffixTuning randomAffix in RandomAffixes)
                randomAffix?.Normalize();

            foreach (OmegaTierBuiltInPropertyTuning builtInProperty in BuiltInProperties)
                builtInProperty?.Normalize();

            foreach (OmegaTierProcKeywordPropertyTuning procKeywordProperty in ProcKeywordProperties)
                procKeywordProperty?.Normalize();
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

        public bool PreservesExistingAffix(PrototypeId affixRef)
        {
            if (affixRef == PrototypeId.Invalid || PreserveExistingAffixes.Count == 0)
                return false;

            foreach (string affixName in PreserveExistingAffixes)
            {
                PrototypeId preserveAffixRef = GameDatabase.GetPrototypeRefByName(affixName);
                if (preserveAffixRef != PrototypeId.Invalid && preserveAffixRef == affixRef)
                    return true;
            }

            return false;
        }

        public bool PreservesBuiltInPropertyIndex(int index)
        {
            return index >= 0 && PreserveBuiltInPropertyIndexes.Contains(index);
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

    public sealed class OmegaTierAffixReplacementTuning
    {
        public string Prototype { get; set; }
        public int Count { get; set; } = 1;
        public bool AllowInvalidAttachment { get; set; } = false;
        public bool PreserveProcAffixes { get; set; } = true;
        public bool PreserveConfiguredPreferredAffixes { get; set; } = true;
        public List<int> ReplaceAffixIndexes { get; set; } = new();
        public List<string> ReplaceAffixes { get; set; } = new();
        public List<string> ReplacePositions { get; set; } = new();
        public List<string> PreserveAffixes { get; set; } = new();
        public List<string> PreservePositions { get; set; } = new()
        {
            "Metadata",
            "Visual",
            "Cosmic",
            "Blessing",
            "Runeword",
            "TeamUp",
            "Socket1",
            "Socket2",
            "Socket3"
        };

        public void Normalize()
        {
            Prototype = string.IsNullOrWhiteSpace(Prototype) ? string.Empty : Prototype.Trim();
            Count = Math.Max(Count, 0);
            ReplaceAffixIndexes ??= new();
            ReplaceAffixIndexes = ReplaceAffixIndexes
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            ReplaceAffixes = NormalizeStringList(ReplaceAffixes);
            ReplacePositions = NormalizeStringList(ReplacePositions);
            PreserveAffixes = NormalizeStringList(PreserveAffixes);
            PreservePositions = NormalizeStringList(PreservePositions);
        }

        public bool AllowsPosition(AffixPosition position)
        {
            if (ReplacePositions.Count == 0)
                return true;

            foreach (string positionName in ReplacePositions)
            {
                if (Enum.TryParse(positionName, ignoreCase: true, out AffixPosition configuredPosition) &&
                    configuredPosition == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool PreservesPosition(AffixPosition position)
        {
            foreach (string positionName in PreservePositions)
            {
                if (Enum.TryParse(positionName, ignoreCase: true, out AffixPosition configuredPosition) &&
                    configuredPosition == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AllowsIndex(int index)
        {
            return ReplaceAffixIndexes.Count == 0 || ReplaceAffixIndexes.Contains(index);
        }

        public bool MatchesReplaceAffix(PrototypeId affixRef)
        {
            return MatchesConfiguredAffix(ReplaceAffixes, affixRef);
        }

        public bool PreservesAffix(PrototypeId affixRef)
        {
            return MatchesConfiguredAffix(PreserveAffixes, affixRef);
        }

        private static bool MatchesConfiguredAffix(List<string> affixNames, PrototypeId affixRef)
        {
            if (affixRef == PrototypeId.Invalid || affixNames == null || affixNames.Count == 0)
                return false;

            foreach (string affixName in affixNames)
            {
                if (GameDatabase.GetPrototypeRefByName(affixName) == affixRef)
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

    public sealed class OmegaTierBuiltInPropertyTuning
    {
        public JsonElement Property { get; set; }
        public float ValueMin { get; set; }
        public float ValueMax { get; set; }
        public bool RollAsInteger { get; set; }

        public void Normalize()
        {
            if (float.IsNaN(ValueMin) || float.IsInfinity(ValueMin))
                ValueMin = 0f;

            if (float.IsNaN(ValueMax) || float.IsInfinity(ValueMax))
                ValueMax = 0f;
        }
    }

    public sealed class OmegaTierProcKeywordPropertyTuning
    {
        public string Trigger { get; set; }
        public string Power { get; set; }
        public string Keyword { get; set; }
        public float? Chance { get; set; }
        public float ChanceMin { get; set; } = 0f;
        public float ChanceMax { get; set; } = 0f;
        public int? ItemLevelOverride { get; set; }
        public float? ItemVariationOverride { get; set; }
        public int? PowerRankOverride { get; set; }

        public void Normalize()
        {
            Trigger = string.IsNullOrWhiteSpace(Trigger) ? string.Empty : Trigger.Trim();
            Power = string.IsNullOrWhiteSpace(Power) ? string.Empty : Power.Trim();
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? string.Empty : Keyword.Trim();
            ChanceMin = Math.Max(ChanceMin, 0f);
            ChanceMax = Math.Max(ChanceMax, 0f);

            if (Chance.HasValue)
                Chance = Math.Max(Chance.Value, 0f);
        }

        public float GetChance(float randomMult, bool maximizeRoll)
        {
            if (Chance.HasValue)
                return Chance.Value;

            if (ChanceMax <= 0f)
                return ChanceMin;

            float min = Math.Min(ChanceMin, ChanceMax);
            float max = Math.Max(ChanceMin, ChanceMax);
            if (maximizeRoll || min == max)
                return max;

            return min + ((max - min) * randomMult);
        }
    }
}
