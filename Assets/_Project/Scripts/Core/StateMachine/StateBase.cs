namespace AdaptiveBossArena.Core.StateMachine
{
    /// <summary>
    /// Convenience base that turns every state callback into an opt-in override.
    /// </summary>
    /// <remarks>
    /// Most states care about two or three of the five members. Deriving from this keeps concrete
    /// states down to the logic that distinguishes them, and supplies the time-in-state bookkeeping
    /// that attack timing and telegraph length both depend on.
    /// </remarks>
    /// <typeparam name="TContext">Data and services the state operates on.</typeparam>
    public abstract class StateBase<TContext> : IState<TContext>
    {
        /// <inheritdoc />
        public virtual string Name => GetType().Name;

        /// <summary>Seconds elapsed since this state was entered, in scaled time.</summary>
        public float TimeInState { get; private set; }

        /// <inheritdoc />
        public void Enter(TContext context)
        {
            TimeInState = 0f;
            OnEnter(context);
        }

        /// <inheritdoc />
        public void Tick(TContext context, float deltaTime)
        {
            TimeInState += deltaTime;
            OnTick(context, deltaTime);
        }

        /// <inheritdoc />
        public virtual void FixedTick(TContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(TContext context) => OnExit(context);

        /// <summary>Override to run entry logic.</summary>
        /// <param name="context">The machine's context.</param>
        protected virtual void OnEnter(TContext context)
        {
        }

        /// <summary>Override to run per-frame logic.</summary>
        /// <param name="context">The machine's context.</param>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        protected virtual void OnTick(TContext context, float deltaTime)
        {
        }

        /// <summary>Override to run exit logic.</summary>
        /// <param name="context">The machine's context.</param>
        protected virtual void OnExit(TContext context)
        {
        }
    }
}
