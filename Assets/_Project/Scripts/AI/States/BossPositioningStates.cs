using AdaptiveBossArena.Core.Perception;
using AdaptiveBossArena.Core.StateMachine;
using AdaptiveBossArena.Learning;
using UnityEngine;

namespace AdaptiveBossArena.AI.States
{
    /// <summary>
    /// Waiting, before the boss has seen anything worth reacting to.
    /// </summary>
    /// <remarks>
    /// Reached at the very start of a fight, when the perception history is not yet deep enough to
    /// satisfy the observation delay. The boss genuinely does not know where the player is, and
    /// standing still is the honest response.
    /// </remarks>
    public sealed class BossIdleState : StateBase<BossContext>
    {
        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            context.Motor.Decelerate(deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Holding position at the distance the boss currently prefers, watching for an opening.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state the boss spends most of the fight in, and the one adaptation changes most visibly.
    /// Preferred range and aggression are both tuning values, so a boss that has learned the player
    /// likes to keep away will hold closer and press sooner, without any new behaviour having been
    /// added to it.
    /// </para>
    /// <para>
    /// A deliberate dead zone around the preferred distance stops the boss oscillating in and out
    /// when it is already roughly where it wants to be.
    /// </para>
    /// </remarks>
    public sealed class BossObserveState : StateBase<BossContext>
    {
        /// <summary>Tolerance around the preferred distance within which no repositioning happens.</summary>
        private const float RangeDeadZone = 0.75f;

        /// <summary>Sideways drift while observing, so the boss circles rather than standing still.</summary>
        private const float StrafeWeight = 0.45f;

        /// <inheritdoc />
        protected override void OnEnter(BossContext context)
        {
            // Entering this state means no attack is committed. Clearing here stops a choice made
            // before an interruption from being resurrected later, when the situation that justified
            // it has passed.
            context.PendingAttack = null;

            // A fresh reaction delay on entry means the boss cannot leave this state and attack in
            // the same instant it arrives, no matter how obvious the opening.
            context.BeginReactionDelay();
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            PlayerObservation observation = context.Perceived;

            if (!observation.IsValid)
            {
                context.Motor.Decelerate(deltaTime);
                context.Motor.Tick(deltaTime);
                return;
            }

            context.Motor.FaceDirection(context.DirectionToPlayer, deltaTime);
            context.Motor.MoveInDirection(ResolveMovement(context), deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <summary>Decides whether to close, back off, or circle.</summary>
        private static Vector3 ResolveMovement(BossContext context)
        {
            Vector3 toPlayer = context.DirectionToPlayer;
            float distance = context.DistanceToPlayer;
            float preferred = context.PreferredRange;

            Vector3 strafe = Vector3.Cross(Vector3.up, toPlayer) * StrafeWeight;

            if (distance > preferred + RangeDeadZone)
            {
                return toPlayer + strafe;
            }

            if (distance < preferred - RangeDeadZone)
            {
                return -toPlayer + strafe;
            }

            // Comfortable. Circling keeps the boss reading as alive and attentive rather than as a
            // turret waiting for a cooldown.
            return strafe;
        }
    }

    /// <summary>
    /// Closing the distance with intent, rather than drifting toward it.
    /// </summary>
    /// <remarks>
    /// Distinguished from observing because a boss that has decided to attack should commit to
    /// reaching the player instead of continuing to circle. How readily it makes that decision is
    /// governed by its learned aggression.
    /// </remarks>
    public sealed class BossApproachState : StateBase<BossContext>
    {
        /// <summary>True once the boss is close enough for its chosen attack to reach.</summary>
        /// <param name="context">The boss context.</param>
        /// <returns>Whether the approach has succeeded.</returns>
        public bool HasArrived(BossContext context)
        {
            if (context.PendingAttack == null)
            {
                return true;
            }

            float reach = context.PendingAttack.Range +
                          context.PendingAttack.LungeSpeed * context.PendingAttack.StartupSeconds;

            return context.DistanceToPlayer <= reach;
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            Vector3 toPlayer = context.DirectionToPlayer;

            context.Motor.FaceDirection(toPlayer, deltaTime);
            context.Motor.MoveInDirection(toPlayer * AggressionScale(context), deltaTime);
            context.Motor.Tick(deltaTime);
        }

        /// <summary>
        /// Scales approach speed by learned aggression.
        /// </summary>
        /// <remarks>
        /// Kept above a floor so an unaggressive boss still closes eventually. A boss that stopped
        /// approaching entirely would leave the fight with nothing happening in it.
        /// </remarks>
        private static float AggressionScale(BossContext context) =>
            Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(context.Tuning.Get(BossTuningParameter.Aggression)));
    }
}
