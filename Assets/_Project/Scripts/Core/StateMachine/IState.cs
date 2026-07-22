namespace AdaptiveBossArena.Core.StateMachine
{
    /// <summary>
    /// One discrete behaviour a machine can occupy.
    /// </summary>
    /// <remarks>
    /// States are plain C# objects rather than components, so a machine can be built and stepped
    /// entirely in an edit-mode test. The context is supplied on every call instead of being captured
    /// at construction, which keeps states free of per-instance mutable wiring and makes them safe to
    /// share.
    /// </remarks>
    /// <typeparam name="TContext">Data and services the state operates on.</typeparam>
    public interface IState<in TContext>
    {
        /// <summary>Human-readable name, surfaced in the debug overlay.</summary>
        string Name { get; }

        /// <summary>Called once when the machine enters this state.</summary>
        /// <param name="context">The machine's context.</param>
        void Enter(TContext context);

        /// <summary>Called once per frame while this state is active.</summary>
        /// <param name="context">The machine's context.</param>
        /// <param name="deltaTime">Elapsed time, already scaled by the time service.</param>
        void Tick(TContext context, float deltaTime);

        /// <summary>Called on the physics step while this state is active.</summary>
        /// <param name="context">The machine's context.</param>
        /// <param name="fixedDeltaTime">Physics step duration.</param>
        void FixedTick(TContext context, float fixedDeltaTime);

        /// <summary>Called once when the machine leaves this state.</summary>
        /// <param name="context">The machine's context.</param>
        void Exit(TContext context);
    }

    /// <summary>
    /// A condition that moves the machine from its current state to a target state.
    /// </summary>
    /// <remarks>
    /// Transitions are first-class objects rather than branches inside the states themselves. That
    /// is what keeps the boss's behaviour graph modular: adding a counter-attack response to a new
    /// situation means registering a transition, not editing every state that might need to yield
    /// to it.
    /// </remarks>
    /// <typeparam name="TContext">Data and services the condition inspects.</typeparam>
    public interface ITransition<TContext>
    {
        /// <summary>The state to move into when this transition fires.</summary>
        IState<TContext> Target { get; }

        /// <summary>
        /// Evaluation order. Higher values are tested first.
        /// </summary>
        /// <remarks>
        /// Priority is how a death or stagger transition reliably beats a routine attack transition
        /// that happens to be satisfied on the same frame.
        /// </remarks>
        int Priority { get; }

        /// <summary>Tests whether this transition should fire.</summary>
        /// <param name="context">The machine's context.</param>
        /// <returns>True to move to <see cref="Target"/>.</returns>
        bool IsSatisfied(TContext context);
    }
}
