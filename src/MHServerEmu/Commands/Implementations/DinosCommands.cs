using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Network;
using MHServerEmu.Core.System.Time;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("dinos")]
    [CommandGroupDescription("Commands for Dinos Invade Manhattan event diagnostics.")]
    public class DinosCommands : CommandGroup
    {
        private static readonly PrototypeId DinosSharedBossCooldownOriginRef = (PrototypeId)9671380227843956965;  // KingLizardBossEG02.prototype
        private static readonly TimeSpan DinosFinalBossCooldownFallback = TimeSpan.FromHours(20);

        [Command("cooldown")]
        [CommandDescription("Shows when the Dinos final-boss once-daily loot cooldown expires.")]
        [CommandUsage("dinos cooldown")]
        [CommandUserLevel(AccountUserLevel.User)]
        [CommandInvokerType(CommandInvokerType.Client)]
        public string Cooldown(string[] @params, NetClient client)
        {
            PlayerConnection playerConnection = (PlayerConnection)client;
            Player player = playerConnection?.Player;
            if (player?.Game == null)
                return "Game or player not found.";

            TimeSpan cooldownDuration = GetDinosFinalBossCooldownDuration();
            TimeSpan currentGameTime = player.Game.CurrentTime;
            List<string> lines = new()
            {
                "Dinos final-boss once-daily loot cooldown:",
                $"source={DinosSharedBossCooldownOriginRef.GetNameFormatted()} | duration={FormatDuration(cooldownDuration)}"
            };

            bool foundAccountCooldown = AppendCooldownLines(
                lines,
                "account",
                player.Properties,
                currentGameTime,
                cooldownDuration);

            bool foundAvatarCooldown = player.CurrentAvatar != null && AppendCooldownLines(
                lines,
                "avatar",
                player.CurrentAvatar.Properties,
                currentGameTime,
                cooldownDuration);

            if (foundAccountCooldown == false && foundAvatarCooldown == false)
                lines.Add("ready=true | no saved cooldown found for this account/avatar.");

            CommandHelper.SendMessages(client, lines);
            return string.Empty;
        }

        private static TimeSpan GetDinosFinalBossCooldownDuration()
        {
            WorldEntityPrototype bossProto = DinosSharedBossCooldownOriginRef.As<WorldEntityPrototype>();
            if (bossProto?.Properties == null)
                return DinosFinalBossCooldownFallback;

            int cooldownHours = bossProto.Properties[PropertyEnum.LootCooldownTimeHours];
            return cooldownHours > 0
                ? TimeSpan.FromHours(cooldownHours)
                : DinosFinalBossCooldownFallback;
        }

        private static bool AppendCooldownLines(
            List<string> lines,
            string scope,
            PropertyCollection properties,
            TimeSpan currentGameTime,
            TimeSpan cooldownDuration)
        {
            if (properties == null)
                return false;

            bool foundAny = false;
            foreach (var kvp in properties.IteratePropertyRange(PropertyEnum.LootCooldownTimeStartEntity, DinosSharedBossCooldownOriginRef))
            {
                foundAny = true;
                TimeSpan cooldownStart = kvp.Value;
                TimeSpan cooldownEnd = cooldownStart + cooldownDuration;
                TimeSpan remaining = cooldownEnd - currentGameTime;
                bool active = remaining > TimeSpan.Zero;

                Property.FromParam(kvp.Key, 1, out PrototypeId difficultyRef);

                string expireText = active
                    ? Clock.GameTimeToDateTime(cooldownEnd).ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                    : "expired";

                lines.Add(
                    $"scope={scope} | difficulty={difficultyRef.GetNameFormatted()} | ready={!active} | remaining={FormatDuration(remaining)} | expires={expireText}");
            }

            return foundAny;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                return "0s";

            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";

            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";

            if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";

            return $"{duration.Seconds}s";
        }
    }
}
