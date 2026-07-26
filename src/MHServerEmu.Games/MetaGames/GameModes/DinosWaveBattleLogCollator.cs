using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;

namespace MHServerEmu.Games.MetaGames.GameModes
{
    /// <summary>
    /// Per-run log collator for PvEScaleGameMode (Dinos Invade Manhattan) threat-balance diagnostics.
    /// Buffers structured tick/kill/power-up/phase-transition lines in memory for the lifetime of one
    /// continuous playthrough (spanning every phase's separate MetaGameMode instance), and flushes to
    /// a dedicated file under {ServerRoot}/Logs/DinosWaveBattle only on the run's true end (boss
    /// defeated, or any phase failure) - mirrors StashAffinityLogCollator's buffer-then-flush-at-
    /// session-end shape. Uses FileHelper.ServerRoot (not a bare relative "Logs" path) so the output
    /// directory is anchored to the server executable regardless of the process's working directory -
    /// a bare relative path silently wrote elsewhere (or nowhere visible) whenever launched with a
    /// different CWD, e.g. via a service/wrapper.
    ///
    /// Sessions are keyed by (Game.Id, MetaGame.Id), NOT bare MetaGame.Id. MetaGame.Id (like every
    /// Entity.Id) is only unique WITHIN a single Game instance - GameThreadManager runs a pool of
    /// worker threads that each drive multiple concurrent Game instances in one process
    /// (GameThreadManager.cs), and WorldManager.GetOrCreatePublicRegion/RegionLoadBalancer can and does
    /// spin up more than one live "Dinos Invade Manhattan" region (each in its own Game instance) under
    /// player load. Keying on bare MetaGame.Id let two completely unrelated, correctly-functioning
    /// concurrent runs collide on the same dictionary entry and interleave their WriteLine calls into
    /// one merged log - confirmed live: two independent kill/threat streams with diverging thresholds
    /// and independently-restarting killsThisPhase counters appeared interleaved second-by-second in a
    /// single file. This was a logging artifact, not evidence of two mode instances corrupting the same
    /// real playthrough.
    /// </summary>
    public static class DinosWaveBattleLogCollator
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly Dictionary<(ulong GameId, ulong MetaGameId), Session> _sessions = new();

        private class Session
        {
            public readonly ulong GameId;
            public readonly ulong MetaGameId;
            public readonly DateTime StartTime;
            public readonly System.Text.StringBuilder Buffer = new();
            public string PlayerLabel = "unknown";

            public Session(ulong gameId, ulong metaGameId)
            {
                GameId = gameId;
                MetaGameId = metaGameId;
                StartTime = DateTime.Now;
            }
        }

        public static void WriteLine(ulong gameId, ulong metaGameId, string line)
        {
            if (metaGameId == 0 || string.IsNullOrEmpty(line)) return;

            var key = (gameId, metaGameId);
            Session session;
            lock (_sessions)
            {
                if (_sessions.TryGetValue(key, out session) == false)
                {
                    session = new Session(gameId, metaGameId);
                    _sessions[key] = session;
                }
            }

            lock (session)
            {
                TimeSpan elapsed = DateTime.Now - session.StartTime;
                session.Buffer.AppendLine($"[+{elapsed:mm\\:ss}] {line}");
            }
        }

        public static void SetPlayerLabel(ulong gameId, ulong metaGameId, string label)
        {
            if (metaGameId == 0 || string.IsNullOrEmpty(label)) return;
            lock (_sessions)
            {
                if (_sessions.TryGetValue((gameId, metaGameId), out Session session))
                    session.PlayerLabel = label;
            }
        }

        /// <summary>
        /// Flushes the buffered run to a dedicated file and clears the session. Call only on a true
        /// run end (boss SucceedMode or any FailMode) - not on phase-to-phase transitions.
        /// </summary>
        public static void EndRun(ulong gameId, ulong metaGameId, string outcome)
        {
            if (metaGameId == 0) return;

            var key = (gameId, metaGameId);
            Session session;
            lock (_sessions)
            {
                if (_sessions.TryGetValue(key, out session) == false) return;
                _sessions.Remove(key);
            }

            try
            {
                string dir = Path.Combine(FileHelper.ServerRoot, "Logs", "DinosWaveBattle");
                Directory.CreateDirectory(dir);
                string safePlayerName = string.Join("_", session.PlayerLabel.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"DinosWaveBattle_{safePlayerName}_{outcome}_{session.StartTime:yyyyMMdd_HHmmss}_{gameId}_{metaGameId}.log";
                string path = Path.Combine(dir, fileName);
                File.WriteAllText(path, session.Buffer.ToString());
                Logger.Info($"[DinosWaveBattleCollator] Wrote {session.Buffer.Length} chars to '{path}'.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DinosWaveBattleCollator] Flush failed for Game {gameId} MetaGame {metaGameId}: {ex.Message}");
            }
        }
    }
}
