using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Inventories;
using MHServerEmu.Games.Entities.Items;
using MHServerEmu.Games.Entities.Options;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("stash")]
    public class StashCommands : CommandGroup
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private readonly Dictionary<string, List<PrototypeId>> _categoryStashMap = new();

        [Command("internal")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Internal(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            if (playerConnection?.Player == null) return "Invalid player connection";

            return CompactInventory(playerConnection.Player);
        }

        private string CompactInventory(Player player)
        {
            Inventory generalInventory = player.GetInventory(InventoryConvenienceLabel.General);
            if (generalInventory == null) return "No general inventory found";

            var entityManager = player.Game.EntityManager;
            List<Item> itemsToSort = ListPool<Item>.Instance.Get();
            int itemsCompacted = 0;

            foreach (var entry in generalInventory)
            {
                Item item = entityManager.GetEntity<Item>(entry.Id);
                if (item != null && !item.IsEquipped)
                {
                    itemsToSort.Add(item);
                }
            }

            itemsToSort.Sort((a, b) =>
            {
                int categoryComparison = string.Compare(GetItemCategory(a), GetItemCategory(b), StringComparison.Ordinal);
                if (categoryComparison != 0) return categoryComparison;
                return string.Compare(GameDatabase.GetPrototypeName(a.PrototypeDataRef), GameDatabase.GetPrototypeName(b.PrototypeDataRef), StringComparison.Ordinal);
            });

            uint nextSlot = 0;
            foreach (Item item in itemsToSort)
            {
                bool needsBindingSkip = item.IsBoundToCharacter;

                if (needsBindingSkip)
                    item.SetStatus(EntityStatus.SkipItemBindingCheck, true);

                try
                {
                    // We directly move to the next available slot, not caring about the item's original position
                    if (item.InventoryLocation.Slot != nextSlot)
                    {
                        if (player.TryInventoryMove(item.Id, generalInventory.OwnerId, generalInventory.PrototypeDataRef, nextSlot))
                        {
                            itemsCompacted++;
                        }
                    }
                    nextSlot++;
                }
                finally
                {
                    if (needsBindingSkip)
                        item.SetStatus(EntityStatus.SkipItemBindingCheck, false);
                }
            }

            ListPool<Item>.Instance.Return(itemsToSort);
            return $"Compacted {itemsCompacted} items in your inventory.";
        }
        private string GetItemCategory(Item item)
        {
            string itemProto = GameDatabase.GetPrototypeName(item.PrototypeDataRef);
            if (itemProto.StartsWith("Entity/Items/"))
            {
                string[] parts = itemProto.Split('/');
                if (parts.Length >= 3)
                {
                    if (parts[2] == "Armor")
                        return itemProto.Contains("Unique") ? "Uniques" : "Gear";

                    switch (parts[2])
                    {
                        case "Crafting":
                        case "CurrencyItems":
                        case "Artifacts":
                        case "Insignias":
                        case "Legendaries":
                        case "Medals":
                        case "Pets":
                        case "Relics":
                        case "Rings":
                        case "Runewords":
                        case "DRScenario":
                            return parts[2];
                    }
                }
            }
            return "Other";
        }
    }
}
                   
