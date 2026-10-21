using System.Globalization;
using Gazillion;
using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.MythicRifts;
using MHServerEmu.Games.Network;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("rift")]
    [CommandGroupDescription("Player and configuration commands for Mythic Rifts.")]
    public class MythicRiftCommands : CommandGroup
    {
        [Command("status")]
        [CommandDescription("Displays the invoking player's active Rift run.")]
        [CommandUsage("rift status")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Status(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            MythicRiftRunState runState = game.MythicRiftManager.GetInProgressRunForPlayer(player.DatabaseUniqueId);
            if (runState == null)
            {
                int cosmicUnlockedLevel = game.MythicRiftManager.GetHighestUnlockedRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Standard);
                int cosmicSelectedLevel = game.MythicRiftManager.GetPreferredLaunchRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Standard);
                int endlessUnlockedLevel = game.MythicRiftManager.GetHighestUnlockedRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Endless);
                int endlessSelectedLevel = game.MythicRiftManager.GetPreferredLaunchRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Endless);
                int endlessCycles = game.MythicRiftManager.GetCompletedEndlessCycles(player.DatabaseUniqueId);
                return $"No active Rift run. Infinite Rift: heroHighest={cosmicUnlockedLevel}, next={cosmicSelectedLevel}, accountBest={player.MythicRiftHighestUnlockedLevel}. Omega Training: highest={endlessUnlockedLevel}, next={endlessSelectedLevel}, completedCycles={endlessCycles}. Boss Gauntlet has no persisted progression.";
            }

            TimeSpan remaining = runState.GetTimeRemaining(game.CurrentTime);
            return $"Active Rift: runId={runState.Config.RunId} | mode={MythicRiftManager.GetModeDisplayName(runState.Config.Mode)} | status={runState.Status} | map={runState.Config.Content.DisplayName} | level={runState.Config.RiftLevel} | progress={Math.Min(runState.CurrentKillCount, runState.Config.KillQuota)}/{runState.Config.KillQuota} | timeRemaining={Math.Max(0, remaining.TotalSeconds):0}s";
        }

        [Command("ready")]
        [CommandDescription("Marks the invoking player ready, or waiting, for the active Rift ready check.")]
        [CommandUsage("rift ready [ready|wait]")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Ready(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            bool ready = true;
            if (@params.Length > 0)
            {
                string action = @params[0];
                if (string.Equals(action, "wait", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action, "pending", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action, "notready", StringComparison.OrdinalIgnoreCase))
                    ready = false;
                else if (string.Equals(action, "ready", StringComparison.OrdinalIgnoreCase) == false)
                    return "Unknown ready action. Use `rift ready` or `rift ready wait`.";
            }

            return game.MythicRiftManager.TrySetReadyCheckForPlayer(player.DatabaseUniqueId, ready, out string message)
                ? message
                : message ?? "Failed to update ready check.";
        }

        [Command("level")]
        [CommandDescription("Displays or changes the Infinite Rift or Omega Training level used by the next launch.")]
        [CommandUsage("rift level [cosmic|gauntlet|bossgauntlet] [level|max]")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Level(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            if (TryParseOptionalModeArguments(@params, out MythicRiftMode mode, out string[] valueArgs, out string modeError) == false)
                return modeError;

            int unlockedLevel = game.MythicRiftManager.GetHighestUnlockedRiftLevel(player.DatabaseUniqueId, mode);
            int selectedLevel = game.MythicRiftManager.GetPreferredLaunchRiftLevel(player.DatabaseUniqueId, mode);
            string modeName = MythicRiftManager.GetModeDisplayName(mode);
            string modeCommandToken = FormatModeCommandToken(mode);

            if (valueArgs.Length == 0)
                return $"Next {modeName} launch level: {selectedLevel}. Highest unlocked: {unlockedLevel}. Use `rift level {modeCommandToken} [1-{unlockedLevel}]` to arm one lower farming run, or `rift level {modeCommandToken} max` to use your highest unlocked level.";

            if (valueArgs.Length != 1)
                return "Usage: rift level [cosmic|gauntlet|bossgauntlet] [level|max]";

            string requestedLevelText = valueArgs[0];
            if (string.Equals(requestedLevelText, "max", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedLevelText, "highest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedLevelText, "auto", StringComparison.OrdinalIgnoreCase))
            {
                int appliedLevel = game.MythicRiftManager.UseHighestUnlockedLaunchRiftLevel(player.DatabaseUniqueId, mode);
                return $"{modeName} one-shot launch selection cleared. The next matching launch will use level {appliedLevel}.";
            }

            if (TryParsePositiveInt(requestedLevelText, out int requestedLevel) == false)
                return "Invalid Rift level. Use a positive number or `max`.";

            if (game.MythicRiftManager.TrySetPreferredLaunchRiftLevel(player.DatabaseUniqueId, requestedLevel, out int appliedLaunchLevel, out string errorMessage, mode) == false)
                return errorMessage;

            return $"Next {modeName} launch set to level {appliedLaunchLevel}. Highest unlocked: {unlockedLevel}. This one-shot selection is consumed after the next successful matching launch.";
        }

        [Command("abandon")]
        [CommandDescription("Abandons the invoking player's active Rift and returns online participants to the Danger Room hub.")]
        [CommandUsage("rift abandon")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Abandon(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            MythicRiftRunState runState = game.MythicRiftManager.GetInProgressRunForPlayer(player.DatabaseUniqueId);
            if (runState == null)
                return "No active Rift run to abandon.";

            ulong runId = runState.Config.RunId;
            if (game.MythicRiftManager.MarkRunAborted(runId, game.CurrentTime) == false)
                return $"Failed to abandon Rift run {runId}.";

            int returnedPlayers = game.MythicRiftManager.ReturnRunParticipantsToDangerRoomHub(runState, player);
            game.MythicRiftManager.RemoveRun(runId);
            return $"Rift abandoned: runId={runId} | map={runState.Config.Content.DisplayName} | level={runState.Config.RiftLevel} | returnedPlayers={returnedPlayers}.";
        }

        [Command("recover")]
        [CommandDescription("Clears temporary Rift launch state and safely abandons a stuck active run.")]
        [CommandUsage("rift recover")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Recover(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            bool disarmed = game.MythicRiftLauncherService.DisarmChosenLauncher(player.DatabaseUniqueId);
            int nextCosmicLevel = game.MythicRiftManager.UseHighestUnlockedLaunchRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Standard);
            int nextTrainingLevel = game.MythicRiftManager.UseHighestUnlockedLaunchRiftLevel(player.DatabaseUniqueId, MythicRiftMode.Endless);
            MythicRiftRunState runState = game.MythicRiftManager.GetInProgressRunForPlayer(player.DatabaseUniqueId);
            if (runState == null)
                return $"Rift recovery complete: no active run found. clearedBeaconOverride={disarmed} | nextInfiniteRiftLevel={nextCosmicLevel} | nextOmegaTrainingLevel={nextTrainingLevel}.";

            ulong runId = runState.Config.RunId;
            if (game.MythicRiftManager.MarkRunAborted(runId, game.CurrentTime) == false)
                return $"Rift recovery partially complete, but failed to abort run {runId}.";

            int returnedPlayers = game.MythicRiftManager.ReturnRunParticipantsToDangerRoomHub(runState, player);
            game.MythicRiftManager.RemoveRun(runId);
            return $"Rift recovery complete: abandoned runId={runId} | returnedPlayers={returnedPlayers} | clearedBeaconOverride={disarmed}.";
        }

        [Command("contentpool")]
        [CommandDescription("Displays or reloads the server-side Mythic Rift content pool file.")]
        [CommandUsage("rift contentpool [reload]")]
        [CommandUserLevel(AccountUserLevel.Admin)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string ContentPool(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            if (game == null)
                return "Game not found.";

            if (@params.Length > 0)
            {
                if (string.Equals(@params[0], "reload", StringComparison.OrdinalIgnoreCase))
                {
                    bool loaded = game.MythicRiftManager.TryReloadContentPool(out string reloadMessage);
                    return loaded ? $"Content pool reload OK: {reloadMessage}" : $"Content pool reload FAILED: {reloadMessage}";
                }
                return "Unknown contentpool action. Use `rift contentpool` or `rift contentpool reload`.";
            }

            return $"{game.MythicRiftManager.ContentPoolLastLoadMessage} entries={game.MythicRiftManager.ContentPool.Count}.";
        }

        [Command("rewardconfig")]
        [CommandDescription("Displays or reloads the server-side Rift reward tuning file.")]
        [CommandUsage("rift rewardconfig [reload]")]
        [CommandUserLevel(AccountUserLevel.Admin)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string RewardConfig(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            if (game == null)
                return "Game not found.";

            if (@params.Length > 0)
            {
                if (string.Equals(@params[0], "reload", StringComparison.OrdinalIgnoreCase))
                {
                    bool loaded = game.MythicRiftManager.TryReloadRewardTuning(out string reloadMessage);
                    List<string> lines = game.MythicRiftManager.BuildRewardTuningDiagnostics();
                    lines.Insert(0, loaded ? $"Reward tuning reload OK: {reloadMessage}" : $"Reward tuning reload FAILED: {reloadMessage}");
                    CommandHelper.SendMessages(client, lines);
                    return string.Empty;
                }
                return "Unknown rewardconfig action. Use `rift rewardconfig` or `rift rewardconfig reload`.";
            }

            CommandHelper.SendMessages(client, game.MythicRiftManager.BuildRewardTuningDiagnostics());
            return string.Empty;
        }

        [Command("affixconfig")]
        [CommandDescription("Displays or reloads the server-side Rift affix tuning file.")]
        [CommandUsage("rift affixconfig [reload]")]
        [CommandUserLevel(AccountUserLevel.Admin)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string AffixConfig(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            if (game == null)
                return "Game not found.";

            if (@params.Length > 0)
            {
                if (string.Equals(@params[0], "reload", StringComparison.OrdinalIgnoreCase))
                {
                    bool loaded = game.MythicRiftManager.TryReloadAffixTuning(out string reloadMessage);
                    List<string> lines = game.MythicRiftManager.BuildAffixTuningDiagnostics();
                    lines.Insert(0, loaded ? $"Affix tuning reload OK: {reloadMessage}" : $"Affix tuning reload FAILED: {reloadMessage}");
                    CommandHelper.SendMessages(client, lines);
                    return string.Empty;
                }
                return "Unknown affixconfig action. Use `rift affixconfig` or `rift affixconfig reload`.";
            }

            CommandHelper.SendMessages(client, game.MythicRiftManager.BuildAffixTuningDiagnostics());
            return string.Empty;
        }

        [Command("hazardconfig")]
        [CommandDescription("Displays or reloads the server-side Rift hazard tuning file.")]
        [CommandUsage("rift hazardconfig [reload]")]
        [CommandUserLevel(AccountUserLevel.Admin)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string HazardConfig(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            if (game == null)
                return "Game not found.";

            if (@params.Length > 0)
            {
                if (string.Equals(@params[0], "reload", StringComparison.OrdinalIgnoreCase))
                {
                    bool loaded = game.MythicRiftManager.TryReloadHazardTuning(out string reloadMessage);
                    List<string> lines = game.MythicRiftManager.BuildHazardTuningDiagnostics();
                    lines.Insert(0, loaded ? $"Hazard tuning reload OK: {reloadMessage}" : $"Hazard tuning reload FAILED: {reloadMessage}");
                    CommandHelper.SendMessages(client, lines);
                    return string.Empty;
                }
                return "Unknown hazardconfig action. Use `rift hazardconfig` or `rift hazardconfig reload`.";
            }

            CommandHelper.SendMessages(client, game.MythicRiftManager.BuildHazardTuningDiagnostics());
            return string.Empty;
        }

        [Command("modifiers")]
        [CommandDescription("Displays the active modifier identity for the invoking player's Rift run.")]
        [CommandUsage("rift modifiers [runId]")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Modifiers(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Game game = playerConnection?.Game;
            Player player = playerConnection?.Player;
            if (game == null || player == null)
                return "Game or player not found.";

            MythicRiftRunState runState;
            if (@params.Length > 0)
            {
                if (ulong.TryParse(@params[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong runId) == false || runId == 0)
                    return "Invalid run id.";
                runState = game.MythicRiftManager.GetRun(runId);
            }
            else
            {
                runState = game.MythicRiftManager.GetInProgressRunForPlayer(player.DatabaseUniqueId);
            }

            if (runState == null)
                return "No active Rift run found.";

            CommandHelper.SendMessages(client, game.MythicRiftManager.BuildModifierDiagnostics(runState.Config.RunId));
            return string.Empty;
        }

        private static bool TryParseOptionalModeArguments(string[] args, out MythicRiftMode mode, out string[] valueArgs, out string errorMessage)
        {
            mode = MythicRiftMode.Standard;
            valueArgs = args ?? Array.Empty<string>();
            errorMessage = string.Empty;
            if (args == null || args.Length == 0)
                return true;

            bool firstIsMode = TryParseRiftModeToken(args[0], out MythicRiftMode firstMode);
            MythicRiftMode lastMode = MythicRiftMode.Standard;
            bool lastIsMode = args.Length > 1 && TryParseRiftModeToken(args[^1], out lastMode);
            if (firstIsMode && lastIsMode && firstMode != lastMode)
            {
                errorMessage = "Conflicting Rift modes. Use `cosmic`, `gauntlet`, or `bossgauntlet` once.";
                return false;
            }

            if (firstIsMode)
            {
                mode = firstMode;
                valueArgs = args.Skip(1).ToArray();
            }
            else if (lastIsMode)
            {
                mode = lastMode;
                valueArgs = args.Take(args.Length - 1).ToArray();
            }

            return true;
        }

        private static bool TryParseRiftModeToken(string value, out MythicRiftMode mode)
        {
            mode = MythicRiftMode.Standard;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "standard": case "cosmic": case "infinite": case "infiniterift": case "infinite-rift": case "mythic": case "rift":
                    mode = MythicRiftMode.Standard;
                    return true;
                case "endless": case "gauntlet": case "ot": case "omegatraining": case "omega-training": case "training": case "thirtywave": case "30wave": case "30-wave":
                    mode = MythicRiftMode.Endless;
                    return true;
                case "boss": case "bossgauntlet": case "boss-gauntlet":
                    mode = MythicRiftMode.BossGauntlet;
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatModeCommandToken(MythicRiftMode mode)
        {
            return mode switch
            {
                MythicRiftMode.Endless => "gauntlet",
                MythicRiftMode.BossGauntlet => "bossgauntlet",
                _ => "cosmic"
            };
        }

        private static bool TryParsePositiveInt(string value, out int parsedValue)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue) && parsedValue > 0;
        }
    }
}
