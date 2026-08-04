using Gazillion;
using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.VillainIntelBoard;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("intel")]
    [CommandGroupDescription("Villain Intel Board commands.")]
    public class VillainIntelCommands : CommandGroup
    {
        [Command("status")]
        [CommandDescription("Shows your current Villain Intel Board.")]
        [CommandUsage("intel status")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Status(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            CommandHelper.SendMessages(client, VillainIntelBoardManager.BuildStatusLines(player));
            return string.Empty;
        }

        [Command("start")]
        [CommandDescription("Starts a Villain Intel bounty by slot number.")]
        [CommandUsage("intel start [1-6]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Start(string[] @params, NetClient client)
        {
            if (@params.Length < 1 || int.TryParse(@params[0], out int slot) == false)
                return "Usage: intel start [1-6]";

            Player player = ((PlayerConnection)client).Player;
            return VillainIntelBoardManager.AcceptBounty(player, slot - 1);
        }

        [Command("collect")]
        [CommandDescription("Collects a completed Villain Intel bounty reward by slot number.")]
        [CommandUsage("intel collect [1-6]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Collect(string[] @params, NetClient client)
        {
            if (@params.Length < 1 || int.TryParse(@params[0], out int slot) == false)
                return "Usage: intel collect [1-6]";

            Player player = ((PlayerConnection)client).Player;
            return VillainIntelBoardManager.CollectReward(player, slot - 1);
        }

        [Command("reroll")]
        [CommandDescription("Rerolls your Villain Intel Board.")]
        [CommandUsage("intel reroll")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Reroll(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            return VillainIntelBoardManager.ForceReroll(player, spendCredits: true);
        }

        [Command("reload")]
        [CommandDescription("Reloads Villain Intel Board tuning JSON.")]
        [CommandUsage("intel reload")]
        [CommandUserLevel(AccountUserLevel.Admin)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Reload(string[] @params, NetClient client)
        {
            VillainIntelBoardManager.TryReloadTuning(out string message);
            return message;
        }
    }
}
