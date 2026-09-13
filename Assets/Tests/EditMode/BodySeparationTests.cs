using AdaptiveBossArena.Combat.Movement;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how two overlapping fighters are pushed apart.
    /// </summary>
    [TestFixture]
    public sealed class BodySeparationTests
    {
        private const float KnightRadius = 0.4f;
        private const float BruteRadius = 0.9f;
        private const float KnightMass = 1f;
        private const float BruteMass = 4f;

        [Test]
        public void BodiesThatDoNotTouchAreLeftAlone()
        {
            BodySeparation.Result result = BodySeparation.Resolve(
                Vector3.zero, KnightRadius, KnightMass,
                new Vector3(3f, 0f, 0f), BruteRadius, BruteMass,
                Vector3.zero);

            Assert.IsFalse(result.Overlapped);
        }

        [Test]
        public void OverlappingBodiesEndExactlyInContact()
        {
            Vector3 knight = Vector3.zero;
            Vector3 brute = new Vector3(1f, 0f, 0f);

            BodySeparation.Result result = BodySeparation.Resolve(
                knight, KnightRadius, KnightMass, brute, BruteRadius, BruteMass, Vector3.zero);

            float after = Vector3.Distance(knight + result.PushFirst, brute + result.PushSecond);

            Assert.IsTrue(result.Overlapped);
            Assert.AreEqual(KnightRadius + BruteRadius, after, 0.001f);
        }

        [Test]
        public void TheHeavierBodyGivesLessGround()
        {
            // Four times the mass should move a quarter as far: leaning into the brute shoves it a
            // little and throws the knight back a lot.
            BodySeparation.Result result = BodySeparation.Resolve(
                Vector3.zero, KnightRadius, KnightMass,
                new Vector3(1f, 0f, 0f), BruteRadius, BruteMass,
                Vector3.zero);

            Assert.AreEqual(4f, result.PushFirst.magnitude / result.PushSecond.magnitude, 0.01f);
        }

        [Test]
        public void BodiesThatPassedThroughEachOtherArePushedBackToTheSideTheyCameFrom()
        {
            // Last frame the brute was to the knight's right. A long frame carried the knight's centre
            // past the brute's. Resolving along the new direction would finish pushing the knight out
            // of the brute's far side - through it.
            Vector3 knight = new Vector3(0.2f, 0f, 0f);
            Vector3 brute = Vector3.zero;

            BodySeparation.Result result = BodySeparation.Resolve(
                knight, KnightRadius, KnightMass, brute, BruteRadius, BruteMass,
                previousDirection: Vector3.right);

            Vector3 knightAfter = knight + result.PushFirst;
            Vector3 bruteAfter = brute + result.PushSecond;

            Assert.Less(knightAfter.x, bruteAfter.x, "The knight was pushed out of the far side of the brute.");
            Assert.AreEqual(KnightRadius + BruteRadius, bruteAfter.x - knightAfter.x, 0.001f);
        }

        [Test]
        public void BodiesMovedFarApartDeliberatelyAreNotYankedBack()
        {
            // A retry teleports both fighters. Being on opposite sides from last frame is expected
            // then, and must not read as having passed through each other.
            BodySeparation.Result result = BodySeparation.Resolve(
                new Vector3(10f, 0f, 0f), KnightRadius, KnightMass,
                Vector3.zero, BruteRadius, BruteMass,
                previousDirection: Vector3.right);

            Assert.IsFalse(result.Overlapped);
        }

        [Test]
        public void ExactlyCoincidentBodiesStillSeparate()
        {
            BodySeparation.Result result = BodySeparation.Resolve(
                Vector3.zero, KnightRadius, KnightMass, Vector3.zero, BruteRadius, BruteMass, Vector3.zero);

            Assert.IsTrue(result.Overlapped);
            Assert.Greater((result.PushSecond - result.PushFirst).magnitude, KnightRadius);
        }

        [Test]
        public void HeightDifferencesDoNotCreateOrHideOverlap()
        {
            BodySeparation.Result result = BodySeparation.Resolve(
                Vector3.zero, KnightRadius, KnightMass,
                new Vector3(1f, 5f, 0f), BruteRadius, BruteMass,
                Vector3.zero);

            Assert.IsTrue(result.Overlapped);
            Assert.AreEqual(0f, result.PushFirst.y, 0.0001f);
            Assert.AreEqual(0f, result.PushSecond.y, 0.0001f);
        }
    }
}
