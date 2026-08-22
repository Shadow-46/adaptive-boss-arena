using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the geometry that decides whether a swing reaches a body.
    /// </summary>
    /// <remarks>
    /// These exist because of a fault a play-tester felt long before anyone found it: swings that
    /// visibly overlapped the boss did not register. The angle was measured to the target's centre
    /// and compared against the arc directly, which quietly required the target's axis to be inside
    /// the wedge and discarded everything between that axis and the near edge of its body.
    /// </remarks>
    [TestFixture]
    public sealed class ArcHitTests
    {
        /// <summary>The boss's hurtbox radius, the case the fault was worst against.</summary>
        private const float BossRadius = 0.85f;

        /// <summary>The player's hurtbox radius.</summary>
        private const float PlayerRadius = 0.38f;

        private static Vector3 AtAngle(float degrees, float distance) =>
            Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * distance;

        [Test]
        public void ABodyOverlappingTheWedgeIsHit_EvenWhenItsCentreIsOutside()
        {
            // The exact failure. A 90-degree swing reaches 45 degrees off-axis; a boss centred at 55
            // degrees and 2.5 metres away still has most of its body inside the wedge, because at
            // that range its 0.85 m radius spans roughly 19 degrees either side of its centre.
            bool hit = AttackHitDetector.IsWithinArc(
                Vector3.forward, AtAngle(55f, 2.5f), BossRadius, arcDegrees: 90f);

            Assert.IsTrue(hit, "A swing overlapping the boss should connect even if its centre does not.");
        }

        [Test]
        public void ABodyFullyOutsideTheWedgeIsMissed()
        {
            // Far enough off-axis that even the near edge is clear of the wedge. The fix must not
            // turn every arc into a circle.
            bool hit = AttackHitDetector.IsWithinArc(
                Vector3.forward, AtAngle(85f, 2.5f), BossRadius, arcDegrees: 90f);

            Assert.IsFalse(hit, "A body clear of the wedge should still be missed.");
        }

        [Test]
        public void ATargetBehindTheAttackerIsMissed()
        {
            bool hit = AttackHitDetector.IsWithinArc(
                Vector3.forward, AtAngle(180f, 2.5f), BossRadius, arcDegrees: 120f);

            Assert.IsFalse(hit, "Nothing behind the attacker should ever be hit by a forward swing.");
        }

        [Test]
        public void AWiderBodyIsReachedFromFurtherOffAxis()
        {
            // The correction has to scale with the target's size, which is why this matters far more
            // for the player hitting the boss than the other way round.
            // Between the two thresholds: the boss's 0.85 m body reaches about 65 degrees off-axis
            // at this range, the player's 0.38 m body only about 54.
            const float offAxis = 58f;
            const float distance = 2.5f;

            Assert.IsTrue(
                AttackHitDetector.IsWithinArc(
                    Vector3.forward, AtAngle(offAxis, distance), BossRadius, 90f),
                "The wide body should be reachable at this angle.");

            Assert.IsFalse(
                AttackHitDetector.IsWithinArc(
                    Vector3.forward, AtAngle(offAxis, distance), PlayerRadius, 90f),
                "The narrow body should not be, at the same angle and range.");
        }

        [Test]
        public void TheSameBodyIsHarderToReachFromFurtherAway()
        {
            // A body subtends less angle the further off it stands, so a swing that catches its edge
            // up close should miss the same edge at range.
            Assert.IsTrue(
                AttackHitDetector.IsWithinArc(Vector3.forward, AtAngle(60f, 1.2f), BossRadius, 90f));

            Assert.IsFalse(
                AttackHitDetector.IsWithinArc(Vector3.forward, AtAngle(60f, 6f), BossRadius, 90f));
        }

        [Test]
        public void APointBlankTargetIsAlwaysHit()
        {
            // No meaningful direction to reject, and a miss at zero range reads as a bug.
            Assert.IsTrue(
                AttackHitDetector.IsWithinArc(Vector3.forward, Vector3.zero, BossRadius, 40f));

            // A body wider than its own distance encloses the attacker, so a narrow swing still
            // connects with something pressed against them regardless of which way they face.
            Assert.IsTrue(
                AttackHitDetector.IsWithinArc(
                    Vector3.forward, AtAngle(170f, 0.4f), BossRadius, 40f));
        }

        [Test]
        public void AZeroWidthTargetFallsBackToTheCentreTest()
        {
            // With no width the behaviour must be exactly the old one, so the change is provably an
            // extension rather than a loosening.
            Assert.IsTrue(
                AttackHitDetector.IsWithinArc(Vector3.forward, AtAngle(44f, 2.5f), 0f, 90f));

            Assert.IsFalse(
                AttackHitDetector.IsWithinArc(Vector3.forward, AtAngle(46f, 2.5f), 0f, 90f));
        }

        [Test]
        public void ANegativeRadiusIsTreatedAsNoWidth()
        {
            Assert.IsFalse(
                AttackHitDetector.IsWithinArc(Vector3.forward, AtAngle(46f, 2.5f), -1f, 90f));
        }
    }
}
