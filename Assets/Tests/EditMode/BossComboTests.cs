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
        /// <summary>The untrained continue chance, so the bounds tests state the baseline by name.</summary>
        private const float Baseline = BossAttackState.BaseComboContinueChance;

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
            Assert.AreEqual(0, BossAttackState.ComboLength(0, new FixedBoolRandom(true), Baseline));
        }

        [Test]
        public void AlwaysContinuingChainsUpToThePhaseIndex()
        {
            Assert.AreEqual(1, BossAttackState.ComboLength(1, new FixedBoolRandom(true), Baseline));
            Assert.AreEqual(2, BossAttackState.ComboLength(2, new FixedBoolRandom(true), Baseline));
            Assert.AreEqual(3, BossAttackState.ComboLength(3, new FixedBoolRandom(true), Baseline));
        }

        [Test]
        public void NeverContinuingProducesNoExtraHits()
        {
            Assert.AreEqual(0, BossAttackState.ComboLength(3, new FixedBoolRandom(false), Baseline));
        }

        [Test]
        public void ResultStaysWithinBoundsAcrossManySeeds()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var random = new XorShiftRandomProvider(seed);
                int length = BossAttackState.ComboLength(2, random, Baseline);

                Assert.GreaterOrEqual(length, 0, $"Seed {seed} produced a negative combo length.");
                Assert.LessOrEqual(length, 2, $"Seed {seed} chained more than the phase allows.");
            }
        }

        [Test]
        public void SameSeedProducesTheSameLength()
        {
            int first = BossAttackState.ComboLength(2, new XorShiftRandomProvider(12345u), Baseline);
            int second = BossAttackState.ComboLength(2, new XorShiftRandomProvider(12345u), Baseline);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void TheLastStandPhaseStaysWithinItsBounds()
        {
            // The desperation phase (index 3) is the most dangerous, but the same fairness contract
            // holds: it may chain hard, never beyond its index, and never negatively.
            for (uint seed = 1; seed <= 200; seed++)
            {
                int length = BossAttackState.ComboLength(3, new XorShiftRandomProvider(seed), Baseline);

                Assert.GreaterOrEqual(length, 0, $"Seed {seed} produced a negative combo length.");
                Assert.LessOrEqual(length, 3, $"Seed {seed} chained more than the Last Stand allows.");
            }
        }

        [Test]
        public void ABossThatHasLearnedToPress_ChainsMoreOften()
        {
            // The behaviour ComboExtensionChance exists to produce. It was set by three strategies and
            // read by nothing, so "it strings its blows together now" was an empty claim; this pins
            // that a raised chance genuinely lengthens chains.
            int baselineTotal = 0;
            int pressingTotal = 0;

            for (uint seed = 1; seed <= 400; seed++)
            {
                baselineTotal += BossAttackState.ComboLength(
                    3, new XorShiftRandomProvider(seed), Baseline);

                pressingTotal += BossAttackState.ComboLength(
                    3, new XorShiftRandomProvider(seed), Baseline + 0.35f);
            }

            Assert.Greater(
                pressingTotal, baselineTotal,
                "A raised combo-extension chance should produce longer chains overall.");
        }

        [Test]
        public void TheContinueChanceIsClamped()
        {
            // Tuning is eased toward targets and summed with the baseline, so it can overshoot one.
            // An out-of-range chance must not change the fairness contract on chain length.
            Assert.AreEqual(2, BossAttackState.ComboLength(2, new XorShiftRandomProvider(7u), 5f));
            Assert.AreEqual(0, BossAttackState.ComboLength(2, new XorShiftRandomProvider(7u), -3f));
        }

        [Test]
        public void ALearnedDelay_MakesTheBossWaitLonger()
        {
            // The behaviour behind "It waits for your opening now." AttackDelay was written by the
            // most commonly adopted strategy and read by nothing, so the tell announced a patience
            // the boss did not have.
            float untrained = BossAttackState.AttackCooldownFor(1f, 0f, isFeint: false);
            float patient = BossAttackState.AttackCooldownFor(1f, 0.35f, isFeint: false);

            Assert.AreEqual(1f, untrained, 0.0001f);
            Assert.AreEqual(1.35f, patient, 0.0001f);
            Assert.Greater(patient, untrained);
        }

        [Test]
        public void AFeintCostsLessThanACommittedSwing_AndStillRespectsTheDelay()
        {
            float feint = BossAttackState.AttackCooldownFor(1f, 0f, isFeint: true);
            float committed = BossAttackState.AttackCooldownFor(1f, 0f, isFeint: false);

            Assert.Less(feint, committed, "A feint must recover faster than a committed swing.");

            // The learned patience applies to feints too, so a delaying boss does not give itself a
            // loophole by feinting.
            Assert.Greater(
                BossAttackState.AttackCooldownFor(1f, 0.4f, isFeint: true), feint);
        }

        [Test]
        public void ANegativeDelay_CannotSpeedTheBossUp()
        {
            // Adaptation may only ever add patience here. A negative value would let a decayed target
            // shorten the phase's own rhythm, which is not a capability the boss is meant to gain.
            Assert.AreEqual(
                1f, BossAttackState.AttackCooldownFor(1f, -2f, isFeint: false), 0.0001f);
        }
    }
}
