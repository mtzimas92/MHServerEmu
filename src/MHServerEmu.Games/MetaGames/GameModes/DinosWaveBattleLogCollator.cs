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
    ///
    /// EndRun() writes the SAME buffered run content to one file per distinct participant label
    /// (AddParticipant()), rather than a single file named after one arbitrarily-picked player.
    /// Support could not otherwise find a specific player's run without opening every file in the
    /// folder - the run is a shared instance with no per-player content to split out, so duplicating
    /// the whole log under every participant's own name (searchable by exact player name) is the
    /// practical fix, accepted duplication cost included (folder gets pruned every few days anyway).
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

            // Keyed by a stable player id (DatabaseUniqueId), NOT the display label - the label
            // includes character level, and a player who levels up mid-run (common, given the XP
            // orbs bosses grant) would otherwise get re-added under a new distinct string each time,
            // producing one duplicate file per level gained instead of one file for that player.
            // The dictionary value always holds their MOST RECENT label, so leveling during the run
            // just updates the filename in place rather than creating extra entries.
            public readonly Dictionary<ulong, string> ParticipantLabelsByPlayerId = new();

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

        /// <summary>
        /// Adds (or updates) a player in this run's participant roster, so the run's log gets
        /// written under their name too on flush. Safe to call repeatedly for the same player across
        /// every phase of a run (e.g. once per OnActivate() for every in-world player) - keyed by
        /// playerId, so re-adding an already-known participant just refreshes their label (e.g. after
        /// a level-up) instead of creating a second entry. Also lazily creates the session if this is
        /// the very first call for a run (a phase can call this before any WriteLine()).
        /// </summary>
        public static void AddParticipant(ulong gameId, ulong metaGameId, ulong playerId, string label)
        {
            if (metaGameId == 0 || playerId == 0 || string.IsNullOrEmpty(label)) return;

            var key = (gameId, metaGameId);
            lock (_sessions)
            {
                if (_sessions.TryGetValue(key, out Session session) == false)
                {
                    session = new Session(gameId, metaGameId);
                    _sessions[key] = session;
                }

                session.ParticipantLabelsByPlayerId[playerId] = label;
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

                List<string> participants = session.ParticipantLabelsByPlayerId.Count > 0
                    ? new List<string>(session.ParticipantLabelsByPlayerId.Values)
                    : new List<string> { "unknown" };

                string content = session.Buffer.ToString();
                string timestamp = $"{session.StartTime:yyyyMMdd_HHmmss}";

                foreach (string participantLabel in participants)
                {
                    string safePlayerName = string.Join("_", participantLabel.Split(Path.GetInvalidFileNameChars()));
                    string fileName = $"DinosWaveBattle_{safePlayerName}_{outcome}_{timestamp}_{gameId}_{metaGameId}.log";
                    string path = Path.Combine(dir, fileName);
                    File.WriteAllText(path, content);
                }

                Logger.Info($"[DinosWaveBattleCollator] Wrote {content.Length} chars to {participants.Count} file(s) (one per participant) for Game {gameId} MetaGame {metaGameId}.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DinosWaveBattleCollator] Flush failed for Game {gameId} MetaGame {metaGameId}: {ex.Message}");
            }
        }
    }
}
