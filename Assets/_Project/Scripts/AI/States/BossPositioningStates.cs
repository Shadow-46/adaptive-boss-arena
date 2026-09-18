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

            // A slow, circling stalk that eases between closing and giving ground; see BossPacing.
            context.Motor.MoveInDirection(
                BossPacing.Stalk(
                    context.DirectionToPlayer, context.DistanceToPlayer, context.PreferredRange,
                    context.Config.StalkSpeedFraction),
                deltaTime);

            context.Motor.Tick(deltaTime);
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

            float reach = context.PendingAttack.EffectiveReach;

            return context.DistanceToPlayer <= reach;
        }

        /// <inheritdoc />
        protected override void OnTick(BossContext context, float deltaTime)
        {
            Vector3 toPlayer = context.DirectionToPlayer;

            context.Motor.FaceDirection(toPlayer, deltaTime);

            // Walks in when the blow is nearly in reach and runs only across real distance, scaled by learned
            // aggression; see BossPacing.
            float reach = context.PendingAttack != null ? context.PendingAttack.EffectiveReach : 0f;
            float speed = BossPacing.ApproachSpeed(
                context.DistanceToPlayer - reach,
                context.Config.ApproachRunDistance,
                context.Config.ApproachWalkFraction,
                context.Tuning.Get(BossTuningParameter.Aggression));

            context.Motor.MoveInDirection(toPlayer.normalized * speed, deltaTime);
            context.Motor.Tick(deltaTime);
        }
    }
}
