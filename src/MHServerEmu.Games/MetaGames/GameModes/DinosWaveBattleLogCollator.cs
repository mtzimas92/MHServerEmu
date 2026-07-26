using MHServerEmu.Core.Logging;

namespace MHServerEmu.Games.MetaGames.GameModes
{
    /// <summary>
    /// Per-run log collator for PvEScaleGameMode (Dinos Invade Manhattan) threat-balance diagnostics.
    /// Buffers structured tick/kill/power-up/phase-transition lines in memory for the lifetime of one
    /// continuous playthrough (spanning every phase's separate MetaGameMode instance, keyed by
    /// MetaGame.Id like _persistedThreatByMetaGameId), and flushes to a dedicated file only on the
    /// run's true end (boss defeated, or any phase failure) - mirrors StashAffinityLogCollator's
    /// buffer-then-flush-at-session-end shape.
    /// </summary>
    public static class DinosWaveBattleLogCollator
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly Dictionary<ulong, Session> _sessions = new();

        private class Session
        {
            public readonly ulong MetaGameId;
            public readonly DateTime StartTime;
            public readonly System.Text.StringBuilder Buffer = new();
            public string AvatarLabel = "unknown";

            public Session(ulong metaGameId)
            {
                MetaGameId = metaGameId;
                StartTime = DateTime.Now;
            }
        }

        public static void WriteLine(ulong metaGameId, string line)
        {
            if (metaGameId == 0 || string.IsNullOrEmpty(line)) return;

            Session session;
            lock (_sessions)
            {
                if (_sessions.TryGetValue(metaGameId, out session) == false)
                {
                    session = new Session(metaGameId);
                    _sessions[metaGameId] = session;
                }
            }

            lock (session)
            {
                TimeSpan elapsed = DateTime.Now - session.StartTime;
                session.Buffer.AppendLine($"[+{elapsed:mm\\:ss}] {line}");
            }
        }

        public static void SetAvatarLabel(ulong metaGameId, string label)
        {
            if (metaGameId == 0 || string.IsNullOrEmpty(label)) return;
            lock (_sessions)
            {
                if (_sessions.TryGetValue(metaGameId, out Session session))
                    session.AvatarLabel = label;
            }
        }

        /// <summary>
        /// Flushes the buffered run to a dedicated file and clears the session. Call only on a true
        /// run end (boss SucceedMode or any FailMode) - not on phase-to-phase transitions.
        /// </summary>
        public static void EndRun(ulong metaGameId, string outcome)
        {
            if (metaGameId == 0) return;

            Session session;
            lock (_sessions)
            {
                if (_sessions.TryGetValue(metaGameId, out session) == false) return;
                _sessions.Remove(metaGameId);
            }

            try
            {
                string dir = Path.Combine("Logs", "DinosWaveBattle");
                Directory.CreateDirectory(dir);
                string safeAvatar = string.Join("_", session.AvatarLabel.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"DinosWaveBattle_{safeAvatar}_{outcome}_{session.StartTime:yyyyMMdd_HHmmss}_{metaGameId}.log";
                string path = Path.Combine(dir, fileName);
                File.WriteAllText(path, session.Buffer.ToString());
                Logger.Info($"[DinosWaveBattleCollator] Wrote {session.Buffer.Length} chars to '{path}'.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DinosWaveBattleCollator] Flush failed for MetaGame {metaGameId}: {ex.Message}");
            }
        }
    }
}
