using System.Linq;
using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.Diagnostics;
using MHServerEmu.Games.Network;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("damageprofile")]
    [CommandGroupDescription("Controls server-side power damage metrics logging.")]
    [CommandGroupUserLevel(AccountUserLevel.Admin)]
    public class DamageProfileCommands : CommandGroup
    {
        [DefaultCommand]
        [CommandDescription("Starts, stops, marks, or shows server-side power damage metrics logging.")]
        [CommandUsage("damageprofile [start|stop|status|mark] [label]")]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string DamageProfile(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            string playerName = playerConnection?.Player?.GetName() ?? "server";

            string action = @params.Length > 0 ? @params[0].ToLowerInvariant() : "status";
            string label = @params.Length > 1 ? string.Join("_", @params.Skip(1)) : playerName;

            switch (action)
            {
                case "start":
                case "on":
                    bool started = PowerDamageMetricsLogger.Start(label, playerName, out string startPath);
                    return started
                        ? $"Power damage profiling started: {startPath}"
                        : $"Power damage profiling is already running: {startPath}";

                case "stop":
                case "off":
                    bool stopped = PowerDamageMetricsLogger.Stop(playerName, out string stopPath, out long count);
                    return stopped
                        ? $"Power damage profiling stopped: {count} damage events written to {stopPath}"
                        : "Power damage profiling is not running.";

                case "mark":
                    if (PowerDamageMetricsLogger.Mark(label, playerName))
                        return $"Power damage profile marker added: {label}";

                    return "Power damage profiling is not running.";

                case "status":
                    return PowerDamageMetricsLogger.IsEnabled
                        ? $"Power damage profiling is running: session={PowerDamageMetricsLogger.SessionId}, events={PowerDamageMetricsLogger.DamageEventCount}, path={PowerDamageMetricsLogger.OutputPath}"
                        : "Power damage profiling is not running.";

                default:
                    return "Usage: damageprofile [start|stop|status|mark] [label]";
            }
        }
    }
}
