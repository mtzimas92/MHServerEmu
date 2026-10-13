using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Network;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("riftrewards")]
    [CommandGroupDescription("Administrative commands for testing Mythic Rift rewards.")]
    [CommandGroupUserLevel(AccountUserLevel.Admin)]
    public class MythicRiftRewardCommands : CommandGroup
    {
        [Command("reset")]
        [CommandDescription("Resets current-period Mythic Rift reward claims without changing Rift progression.")]
        [CommandUsage("riftrewards reset [all|account|hero]")]
        public string Reset(string[] @params, NetClient client)
        {
            if (client is not PlayerConnection connection || connection.Player is not Player player)
                return "This command must be used by an in-game player.";

            string scope = @params.Length > 0 ? @params[0].Trim().ToLowerInvariant() : "all";
            bool resetAccount = scope is "all" or "account";
            bool resetHero = scope is "all" or "hero";
            if (resetAccount == false && resetHero == false)
                return "Invalid scope. Use: riftrewards reset [all|account|hero]";

            int removed = player.Game.MythicRiftManager.ResetRewardClaimsForTesting(player, resetAccount, resetHero);
            return $"Reset {removed} Mythic Rift reward claim records for scope '{scope}'. Rift progression was not changed.";
        }

        [Command("resetcommendations")]
        [CommandDescription("Resets Hero, Protector, and Champion weekly commendation cap progress.")]
        [CommandUsage("riftrewards resetcommendations")]
        public string ResetCommendations(string[] @params, NetClient client)
        {
            if (client is not PlayerConnection connection || connection.Player is not Player player)
                return "This command must be used by an in-game player.";

            IReadOnlyList<string> results = player.Game.MythicRiftManager.ResetWeeklyCommendationCapsForTesting(player);
            if (results.Count == 0)
                return "No weekly commendation cap progress was reset.";

            return $"Weekly commendation cap progress reset:\n{string.Join('\n', results)}";
        }
    }
}
