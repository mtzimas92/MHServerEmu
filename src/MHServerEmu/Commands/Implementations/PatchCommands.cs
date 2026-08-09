using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.GameData.PatchManager;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("patch")]
    [CommandGroupDescription("Inspects prototype patch application status.")]
    [CommandGroupUserLevel(AccountUserLevel.Admin)]
    public class PatchCommands : CommandGroup
    {
        [DefaultCommand]
        [CommandDescription("Shows whether loaded prototype patches actually applied.")]
        [CommandUsage("patch [status|verify] [filter]")]
        public string Patch(string[] @params, NetClient client)
        {
            string action = @params.Length > 0 ? @params[0].ToLowerInvariant() : "status";
            if (action != "status" && action != "verify")
                return "Usage: patch [status|verify] [filter]";

            string filter = @params.Length > 1 ? string.Join(' ', @params.Skip(1)) : string.Empty;
            string report = PrototypePatchManager.Instance.BuildPatchStatusReport(filter, forceLoad: true);

            if (client != null)
            {
                CommandHelper.SendMessageSplit(client, report, false);
                return string.Empty;
            }

            return report;
        }
    }
}
