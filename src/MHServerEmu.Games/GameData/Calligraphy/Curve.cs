using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;

namespace MHServerEmu.Games.GameData.Calligraphy
{
    /// <summary>
    /// Contains a collection of numeric values.
    /// </summary>
    public class Curve
    {
        private CurveId _curveRef;
        private float[] _values;

        public int MinPosition { get; private set; }    // m_startPosition
        public int MaxPosition { get; private set; }    // m_endPosition

        public bool IsCurveZero { get; private set; } = true;

        public Curve() { }

        public override string ToString()
        {
            return GameDatabase.GetCurveName(_curveRef);
        }

        public bool Load(CalligraphyReader reader, CurveId curveRef)
        {
            _curveRef = curveRef;

            if (!Verify.IsTrue(reader.ReadHeader("CRV"))) return false;

            if (!Verify.IsTrue(reader.Read(out int startPosition))) return false;
            MinPosition = startPosition;

            if (!Verify.IsTrue(reader.Read(out int endPosition))) return false;
            MaxPosition = endPosition;

            int numElements = endPosition - startPosition + 1;
            if (!Verify.IsTrue(numElements >= 1)) return false;

            _values = new float[numElements];
            for (int i = 0; i < numElements; i++)
            {
                if (!Verify.IsTrue(reader.Read(out double value))) return false;
                _values[i] = (float)value;
                IsCurveZero &= value == 0;
            }

            return true;
        }

        /// <summary>
        /// Sets the value at the specified position. Returns <see langword="false"/> if the position is outside
        /// the curve.
        /// </summary>
        /// <remarks>
        /// Exists so curves can be patched server-side rather than by editing the .sip and shipping it to every
        /// player. Curves are evaluated on the server (GameDatabase.GetCurve, used by loot resolution and
        /// property evals), so a change here is authoritative for what players actually receive.
        ///
        /// CAVEAT: the client keeps its own copy of every curve, and may use it for tooltip previews. A
        /// server-only change can therefore show one number and deliver another. Check the tooltip against the
        /// real result before relying on a patched curve for anything player-facing.
        /// </remarks>
        public bool SetAt(int position, float value)
        {
            if (position < MinPosition || position > MaxPosition)
                return false;

            _values[position - MinPosition] = value;
            RefreshIsCurveZero();
            return true;
        }

        /// <summary>
        /// Multiplies every value in the curve by <paramref name="scale"/>.
        /// </summary>
        /// <remarks>
        /// The usual way to retune a curve: these are progression curves, so rebalancing means changing the
        /// whole shape, not one point. Editing a single position leaves a discontinuity at that index.
        /// </remarks>
        public void ScaleAll(float scale)
        {
            for (int i = 0; i < _values.Length; i++)
                _values[i] *= scale;

            RefreshIsCurveZero();
        }

        /// <summary>
        /// Recomputes <see cref="IsCurveZero"/> after a mutation.
        /// </summary>
        /// <remarks>
        /// Load() accumulates this while reading, so it must be rebuilt rather than updated incrementally -
        /// a patch can take the last non-zero value to zero, or bring a zeroed curve back.
        /// </remarks>
        private void RefreshIsCurveZero()
        {
            IsCurveZero = true;

            for (int i = 0; i < _values.Length; i++)
            {
                if (_values[i] != 0f)
                {
                    IsCurveZero = false;
                    break;
                }
            }
        }

        // NOTE: The client uses a bunch of copy-pasted code here for different versions of GetAt(), we just wrap the same float functions for int versions.

        /// <summary>
        /// Returns the value at the specified position as <see cref="float"/>.
        /// </summary>
        public float GetAt(int position)
        {
            if (!Verify.IsTrue(position >= MinPosition, $"Curve position ({position}) below min of ({MinPosition}) Curve: {this}"))
                return _values[0];

            if (!Verify.IsTrue(position <= MaxPosition, $"Curve position ({position}) above max of ({MaxPosition}) Curve: {this}"))
                return _values[MaxPosition - MinPosition];

            //position = Math.Clamp(position, MinPosition, MaxPosition);    // Unnecessary client-side clamp that is already handled by verify checks above
            int index = position - MinPosition;
            return _values[index];
        }

        /// <summary>
        /// Retrieves the value at the specified position as <see cref="float"/>. Returns <see langword="true"/> if successful.
        /// </summary>
        public bool GetAt(int position, out float value)
        {
            if (position < MinPosition)
            {
                value = _values[0];
                Verify.IsTrue(false, $"Curve position ({position}) below min of ({MinPosition}) Curve: {this}");
                return false;
            }
            else if (position > MaxPosition)
            {
                value = _values[MaxPosition - MinPosition];
                Verify.IsTrue(false, $"Curve position ({position}) above max of ({MaxPosition}) Curve: {this}");
                return false;
            }

            int index = position - MinPosition;
            value = _values[index];
            return true;
        }

        /// <summary>
        /// Returns the value at the specified position as <see cref="int"/>.
        /// </summary>
        public int GetIntAt(int position)
        {
            return MathHelper.RoundToInt(GetAt(position));
        }

        /// <summary>
        /// Retrieves the value at the specified position as <see cref="int"/>. Returns <see langword="true"/> if successful.
        /// </summary>
        public bool GetIntAt(int position, out int value)
        {
            bool result = GetAt(position, out float floatValue);
            value = MathHelper.RoundToInt(floatValue);
            return result;
        }

        /// <summary>
        /// Returns the value at the specified position as <see cref="long"/>.
        /// </summary>
        public long GetInt64At(int position)
        {
            return MathHelper.RoundToInt64(GetAt(position));
        }

        /// <summary>
        /// Retrieves the value at the specified position as <see cref="long"/>. Returns <see langword="true"/> if successful.
        /// </summary>
        public bool GetInt64At(int position, out long value)
        {
            bool result = GetAt(position, out float floatValue);
            value = MathHelper.RoundToInt64(floatValue);
            return result;
        }

        /// <summary>
        /// Sums the values within specified range and returns the result as <see cref="float"/>.
        /// </summary>
        public float IntegrateDiscrete(int start, int end)
        {
            if (!Verify.IsTrue(start >= MinPosition, $"Curve start (%d) below min of (%d) Curve: %s"))
                return 0f;

            if (!Verify.IsTrue(start <= MaxPosition, $"Curve start (%d) above max of (%d) Curve: %s"))
                return 0f;

            if (!Verify.IsTrue(end >= MinPosition, $"Curve end (%d) below min of (%d) Curve: %s"))
                return 0f;

            if (!Verify.IsTrue(end <= MaxPosition, $"Curve end (%d) above max of (%d) Curve: %s"))
                return 0f;

            float result = 0;
            for (int i = start; i <= end; i++)
                result += _values[i - MinPosition];
            return result;
        }

        /// <summary>
        /// Sums the values within specified range and returns the result as <see cref="int"/>.
        /// </summary>
        public int IntegrateDiscreteInt(int start, int end)
        {
            return MathHelper.RoundToInt(IntegrateDiscrete(start, end));
        }

        /// <summary>
        /// Checks if the specified index is within range of this <see cref="Curve"/>.
        /// </summary>
        public bool IndexInRange(int index)
        {
            return index >= MinPosition && index <= MaxPosition;
        }
    }
}
