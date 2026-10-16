using System.Collections.Concurrent;
using MHServerEmu.Games.Entities;

namespace MHServerEmu.Games.MythicRifts
{
    internal static class MythicRiftPartyTeleportQueue
    {
        private static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan LeaderTransferHeadStart = TimeSpan.FromSeconds(1);
        private static readonly ConcurrentDictionary<ulong, PendingTeleport> PendingTeleports = new();

        public static void Enqueue(ulong playerDbId, ulong targetPlayerDbId)
        {
            if (playerDbId == 0 || targetPlayerDbId == 0 || playerDbId == targetPlayerDbId)
                return;

            DateTime utcNow = DateTime.UtcNow;
            PendingTeleports[playerDbId] = new(
                targetPlayerDbId,
                utcNow + LeaderTransferHeadStart,
                utcNow + RequestLifetime);
        }

        public static void Process(Game game)
        {
            if (game == null || PendingTeleports.IsEmpty)
                return;

            DateTime utcNow = DateTime.UtcNow;
            foreach (var kvp in PendingTeleports)
            {
                ulong playerDbId = kvp.Key;
                PendingTeleport pending = kvp.Value;
                if (pending.ExpiresAtUtc <= utcNow)
                {
                    PendingTeleports.TryRemove(playerDbId, out _);
                    continue;
                }

                if (pending.NotBeforeUtc > utcNow)
                    continue;

                Player player = game.EntityManager.GetEntityByDbGuid<Player>(playerDbId);
                if (player == null || PendingTeleports.TryRemove(playerDbId, out _) == false)
                    continue;

                player.BeginTeleportToPartyMember(pending.TargetPlayerDbId);
            }
        }

        private readonly record struct PendingTeleport(
            ulong TargetPlayerDbId,
            DateTime NotBeforeUtc,
            DateTime ExpiresAtUtc);
    }
}
