using AdaptiveBossArena.AI;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how the boss moves between attacks.
    /// </summary>
    /// <remarks>
    /// The player asked for "slow and heavy walks", and got a boss that circled at a run and flickered between
    /// closing and backing off at a hard boundary.
    /// </remarks>
    [TestFixture]
    public sealed class BossPacingTests
    {
        [Test]
        public void TheBossStalksAtAWalkNotARun()
        {
            Vector3 move = BossPacing.Stalk(Vector3.forward, distance: 3f, preferredRange: 3f, stalkFraction: 0.35f);

            Assert.AreEqual(0.35f, move.magnitude, 0.001f, "The stalk is not at the configured walk.");
        }

        [Test]
        public void AtItsPreferredRangeItCirclesRatherThanClosing()
        {
            Vector3 move = BossPacing.Stalk(Vector3.forward, distance: 3f, preferredRange: 3f, stalkFraction: 0.35f);

            Assert.AreEqual(0f, Vector3.Dot(move.normalized, Vector3.forward), 0.001f, "It walks toward the player at its preferred range.");
        }

        [Test]
        public void ClosingAndGivingGroundBlendSmoothlyThroughThePreferredRange()
        {
            float previous = float.PositiveInfinity;

            // Walking the distance in from far to close, the pull toward the player must fall steadily - no edge.
            for (float distance = 6f; distance >= 0f; distance -= 0.25f)
            {
                Vector3 move = BossPacing.Stalk(Vector3.forward, distance, preferredRange: 3f, stalkFraction: 1f);
                float pull = Vector3.Dot(move, Vector3.forward);

                Assert.LessOrEqual(pull, previous + 0.0001f, $"The pull jumped at {distance} m.");
                previous = pull;
            }
        }

        [Test]
        public void ItWalksTheLastStretchAndRunsOnlyAcrossRealDistance()
        {
            float near = BossPacing.ApproachSpeed(distanceBeyondReach: 0.5f, runDistance: 6f, walkFraction: 0.45f, aggression: 1f);
            float far = BossPacing.ApproachSpeed(distanceBeyondReach: 8f, runDistance: 6f, walkFraction: 0.45f, aggression: 1f);

            Assert.Less(near, 0.5f, "It runs the last stretch to a blow.");
            Assert.AreEqual(1f, far, 0.001f, "It does not run across real distance.");
        }

        [Test]
        public void LearnedAggressionMakesItComeOnHarder()
        {
            float calm = BossPacing.ApproachSpeed(3f, 6f, 0.45f, aggression: 0f);
            float pressing = BossPacing.ApproachSpeed(3f, 6f, 0.45f, aggression: 1f);

            Assert.Greater(pressing, calm);
        }
    }
}
