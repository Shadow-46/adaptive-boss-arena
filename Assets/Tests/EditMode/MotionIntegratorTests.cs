using AdaptiveBossArena.Combat.Movement;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the arithmetic that lets a blow move someone who is trying to move somewhere else.
    /// </summary>
    /// <remarks>
    /// Knockback used to be a velocity overwrite that the victim's own movement replaced on the next
    /// frame, and the boss never applied it at all. These pin the three properties that make a shove
    /// real: it survives steering, it fades at a rate independent of frame rate, and gravity grounds
    /// rather than accumulates.
    /// </remarks>
    [TestFixture]
    public sealed class MotionIntegratorTests
    {
        private const float Gravity = 25f;
        private const float Grounded = -2f;
        private const float Frame = 1f / 60f;
        private const float HalfLife = 0.1f;

        [Test]
        public void AShoveMovesItsVictimOnTheFrameItLands()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(8f, 0f, 0f));

            Vector3 velocity = motion.Step(Vector3.zero, HalfLife, true, Frame);

            Assert.AreEqual(8f, velocity.x, 0.001f);
        }

        [Test]
        public void AShoveSurvivesTheVictimSteeringTheOtherWay()
        {
            // The bug, exactly: steering writes its own velocity every frame. The shove must add to it,
            // not be replaced by it.
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(8f, 0f, 0f));

            Vector3 velocity = motion.Step(new Vector3(-7f, 0f, 0f), HalfLife, true, Frame);

            Assert.AreEqual(1f, velocity.x, 0.001f, "Steering replaced the shove instead of adding to it.");
        }

        [Test]
        public void AShoveLosesHalfItsSpeedAfterOneHalfLife()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(8f, 0f, 0f));

            int frames = Mathf.RoundToInt(HalfLife / Frame);

            for (int i = 0; i < frames; i++)
            {
                motion.Step(Vector3.zero, HalfLife, true, Frame);
            }

            Assert.AreEqual(4f, motion.Impulse.x, 0.05f);
        }

        [Test]
        public void TheSameShoveTravelsTheSameDistanceAtAnyFrameRate()
        {
            // A knockback that carried further at 144 Hz than at 30 Hz would make the same hit land a
            // different fight on different machines.
            float distanceAt30 = DistanceTravelled(1f / 30f);
            float distanceAt144 = DistanceTravelled(1f / 144f);

            Assert.AreEqual(distanceAt30, distanceAt144, distanceAt144 * 0.15f);
        }

        [Test]
        public void TwoBlowsTogetherPushFurtherThanOne()
        {
            var one = new MotionIntegrator(Gravity, Grounded);
            one.AddImpulse(new Vector3(5f, 0f, 0f));

            var two = new MotionIntegrator(Gravity, Grounded);
            two.AddImpulse(new Vector3(5f, 0f, 0f));
            two.AddImpulse(new Vector3(5f, 0f, 0f));

            Assert.Greater(two.Impulse.x, one.Impulse.x);
        }

        [Test]
        public void AShoveNeverLiftsTheCharacter()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(0f, 50f, 3f));

            Vector3 velocity = motion.Step(Vector3.zero, HalfLife, true, Frame);

            Assert.AreEqual(Grounded, velocity.y, 0.001f, "A horizontal shove launched the character.");
        }

        [Test]
        public void StandingStillNeverBuildsUpFallingSpeed()
        {
            // Grounded characters hold the contact speed. Accumulated gravity would throw a character
            // that stepped off an edge after a minute standing at a speed it never fell to.
            var motion = new MotionIntegrator(Gravity, Grounded);

            for (int i = 0; i < 600; i++)
            {
                motion.Step(Vector3.zero, HalfLife, true, Frame);
            }

            Assert.AreEqual(Grounded, motion.VerticalVelocity, 0.001f);
        }

        [Test]
        public void OffTheGroundGravityAccelerates()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);

            motion.Step(Vector3.zero, HalfLife, false, 0.1f);

            Assert.AreEqual(Grounded - Gravity * 0.1f, motion.VerticalVelocity, 0.001f);
        }

        [Test]
        public void ResetClearsEveryImposedMotion()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(9f, 0f, 9f));
            motion.Step(Vector3.zero, HalfLife, false, 0.5f);

            motion.Reset();

            Assert.AreEqual(Vector3.zero, motion.Impulse);
            Assert.AreEqual(Grounded, motion.VerticalVelocity, 0.001f);
        }

        private static float DistanceTravelled(float frame)
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(10f, 0f, 0f));

            float distance = 0f;

            for (float t = 0f; t < 1f; t += frame)
            {
                distance += motion.Step(Vector3.zero, HalfLife, true, frame).x * frame;
            }

            return distance;
        }

        [Test]
        public void ALaunchRisesAndComesBackDown()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.Launch(9f);

            float height = 0f, peak = 0f, airtime = 0f;

            // Airborne from the first step: the ground the body left must not cancel the launch.
            do
            {
                height += motion.Step(Vector3.zero, HalfLife, height <= 0f && airtime > 0f, Frame).y * Frame;
                peak = Mathf.Max(peak, height);
                airtime += Frame;
            }
            while (height > 0f && airtime < 5f);

            // v^2 / 2g and 2v / g, within a frame's worth of integration error.
            Assert.AreEqual(9f * 9f / (2f * Gravity), peak, 0.1f);
            Assert.AreEqual(2f * 9f / Gravity, airtime, 0.05f);
        }

        [Test]
        public void AWallStopsTheShoveItWasCarrying()
        {
            var motion = new MotionIntegrator(Gravity, Grounded);
            motion.AddImpulse(new Vector3(8f, 0f, 0f));

            motion.StopImpulse();

            Assert.AreEqual(0f, motion.Step(Vector3.zero, HalfLife, true, Frame).x, 0.001f);
        }
    }
}
