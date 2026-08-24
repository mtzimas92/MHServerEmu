namespace MHServerEmu.Games.GameData.PatchManager
{
    /// <summary>
    /// A single edit to a <see cref="Calligraphy.Curve"/>, read from a PatchCurve*.json file.
    /// </summary>
    /// <remarks>
    /// Curves drive progression maths - relic stack count to damage bonus, item level to stat, credits per
    /// kill - and were previously unreachable: the patcher only ever touched prototypes, so retuning one meant
    /// editing Calligraphy.sip and shipping it to every player.
    ///
    /// They do not need to be. Curves are evaluated SERVER-side (GameDatabase.GetCurve, consumed by
    /// ItemResolver, Eval and PropertyCollection), so patching them in memory after load is authoritative for
    /// what players actually receive, with no client distribution.
    ///
    /// CAVEAT worth testing before relying on it: the client keeps its own copy of every curve and may use it
    /// for tooltip previews, so a server-only change can display one number and deliver another.
    ///
    /// Deliberately NOT a whole-payload replacement. OpenCalligraphy models curve patches as a base64 blob,
    /// which is unreviewable - the whole point of this format is that a patch file can be read and understood
    /// a year later.
    /// </remarks>
    public class CurvePatchEntry
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Curve name, e.g. "Curves/Items/RelicDamageBonus.curve".
        /// </summary>
        public string Curve { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Multiplies every value in the curve. The usual way to retune, since these are progression curves
        /// and rebalancing means changing the whole shape rather than one point.
        /// </summary>
        public float? Scale { get; set; }

        /// <summary>
        /// Sets a single position instead. Use sparingly - editing one index leaves a step in an otherwise
        /// smooth curve.
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// The value to write at <see cref="Position"/>.
        /// </summary>
        public float? Value { get; set; }

        /// <summary>
        /// Optional. The value expected at <see cref="Position"/> before writing; a mismatch warns, exactly as
        /// it does for prototype patches.
        /// </summary>
        public float? OriginalValue { get; set; }
    }
}
