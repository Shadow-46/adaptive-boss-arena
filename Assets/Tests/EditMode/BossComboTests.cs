using AdaptiveBossArena.AI.States;
using AdaptiveBossArena.Core.Services;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for how the boss decides the length of an attack combo.
    /// </summary>
    /// <remarks>
    /// The combo length is drawn from the seeded random provider, and the whole encounter's promise
    /// of being reproducible from a seed rests on decisions like this being deterministic. It is also
    /// a fairness contract: the opening phase must never chain, and no phase may chain more hits than
    /// its index, or the escalation the design describes would not hold.
    /// </remarks>
    [TestFixture]
    public sealed class BossComboTests
    {
        /// <summary>A stub whose coin-flips are fixed, for testing the bounds independent of a seed.</summary>
        private sealed class FixedBoolRandom : IRandomProvider
        {
            private readonly bool _value;

            public FixedBoolRandom(bool value) => _value = value;

            public uint Seed => 0u;

            public float NextFloat01() => 0f;

            public float NextFloat(float min, float max) => min;

            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

            public bool NextBool(float probability = 0.5f) => _value;

            public Vector2 NextDirectionOnPlane() => Vector2.right;

            public void Reseed(uint seed) { }
        }

        [Test]
        public void OpeningPhaseNeverChains()
        {
            // Even with a source that always says "continue", phase zero throws a single strike.
            Assert.AreEqual(0, BossAttackState.ComboLength(0, new FixedBoolRandom(true)));
        }

        [Test]
        public void AlwaysContinuingChainsUpToThePhaseIndex()
        {
            Assert.AreEqual(1, BossAttackState.ComboLength(1, new FixedBoolRandom(true)));
            Assert.AreEqual(2, BossAttackState.ComboLength(2, new FixedBoolRandom(true)));
            Assert.AreEqual(3, BossAttackState.ComboLength(3, new FixedBoolRandom(true)));
        }

        [Test]
        public void NeverContinuingProducesNoExtraHits()
        {
            Assert.AreEqual(0, BossAttackState.ComboLength(3, new FixedBoolRandom(false)));
        }

        [Test]
        public void ResultStaysWithinBoundsAcrossManySeeds()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var random = new XorShiftRandomProvider(seed);
                int length = BossAttackState.ComboLength(2, random);

                Assert.GreaterOrEqual(length, 0, $"Seed {seed} produced a negative combo length.");
                Assert.LessOrEqual(length, 2, $"Seed {seed} chained more than the phase allows.");
            }
        }

        [Test]
        public void SameSeedProducesTheSameLength()
        {
            int first = BossAttackState.ComboLength(2, new XorShiftRandomProvider(12345u));
            int second = BossAttackState.ComboLength(2, new XorShiftRandomProvider(12345u));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void TheLastStandPhaseStaysWithinItsBounds()
        {
            // The desperation phase (index 3) is the most dangerous, but the same fairness contract
            // holds: it may chain hard, never beyond its index, and never negatively.
            for (uint seed = 1; seed <= 200; seed++)
            {
                int length = BossAttackState.ComboLength(3, new XorShiftRandomProvider(seed));

                Assert.GreaterOrEqual(length, 0, $"Seed {seed} produced a negative combo length.");
                Assert.LessOrEqual(length, 3, $"Seed {seed} chained more than the Last Stand allows.");
            }
        }
    }
}
