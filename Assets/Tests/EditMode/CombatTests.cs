using System.Collections.Generic;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Vitals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the attack timeline that stands in for animation events.
    /// </summary>
    /// <remarks>
    /// Frame data is the substance of an action game's feel, so it is asserted directly rather than
    /// judged by eye. The phase-skipping behaviour matters most: a long frame must never leave an
    /// attack stuck with its hitbox open, and it must never silently pass over the active window
    /// without opening it at all.
    /// </remarks>
    [TestFixture]
    public sealed class AttackTimelineTests
    {
        private const float Startup = 0.2f;
        private const float Active = 0.1f;
        private const float Recovery = 0.3f;

        private AttackDefinition _attack;

        [SetUp]
        public void CreateAttack()
        {
            _attack = ScriptableObject.CreateInstance<AttackDefinition>();

            var serialized = new SerializedObject(_attack);
            serialized.FindProperty("_startupSeconds").floatValue = Startup;
            serialized.FindProperty("_activeSeconds").floatValue = Active;
            serialized.FindProperty("_recoverySeconds").floatValue = Recovery;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyAttack() => Object.DestroyImmediate(_attack);

        [Test]
        public void BeforeBeginning_IsInactive()
        {
            var timeline = new AttackTimeline();

            Assert.AreEqual(AttackPhase.Inactive, timeline.Phase);
            Assert.IsFalse(timeline.IsRunning);
        }

        [Test]
        public void Begin_EntersStartup()
        {
            var timeline = new AttackTimeline();
            timeline.Begin(_attack);

            Assert.AreEqual(AttackPhase.Startup, timeline.Phase);
            Assert.IsTrue(timeline.IsRunning);
        }

        [Test]
        public void Timeline_PassesThroughEveryPhaseInOrder()
        {
            var timeline = new AttackTimeline();
            var observed = new List<AttackPhase>();
            timeline.PhaseChanged += observed.Add;

            timeline.Begin(_attack);
            for (int i = 0; i < 60; i++)
            {
                timeline.Tick(0.02f);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    AttackPhase.Startup,
                    AttackPhase.Active,
                    AttackPhase.Recovery,
                    AttackPhase.Inactive
                },
                observed);
        }

        [Test]
        public void HitboxOpensOnlyAfterStartup()
        {
            var timeline = new AttackTimeline();
            timeline.Begin(_attack);

            timeline.Tick(Startup * 0.5f);
            Assert.AreEqual(AttackPhase.Startup, timeline.Phase);

            timeline.Tick(Startup * 0.6f);
            Assert.AreEqual(AttackPhase.Active, timeline.Phase);
        }

        [Test]
        public void ASingleLongFrame_StillReportsTheActiveWindow()
        {
            var timeline = new AttackTimeline();
            var observed = new List<AttackPhase>();
            timeline.PhaseChanged += observed.Add;

            timeline.Begin(_attack);

            // One frame long enough to swallow the entire attack. A hitch must not cause the active
            // window to be skipped without ever having been announced.
            timeline.Tick(5f);

            CollectionAssert.Contains(observed, AttackPhase.Active);
            Assert.AreEqual(AttackPhase.Inactive, timeline.Phase);
        }

        [Test]
        public void Completed_IsRaisedWhenTheAttackFinishes()
        {
            var timeline = new AttackTimeline();
            int completedCount = 0;
            timeline.Completed += () => completedCount++;

            timeline.Begin(_attack);
            for (int i = 0; i < 60; i++)
            {
                timeline.Tick(0.02f);
            }

            Assert.AreEqual(1, completedCount);
            Assert.IsFalse(timeline.IsRunning);
        }

        [Test]
        public void Cancel_StopsTheAttackWithoutCompletingIt()
        {
            var timeline = new AttackTimeline();
            int completedCount = 0;
            timeline.Completed += () => completedCount++;

            timeline.Begin(_attack);
            timeline.Tick(Startup + Active * 0.5f);
            timeline.Cancel();

            Assert.IsFalse(timeline.IsRunning);

            // A cancelled attack did not finish. Anything waiting on completion to hand control back
            // must be able to tell the two apart.
            Assert.AreEqual(0, completedCount);
        }

        [Test]
        public void ComboWindow_OpensOnlyDuringRecovery()
        {
            var timeline = new AttackTimeline();
            timeline.Begin(_attack);

            timeline.Tick(Startup * 0.5f);
            Assert.IsFalse(timeline.IsInComboWindow, "A combo must not chain before the hit resolves.");

            timeline.Tick(Startup * 0.6f + Active + Recovery * 0.4f);
            Assert.IsTrue(timeline.IsInComboWindow);
        }

        [Test]
        public void NormalizedTime_RunsFromZeroToOne()
        {
            var timeline = new AttackTimeline();
            timeline.Begin(_attack);

            Assert.AreEqual(0f, timeline.NormalizedTime, 0.001f);

            timeline.Tick(_attack.TotalSeconds * 0.5f);
            Assert.AreEqual(0.5f, timeline.NormalizedTime, 0.05f);
        }
    }

    /// <summary>
    /// Tests for poise, the pool that decides whether a hit interrupts.
    /// </summary>
    /// <remarks>
    /// The rule worth protecting is that a break cannot chain into another break. Without it, a
    /// combo landing on a staggered target removes them from the fight entirely, which is the
    /// difference between a demanding game and an unfair one.
    /// </remarks>
    [TestFixture]
    public sealed class PoisePoolTests
    {
        private const float Frame = 1f / 60f;
        private const float Tolerance = 0.001f;

        [Test]
        public void PartialPoiseDamage_DoesNotBreak()
        {
            var poise = new PoisePool(100f, regenPerSecond: 20f, breakRecoverySeconds: 0.5f);

            Assert.IsFalse(poise.ApplyPoiseDamage(40f));
            Assert.IsFalse(poise.IsBroken);
            Assert.AreEqual(60f, poise.Current, Tolerance);
        }

        [Test]
        public void AccumulatedPoiseDamage_Breaks()
        {
            var poise = new PoisePool(100f, regenPerSecond: 20f, breakRecoverySeconds: 0.5f);
            int breakCount = 0;
            poise.Broken += () => breakCount++;

            poise.ApplyPoiseDamage(60f);

            Assert.IsTrue(poise.ApplyPoiseDamage(40f));
            Assert.IsTrue(poise.IsBroken);
            Assert.AreEqual(1, breakCount);
        }

        [Test]
        public void PoiseDamageDuringABreak_IsDiscarded()
        {
            var poise = new PoisePool(50f, regenPerSecond: 20f, breakRecoverySeconds: 1f);
            poise.ApplyPoiseDamage(50f);

            // Landing a combo on a staggered target must not chain into a second break.
            Assert.IsFalse(poise.ApplyPoiseDamage(500f));
            Assert.IsTrue(poise.IsBroken);
        }

        [Test]
        public void AfterTheBreakRecovers_PoiseReturnsFull()
        {
            var poise = new PoisePool(50f, regenPerSecond: 5f, breakRecoverySeconds: 0.2f);
            poise.ApplyPoiseDamage(50f);

            for (int i = 0; i < 30; i++)
            {
                poise.Tick(Frame);
            }

            // Rebuilding from zero would leave a recovered combatant re-breakable by a single light
            // hit, which reads as the stagger never really ending.
            Assert.IsFalse(poise.IsBroken);
            Assert.AreEqual(1f, poise.Normalized, Tolerance);
        }

        [Test]
        public void PoiseRegenerates_SoChipDamageNeverStaggers()
        {
            var poise = new PoisePool(100f, regenPerSecond: 50f, breakRecoverySeconds: 0.5f);

            // A light poke every half second against fifty-per-second regeneration should never
            // accumulate toward a break.
            for (int poke = 0; poke < 10; poke++)
            {
                Assert.IsFalse(poise.ApplyPoiseDamage(10f));

                for (int i = 0; i < 30; i++)
                {
                    poise.Tick(Frame);
                }
            }

            Assert.IsFalse(poise.IsBroken);
            Assert.AreEqual(1f, poise.Normalized, 0.01f);
        }

        [Test]
        public void ResetToFull_ClearsABreak()
        {
            var poise = new PoisePool(50f, regenPerSecond: 5f, breakRecoverySeconds: 10f);
            poise.ApplyPoiseDamage(50f);

            poise.ResetToFull();

            Assert.IsFalse(poise.IsBroken);
            Assert.AreEqual(1f, poise.Normalized, Tolerance);
        }
    }
}
