using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Perception;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rule mapping a change in visible state to a skeletal animation trigger.
    /// </summary>
    /// <remarks>
    /// The character-art seam is dormant, so this is the only part of it that can be exercised
    /// without a rig — and it holds the one subtle rule (a new combo swing must re-fire even though
    /// the state never changed) that would otherwise only be discovered once real clips were attached.
    /// </remarks>
    [TestFixture]
    public sealed class AnimatorDriveMapTests
    {
        private static AnimatorDriveTrigger Trigger(
            ObservableActionState from,
            ObservableActionState to,
            AttackPhase fromPhase = AttackPhase.Inactive,
            AttackPhase toPhase = AttackPhase.Startup) =>
            AnimatorDriveMap.TriggerFor(from, to, fromPhase, toPhase);

        [Test]
        public void EnteringEachActionFiresItsTrigger()
        {
            Assert.AreEqual(AnimatorDriveTrigger.LightAttack,
                Trigger(ObservableActionState.Idle, ObservableActionState.LightAttacking));
            Assert.AreEqual(AnimatorDriveTrigger.HeavyAttack,
                Trigger(ObservableActionState.Idle, ObservableActionState.HeavyAttacking));
            Assert.AreEqual(AnimatorDriveTrigger.Ability,
                Trigger(ObservableActionState.Idle, ObservableActionState.UsingAbility));
            Assert.AreEqual(AnimatorDriveTrigger.Dash,
                Trigger(ObservableActionState.Moving, ObservableActionState.Dashing,
                    AttackPhase.Inactive, AttackPhase.Inactive));
            Assert.AreEqual(AnimatorDriveTrigger.Guard,
                Trigger(ObservableActionState.Idle, ObservableActionState.Guarding,
                    AttackPhase.Inactive, AttackPhase.Inactive));
            Assert.AreEqual(AnimatorDriveTrigger.Death,
                Trigger(ObservableActionState.Staggered, ObservableActionState.Dead,
                    AttackPhase.Inactive, AttackPhase.Inactive));
        }

        [Test]
        public void HoldingAStateFiresNothing()
        {
            Assert.AreEqual(AnimatorDriveTrigger.None,
                Trigger(ObservableActionState.Moving, ObservableActionState.Moving,
                    AttackPhase.Inactive, AttackPhase.Inactive));
            Assert.AreEqual(AnimatorDriveTrigger.None,
                Trigger(ObservableActionState.Guarding, ObservableActionState.Guarding,
                    AttackPhase.Inactive, AttackPhase.Inactive));
        }

        [Test]
        public void ANewComboSwingReFiresEvenWhileStillAttacking()
        {
            // State stays LightAttacking across the chain, but the timeline restarts at Startup — the
            // second swing must still animate.
            Assert.AreEqual(AnimatorDriveTrigger.LightAttack,
                Trigger(ObservableActionState.LightAttacking, ObservableActionState.LightAttacking,
                    AttackPhase.Recovery, AttackPhase.Startup));
        }

        [Test]
        public void StayingInAnAttackWithoutRestartingDoesNotReFire()
        {
            Assert.AreEqual(AnimatorDriveTrigger.None,
                Trigger(ObservableActionState.LightAttacking, ObservableActionState.LightAttacking,
                    AttackPhase.Startup, AttackPhase.Active));
        }

        [Test]
        public void MovementStatesNeverReFireOnPhaseNoise()
        {
            // A dash carries no swing, so a stray phase value must not manufacture a repeated trigger.
            Assert.AreEqual(AnimatorDriveTrigger.None,
                Trigger(ObservableActionState.Dashing, ObservableActionState.Dashing,
                    AttackPhase.Recovery, AttackPhase.Startup));
        }
    }
}
