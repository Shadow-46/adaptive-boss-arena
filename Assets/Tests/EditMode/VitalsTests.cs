using System;
using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>Tests for the shared health pool.</summary>
    [TestFixture]
    public sealed class HealthPoolTests
    {
        private const float Tolerance = 0.0001f;

        private static DamageInfo Hit(float amount) =>
            DamageInfo.Create(amount, DamageType.BossMelee, CombatantTeam.Boss, sourceInstanceId: 7);

        [Test]
        public void Constructor_WithNonPositiveMaximum_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(0f, CombatantTeam.Player));
        }

        [Test]
        public void NewPool_StartsFull()
        {
            var health = new HealthPool(100f, CombatantTeam.Player);

            Assert.AreEqual(100f, health.Current, Tolerance);
            Assert.AreEqual(1f, health.Normalized, Tolerance);
            Assert.IsTrue(health.IsAlive);
        }

        [Test]
        public void Reduce_RemovesHealthAndReportsTheAmountTaken()
        {
            var health = new HealthPool(100f, CombatantTeam.Player);

            float applied = health.Reduce(30f, Hit(30f));

            Assert.AreEqual(30f, applied, Tolerance);
            Assert.AreEqual(70f, health.Current, Tolerance);
        }

        [Test]
        public void Reduce_ClampsAtZeroAndReportsOnlyWhatWasTaken()
        {
            var health = new HealthPool(100f, CombatantTeam.Player);

            float applied = health.Reduce(250f, Hit(250f));

            // Overkill damage must not be reported as dealt, or damage statistics and any future
            // score based on them would be inflated by the killing blow.
            Assert.AreEqual(100f, applied, Tolerance);
            Assert.AreEqual(0f, health.Current, Tolerance);
        }

        [Test]
        public void Reduce_RaisesChangedBeforeDied()
        {
            var health = new HealthPool(50f, CombatantTeam.Player);
            var order = new System.Collections.Generic.List<string>();

            health.Changed += _ => order.Add("changed");
            health.Died += _ => order.Add("died");

            health.Reduce(50f, Hit(50f));

            // A death screen reacting to Died must find the health bar already showing zero.
            CollectionAssert.AreEqual(new[] { "changed", "died" }, order);
        }

        [Test]
        public void Died_CarriesTheKillerAndTheFinalBlow()
        {
            var health = new HealthPool(10f, CombatantTeam.Player);
            DeathArgs captured = default;
            health.Died += args => captured = args;

            health.Reduce(10f, Hit(10f));

            Assert.AreEqual(CombatantTeam.Player, captured.Team);
            Assert.AreEqual(7, captured.KillerInstanceId);
            Assert.AreEqual(DamageType.BossMelee, captured.FinalBlow.Type);
        }

        [Test]
        public void Died_IsRaisedOnlyOnce()
        {
            var health = new HealthPool(10f, CombatantTeam.Player);
            int deathCount = 0;
            health.Died += _ => deathCount++;

            health.Reduce(10f, Hit(10f));
            health.Reduce(10f, Hit(10f));

            Assert.AreEqual(1, deathCount);
        }

        [Test]
        public void Heal_ClampsAtMaximum()
        {
            var health = new HealthPool(100f, CombatantTeam.Player);
            health.Reduce(20f, Hit(20f));

            float restored = health.Heal(50f);

            Assert.AreEqual(20f, restored, Tolerance);
            Assert.AreEqual(100f, health.Current, Tolerance);
        }

        [Test]
        public void Heal_OnTheDead_DoesNothing()
        {
            var health = new HealthPool(10f, CombatantTeam.Player);
            health.Reduce(10f, Hit(10f));

            // Reviving would leave listeners that already handled the death in an inconsistent state.
            Assert.AreEqual(0f, health.Heal(50f), Tolerance);
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void ResetToFull_RestoresTheStartingCondition()
        {
            var health = new HealthPool(100f, CombatantTeam.Player);
            health.Reduce(100f, Hit(100f));

            health.ResetToFull();

            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(1f, health.Normalized, Tolerance);
        }
    }

    /// <summary>
    /// Tests for the stamina pool.
    /// </summary>
    /// <remarks>
    /// The regeneration delay is what turns stamina from a speed limit into a commitment, so the
    /// tests concentrate on the pause rather than on the arithmetic.
    /// </remarks>
    [TestFixture]
    public sealed class StaminaPoolTests
    {
        private const float Tolerance = 0.001f;
        private const float Frame = 1f / 60f;

        [Test]
        public void TrySpend_WithEnoughStamina_Succeeds()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 20f, regenDelaySeconds: 0.5f);

            Assert.IsTrue(stamina.TrySpend(25f));
            Assert.AreEqual(75f, stamina.Current, Tolerance);
        }

        [Test]
        public void TrySpend_WithoutEnoughStamina_FailsAndChangesNothing()
        {
            var stamina = new StaminaPool(20f, regenPerSecond: 20f, regenDelaySeconds: 0.5f);

            Assert.IsFalse(stamina.TrySpend(25f));
            Assert.AreEqual(20f, stamina.Current, Tolerance);
        }

        [Test]
        public void AfterSpending_RegenerationIsBlockedForTheDelay()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 50f, regenDelaySeconds: 0.5f);
            stamina.TrySpend(50f);

            // Well inside the delay, so nothing should have come back yet.
            for (int i = 0; i < 12; i++)
            {
                stamina.Tick(Frame);
            }

            Assert.IsTrue(stamina.IsRegenerationBlocked);
            Assert.AreEqual(50f, stamina.Current, Tolerance);
        }

        [Test]
        public void AfterTheDelay_RegenerationResumes()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 50f, regenDelaySeconds: 0.2f);
            stamina.TrySpend(50f);

            for (int i = 0; i < 60; i++)
            {
                stamina.Tick(Frame);
            }

            Assert.IsFalse(stamina.IsRegenerationBlocked);
            Assert.Greater(stamina.Current, 50f);
        }

        [Test]
        public void SpendingAgainMidRegeneration_RestartsTheDelay()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 50f, regenDelaySeconds: 0.3f);

            stamina.TrySpend(20f);
            for (int i = 0; i < 30; i++)
            {
                stamina.Tick(Frame);
            }

            float beforeSecondSpend = stamina.Current;
            stamina.TrySpend(20f);

            for (int i = 0; i < 6; i++)
            {
                stamina.Tick(Frame);
            }

            // Repeated dashing must not creep back to full; each spend restarts the pause.
            Assert.IsTrue(stamina.IsRegenerationBlocked);
            Assert.AreEqual(beforeSecondSpend - 20f, stamina.Current, Tolerance);
        }

        [Test]
        public void Regeneration_ClampsAtMaximum()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 500f, regenDelaySeconds: 0f);
            stamina.TrySpend(10f);

            for (int i = 0; i < 60; i++)
            {
                stamina.Tick(Frame);
            }

            Assert.AreEqual(100f, stamina.Current, Tolerance);
            Assert.AreEqual(1f, stamina.Normalized, Tolerance);
        }

        [Test]
        public void CanSpend_MatchesWhatTrySpendWillDo()
        {
            var stamina = new StaminaPool(30f, regenPerSecond: 10f, regenDelaySeconds: 0.1f);

            Assert.IsTrue(stamina.CanSpend(30f));
            Assert.IsFalse(stamina.CanSpend(31f));
            Assert.IsFalse(stamina.TrySpend(31f));
        }

        [Test]
        public void ResetToFull_ClearsThePendingDelay()
        {
            var stamina = new StaminaPool(100f, regenPerSecond: 20f, regenDelaySeconds: 5f);
            stamina.TrySpend(50f);

            stamina.ResetToFull();

            Assert.IsFalse(stamina.IsRegenerationBlocked);
            Assert.AreEqual(100f, stamina.Current, Tolerance);
        }
    }
}
