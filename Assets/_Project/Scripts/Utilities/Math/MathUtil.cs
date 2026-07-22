using UnityEngine;

namespace AdaptiveBossArena.Utilities
{
    /// <summary>
    /// Small numeric helpers shared across gameplay systems.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is deliberately free of engine state such as <c>Time.deltaTime</c>, so it can
    /// be exercised from edit-mode tests without entering play mode.
    /// </para>
    /// <para>
    /// This type deliberately does not live in a nested <c>Math</c> namespace. It did once, and the
    /// result was that a bare <c>Math.Pow</c> anywhere under <c>AdaptiveBossArena.Utilities</c>
    /// resolved to that namespace instead of <see cref="System.Math"/>, which fails to compile with
    /// a thoroughly misleading message. <c>System.Math</c> is still written out in full below as a
    /// belt-and-braces measure.
    /// </para>
    /// </remarks>
    public static class MathUtil
    {
        /// <summary>Linearly maps a value from one range onto another without clamping.</summary>
        /// <param name="value">Input value.</param>
        /// <param name="fromMin">Lower bound of the input range.</param>
        /// <param name="fromMax">Upper bound of the input range.</param>
        /// <param name="toMin">Lower bound of the output range.</param>
        /// <param name="toMax">Upper bound of the output range.</param>
        /// <returns>The remapped value, or <paramref name="toMin"/> when the input range is degenerate.</returns>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float span = fromMax - fromMin;
            if (Mathf.Abs(span) < Mathf.Epsilon)
            {
                return toMin;
            }

            return toMin + (value - fromMin) / span * (toMax - toMin);
        }

        /// <summary>Maps a value onto the zero-to-one range and clamps it.</summary>
        /// <param name="value">Input value.</param>
        /// <param name="fromMin">Value that maps to zero.</param>
        /// <param name="fromMax">Value that maps to one.</param>
        /// <returns>The normalised, clamped value.</returns>
        public static float Remap01(float value, float fromMin, float fromMax) =>
            Mathf.Clamp01(Remap(value, fromMin, fromMax, 0f, 1f));

        /// <summary>
        /// Frame-rate independent exponential smoothing toward a target.
        /// </summary>
        /// <remarks>
        /// Prefer this over <c>Mathf.Lerp(current, target, someRate * Time.deltaTime)</c>, which
        /// silently changes behaviour with frame rate. Adaptation parameters are eased with this so
        /// the boss's strategy shifts feel identical at 60 and 144 frames per second.
        /// </remarks>
        /// <param name="current">Current value.</param>
        /// <param name="target">Value being approached.</param>
        /// <param name="halfLifeSeconds">Time to close half the remaining distance. Non-positive snaps to target.</param>
        /// <param name="deltaTimeSeconds">Elapsed time this step.</param>
        /// <returns>The eased value.</returns>
        public static float Damp(float current, float target, float halfLifeSeconds, float deltaTimeSeconds)
        {
            if (halfLifeSeconds <= 0f)
            {
                return target;
            }

            float retained = (float)System.Math.Pow(0.5d, deltaTimeSeconds / halfLifeSeconds);
            return target + (current - target) * retained;
        }

        /// <summary>Frame-rate independent exponential smoothing for vectors.</summary>
        /// <param name="current">Current value.</param>
        /// <param name="target">Value being approached.</param>
        /// <param name="halfLifeSeconds">Time to close half the remaining distance.</param>
        /// <param name="deltaTimeSeconds">Elapsed time this step.</param>
        /// <returns>The eased vector.</returns>
        public static Vector3 Damp(Vector3 current, Vector3 target, float halfLifeSeconds, float deltaTimeSeconds)
        {
            if (halfLifeSeconds <= 0f)
            {
                return target;
            }

            float retained = (float)System.Math.Pow(0.5d, deltaTimeSeconds / halfLifeSeconds);
            return target + (current - target) * retained;
        }

        /// <summary>
        /// Converts an observation count into a zero-to-one confidence weight.
        /// </summary>
        /// <remarks>
        /// The learning system must not act on a single data point. Every derived behaviour feature
        /// carries a confidence produced by this curve, and counter-strategies multiply their weight
        /// by it, so a habit seen twice nudges the boss while a habit seen thirty times drives it.
        /// </remarks>
        /// <param name="sampleCount">Number of observations gathered.</param>
        /// <param name="halfConfidenceSampleCount">Sample count at which confidence reaches one half. Must be positive.</param>
        /// <returns>Confidence in the range zero to one.</returns>
        public static float ConfidenceFromSampleCount(int sampleCount, int halfConfidenceSampleCount)
        {
            if (sampleCount <= 0)
            {
                return 0f;
            }

            if (halfConfidenceSampleCount <= 0)
            {
                return 1f;
            }

            double retained = System.Math.Pow(0.5d, (double)sampleCount / halfConfidenceSampleCount);
            return (float)(1d - retained);
        }

        /// <summary>Projects a world vector onto the arena's horizontal plane.</summary>
        /// <param name="worldVector">Vector in world space.</param>
        /// <returns>The x and z components as a two-dimensional vector.</returns>
        public static Vector2 FlattenToPlane(Vector3 worldVector) => new Vector2(worldVector.x, worldVector.z);

        /// <summary>Lifts a horizontal-plane vector back into world space at the given height.</summary>
        /// <param name="planeVector">Vector on the horizontal plane.</param>
        /// <param name="height">World-space y component.</param>
        /// <returns>The corresponding world vector.</returns>
        public static Vector3 ToWorld(Vector2 planeVector, float height = 0f) =>
            new Vector3(planeVector.x, height, planeVector.y);

        /// <summary>Squared horizontal distance between two world positions, ignoring height.</summary>
        /// <param name="a">First position.</param>
        /// <param name="b">Second position.</param>
        /// <returns>The squared distance on the horizontal plane.</returns>
        public static float SqrPlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>Horizontal distance between two world positions, ignoring height.</summary>
        /// <param name="a">First position.</param>
        /// <param name="b">Second position.</param>
        /// <returns>The distance on the horizontal plane.</returns>
        public static float PlanarDistance(Vector3 a, Vector3 b) => Mathf.Sqrt(SqrPlanarDistance(a, b));

        /// <summary>Normalises a vector, returning <see cref="Vector2.zero"/> when it is degenerate.</summary>
        /// <param name="value">Vector to normalise.</param>
        /// <returns>A unit vector, or zero.</returns>
        public static Vector2 SafeNormalize(Vector2 value)
        {
            float sqrMagnitude = value.sqrMagnitude;
            return sqrMagnitude < Mathf.Epsilon ? Vector2.zero : value / Mathf.Sqrt(sqrMagnitude);
        }

        /// <summary>Normalises a vector, returning <see cref="Vector3.zero"/> when it is degenerate.</summary>
        /// <param name="value">Vector to normalise.</param>
        /// <returns>A unit vector, or zero.</returns>
        public static Vector3 SafeNormalize(Vector3 value)
        {
            float sqrMagnitude = value.sqrMagnitude;
            return sqrMagnitude < Mathf.Epsilon ? Vector3.zero : value / Mathf.Sqrt(sqrMagnitude);
        }
    }
}
