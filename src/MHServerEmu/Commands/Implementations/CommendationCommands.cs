using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("commendations")]
    [CommandGroupDescription("Shows weekly commendation drop progress.")]
    [CommandGroupFlags(CommandGroupFlags.SingleCommand)]
    public class CommendationCommands : CommandGroup
    {
        private static readonly CommendationChannel[] Channels =
        [
            new("Hero Commendations", "Loot/Cooldowns/Channels/EyeOfDemonfireChannelCount.prototype"),
            new("Protector Commendations", "Loot/Cooldowns/Channels/HeartOfDemonfireChannelCount.prototype")
        ];

        [DefaultCommand]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Commendations(string[] @params, NetClient client)
        {
            Player player = ((PlayerConnection)client).Player;
            if (player == null)
                return "Player not found.";

            List<string> lines = new() { "Weekly commendation drops:" };

            foreach (CommendationChannel channel in Channels)
            {
                PrototypeId channelRef = GameDatabase.GetPrototypeRefByName(channel.PrototypeName);
                LootCooldownChannelCountPrototype channelProto = GameDatabase.GetPrototype<LootCooldownChannelCountPrototype>(channelRef);
                if (channelProto == null)
                {
                    lines.Add($"{channel.DisplayName}: unavailable");
                    continue;
                }

                channelProto.UpdateCooldown(player, PrototypeId.Invalid);

                int count = player.Properties[PropertyEnum.LootCooldownCount, channelRef];
                int remaining = Math.Max(channelProto.MaxDrops - count, 0);
                lines.Add($"{channel.DisplayName}: {count}/{channelProto.MaxDrops} ({remaining} remaining)");
            }

            if (player.Game?.MythicRiftManager is { } riftManager &&
                riftManager.TryGetWeeklyChampionCommendationProgress(player, out int championCount, out int championCap))
            {
                int remaining = Math.Max(championCap - championCount, 0);
                lines.Add($"Champion Commendations: {championCount}/{championCap} ({remaining} remaining)");
            }
            else
            {
                lines.Add("Champion Commendations: unavailable");
            }

            return string.Join('\n', lines);
        }

        private readonly record struct CommendationChannel(string DisplayName, string PrototypeName);
    }
}
