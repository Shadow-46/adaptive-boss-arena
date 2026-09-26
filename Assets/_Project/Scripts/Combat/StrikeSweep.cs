using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// How a striking part is tested between two frames, so a fast swing cannot pass through a body unseen.
    /// </summary>
    /// <remarks>
    /// A great sword's tip crosses well over a metre in one frame of a swing. Testing only where it ends up
    /// would let it skip clean over the knight between two frames; testing its whole path would cost a
    /// swept-volume query the physics engine does not offer for capsules. Sampling the path in steps no longer
    /// than the part's own thickness finds the same contacts at a handful of overlap queries per frame.
    /// </remarks>
    public static class StrikeSweep
    {
        /// <summary>
        /// How many samples cover the path between two poses.
        /// </summary>
        /// <param name="previousStart">The base last frame.</param>
        /// <param name="previousEnd">The tip last frame.</param>
        /// <param name="start">The base now.</param>
        /// <param name="end">The tip now.</param>
        /// <param name="radius">The part's thickness.</param>
        /// <param name="maximum">A ceiling on samples, so a teleport cannot cost a hundred queries.</param>
        /// <returns>At least one sample; more the further the fastest end travelled.</returns>
        public static int SampleCount(
            Vector3 previousStart, Vector3 previousEnd, Vector3 start, Vector3 end, float radius, int maximum)
        {
            float travel = Mathf.Max(Vector3.Distance(previousStart, start), Vector3.Distance(previousEnd, end));
            int samples = Mathf.CeilToInt(travel / Mathf.Max(0.01f, radius));

            return Mathf.Clamp(samples, 1, Mathf.Max(1, maximum));
        }

        /// <summary>The part's pose part-way between two frames.</summary>
        /// <param name="previousStart">The base last frame.</param>
        /// <param name="previousEnd">The tip last frame.</param>
        /// <param name="start">The base now.</param>
        /// <param name="end">The tip now.</param>
        /// <param name="t">Zero for last frame's pose, one for this frame's.</param>
        /// <param name="sampleStart">The base at that point.</param>
        /// <param name="sampleEnd">The tip at that point.</param>
        public static void Between(
            Vector3 previousStart, Vector3 previousEnd, Vector3 start, Vector3 end, float t,
            out Vector3 sampleStart, out Vector3 sampleEnd)
        {
            t = Mathf.Clamp01(t);
            sampleStart = Vector3.Lerp(previousStart, start, t);
            sampleEnd = Vector3.Lerp(previousEnd, end, t);
        }
    }
}
