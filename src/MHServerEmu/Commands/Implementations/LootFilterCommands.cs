using System.Text;
using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.OmegaTierItems;
using MHServerEmu.Games.Entities;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("filter")]
    [CommandGroupDescription("Manage personal server-side loot filters.")]
    public class LootFilterCommands : CommandGroup
    {
        [Command("omega")]
        [CommandDescription("Filters Omega armor by gear slot.")]
        [CommandUsage("filter omega <gear01|gear02|gear03|gear04|gear05|all> <on|off> [global|me|character]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        [CommandParamCount(2)]
        public string Omega(string[] @params, NetClient client)
        {
            string slot = @params[0].ToLowerInvariant();
            bool? enabled = ParseBoolean(@params[1]);
            if ((slot != "all" && OmegaGearLootFilter.OmegaSlotKeys.Contains(slot) == false) || enabled == null)
                return "Usage: !filter omega <gear01|gear02|gear03|gear04|gear05|all> <on|off>";

            Player player = ((PlayerConnection)client).Player;
            ulong playerDbId = player.DatabaseUniqueId;
            OmegaLootFilterSettings settings = OmegaGearLootFilter.Get(playerDbId);
            OmegaLootFilterSection section = ResolveSection(player, settings, @params.Length > 2 ? @params[2] : null, create: true, out string scope);
            IEnumerable<string> slots = slot == "all" ? OmegaGearLootFilter.OmegaSlotKeys : [slot];
            foreach (string slotKey in slots)
            {
                if (enabled.Value) section.OmegaGearSlots.Add(slotKey);
                else section.OmegaGearSlots.Remove(slotKey);
            }
            return $"Omega filter [{scope}] {slot}: {(enabled.Value ? "ON" : "OFF")}.";
        }

        [Command("set")]
        [CommandDescription("Filters an item type at or below a rarity.")]
        [CommandUsage("filter set <ring|medal|insignia|teamup|catalyst> <rarity> [global|me|character]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        [CommandParamCount(2)]
        public string Set(string[] @params, NetClient client)
        {
            string key = @params[0].ToLowerInvariant();
            if (OmegaGearLootFilter.ThresholdKeys.Contains(key) == false)
                return "Valid types: ring, medal, insignia, teamup, catalyst.";
            PrototypeId rarityRef = OmegaGearLootFilter.ResolveRarity(@params[1]);
            if (rarityRef == PrototypeId.Invalid)
                return "Unknown rarity. Use !filter rarities.";

            Player player = ((PlayerConnection)client).Player;
            ulong playerDbId = player.DatabaseUniqueId;
            OmegaLootFilterSection section = ResolveSection(player, OmegaGearLootFilter.Get(playerDbId), @params.Length > 2 ? @params[2] : null, create: true, out string scope);
            section.RarityThresholds[key] = @params[1];
            return $"Filter [{scope}]: {key} items at or below {@params[1]} will be filtered.";
        }

        [Command("uruforged")]
        [CommandDescription("Toggles filtering of Uru-Forged items.")]
        [CommandUsage("filter uruforged <on|off> [global|me|character]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        [CommandParamCount(1)]
        public string UruForged(string[] @params, NetClient client)
        {
            bool? enabled = ParseBoolean(@params[0]);
            if (enabled == null) return "Usage: !filter uruforged <on|off>";
            Player player = ((PlayerConnection)client).Player;
            ulong playerDbId = player.DatabaseUniqueId;
            OmegaLootFilterSection section = ResolveSection(player, OmegaGearLootFilter.Get(playerDbId), @params.Length > 1 ? @params[1] : null, create: true, out string scope);
            section.FilterUruForged = enabled.Value;
            return $"Uru-Forged filter [{scope}]: {(enabled.Value ? "ON" : "OFF")}.";
        }

        [Command("clear")]
        [CommandDescription("Clears a rarity filter.")]
        [CommandUsage("filter clear <omega|ring|medal|insignia|teamup|catalyst|uruforged> [global|me|character]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        [CommandParamCount(1)]
        public string Clear(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            ulong playerDbId = player.DatabaseUniqueId;
            OmegaLootFilterSection section = ResolveSection(player, OmegaGearLootFilter.Get(playerDbId), @params.Length > 1 ? @params[1] : null, create: false, out string scope);
            if (section == null)
                return $"No settings exist for {scope}.";
            string key = @params[0].ToLowerInvariant();
            bool removed = key switch
            {
                "omega" => section.OmegaGearSlots.Count > 0 && ClearOmega(section),
                "uruforged" => ClearUru(section),
                _ => section.RarityThresholds.Remove(key)
            };
            return removed ? $"Filter cleared [{scope}]." : "No matching filter was set.";
        }

        [Command("clearall")]
        [CommandDescription("Clears every global and character-specific loot filter.")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string ClearAll(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            OmegaLootFilterSettings settings = OmegaGearLootFilter.Get(player.DatabaseUniqueId);
            settings.Global = new();
            settings.Characters.Clear();
            return "All loot filters cleared.";
        }

        [Command("list")]
        [CommandDescription("Lists active loot filters.")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string List(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            OmegaLootFilterSettings settings = OmegaGearLootFilter.Get(player.DatabaseUniqueId);
            OmegaLootFilterSection character = settings.GetCharacter(player.CurrentAvatar?.PrototypeDataRef.GetNameFormatted(), create: false);
            StringBuilder result = new("Loot filters:\n");
            AppendSection(result, "global", settings.Global);
            if (character != null)
                AppendSection(result, player.CurrentAvatar.PrototypeDataRef.GetNameFormatted(), character);
            return result.ToString();
        }

        [Command("rarities")]
        [CommandDescription("Lists valid rarity names.")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Rarities(string[] @params, NetClient client) => string.Join(", ", OmegaGearLootFilter.GetRarityNames());

        private static bool? ParseBoolean(string value) => value?.ToLowerInvariant() switch
        {
            "on" or "true" or "yes" => true,
            "off" or "false" or "no" => false,
            _ => null
        };

        private static OmegaLootFilterSection ResolveSection(Player player, OmegaLootFilterSettings settings, string target, bool create, out string scope)
        {
            if (string.IsNullOrWhiteSpace(target) || target.Equals("global", StringComparison.OrdinalIgnoreCase))
            {
                scope = "global";
                return settings.Global;
            }
            scope = target.Equals("me", StringComparison.OrdinalIgnoreCase)
                ? player.CurrentAvatar?.PrototypeDataRef.GetNameFormatted()
                : target;
            return settings.GetCharacter(scope, create);
        }

        private static bool ClearOmega(OmegaLootFilterSection section)
        {
            section.OmegaGearSlots.Clear();
            return true;
        }

        private static bool ClearUru(OmegaLootFilterSection section)
        {
            bool wasSet = section.FilterUruForged;
            section.FilterUruForged = false;
            return wasSet;
        }

        private static void AppendSection(StringBuilder result, string name, OmegaLootFilterSection section)
        {
            result.AppendLine($"  [{name}] Omega slots: {(section.OmegaGearSlots.Count == 0 ? "none" : string.Join(", ", section.OmegaGearSlots.OrderBy(value => value)))}");
            foreach ((string key, string rarity) in section.RarityThresholds.OrderBy(entry => entry.Key))
                result.AppendLine($"  [{name}] {key}: <= {rarity}");
            result.AppendLine($"  [{name}] Uru-Forged: {(section.FilterUruForged ? "ON" : "OFF")}");
        }
    }
}
