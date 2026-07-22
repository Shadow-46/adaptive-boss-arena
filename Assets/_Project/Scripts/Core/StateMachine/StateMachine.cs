using System;
using System.Collections.Generic;

namespace AdaptiveBossArena.Core.StateMachine
{
    /// <summary>
    /// Finite state machine with per-state and global transition tables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transitions live in the machine rather than inside the states, so behaviour can be rewired
    /// without touching the states themselves. That matters for the boss: a counter-strategy that
    /// starts parrying heavy attacks registers a transition at runtime, and the existing attack
    /// states are unaware anything changed.
    /// </para>
    /// <para>
    /// Global transitions are evaluated before per-state ones, so conditions that must always win,
    /// such as death or a poise break, do not need duplicating across every state.
    /// </para>
    /// <para>
    /// Ticking is allocation-free once built. The current state's transition list is cached on
    /// entry rather than looked up in a dictionary every frame.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">Data and services the machine and its states operate on.</typeparam>
    public sealed class StateMachine<TContext>
    {
        private static readonly List<ITransition<TContext>> EmptyTransitions =
            new List<ITransition<TContext>>();

        private readonly TContext _context;

        private readonly Dictionary<IState<TContext>, List<ITransition<TContext>>> _transitions =
            new Dictionary<IState<TContext>, List<ITransition<TContext>>>();

        private readonly List<ITransition<TContext>> _globalTransitions =
            new List<ITransition<TContext>>();

        private List<ITransition<TContext>> _currentTransitions = EmptyTransitions;

        /// <summary>Creates a machine and enters its initial state.</summary>
        /// <param name="context">Context handed to every state callback.</param>
        /// <param name="initialState">State to begin in.</param>
        /// <exception cref="ArgumentNullException">Thrown when no initial state is supplied.</exception>
        public StateMachine(TContext context, IState<TContext> initialState)
        {
            _context = context;
            CurrentState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            CurrentState.Enter(_context);
        }

        /// <summary>The state the machine currently occupies.</summary>
        public IState<TContext> CurrentState { get; private set; }

        /// <summary>Seconds spent in the current state, in scaled time.</summary>
        public float TimeInCurrentState { get; private set; }

        /// <summary>
        /// Raised after a transition completes, with the state left and the state entered.
        /// </summary>
        /// <remarks>Used by the debug overlay and by audio cues tied to phase changes.</remarks>
        public event Action<IState<TContext>, IState<TContext>> StateChanged;

        /// <summary>Registers a transition that applies only while a specific state is active.</summary>
        /// <param name="from">State the transition departs from.</param>
        /// <param name="transition">The transition to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is missing.</exception>
        public void AddTransition(IState<TContext> from, ITransition<TContext> transition)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition));
            }

            if (!_transitions.TryGetValue(from, out List<ITransition<TContext>> list))
            {
                list = new List<ITransition<TContext>>();
                _transitions[from] = list;
            }

            InsertByPriority(list, transition);

            // The active state's list is cached, so a transition added to it mid-fight, which is
            // exactly what adaptation does, has to refresh that cache to take effect.
            if (ReferenceEquals(from, CurrentState))
            {
                _currentTransitions = list;
            }
        }

        /// <summary>Registers a transition using a delegate condition.</summary>
        /// <param name="from">State the transition departs from.</param>
        /// <param name="to">State to move into.</param>
        /// <param name="condition">Predicate deciding whether to fire.</param>
        /// <param name="priority">Evaluation order; higher is tested first.</param>
        public void AddTransition(
            IState<TContext> from,
            IState<TContext> to,
            Func<TContext, bool> condition,
            int priority = 0) =>
            AddTransition(from, new FuncTransition<TContext>(to, condition, priority));

        /// <summary>Registers a transition evaluated regardless of the current state.</summary>
        /// <param name="transition">The transition to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when no transition is supplied.</exception>
        public void AddGlobalTransition(ITransition<TContext> transition)
        {
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition));
            }

            InsertByPriority(_globalTransitions, transition);
        }

        /// <summary>Registers a global transition using a delegate condition.</summary>
        /// <param name="to">State to move into.</param>
        /// <param name="condition">Predicate deciding whether to fire.</param>
        /// <param name="priority">Evaluation order; higher is tested first.</param>
        public void AddGlobalTransition(IState<TContext> to, Func<TContext, bool> condition, int priority = 0) =>
            AddGlobalTransition(new FuncTransition<TContext>(to, condition, priority));

        /// <summary>Evaluates transitions, then ticks the resulting state.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Tick(float deltaTime)
        {
            IState<TContext> next = ResolveTransition();
            if (next != null)
            {
                ChangeState(next);
            }

            TimeInCurrentState += deltaTime;
            CurrentState.Tick(_context, deltaTime);
        }

        /// <summary>Ticks the current state on the physics step. Transitions are not evaluated here.</summary>
        /// <param name="fixedDeltaTime">Physics step duration.</param>
        public void FixedTick(float fixedDeltaTime) => CurrentState.FixedTick(_context, fixedDeltaTime);

        /// <summary>
        /// Moves to a state immediately, bypassing transition conditions.
        /// </summary>
        /// <remarks>
        /// Reserved for authoritative events such as death or a scripted phase change. Routine
        /// behaviour must go through transitions, or the graph stops describing what the boss can do.
        /// </remarks>
        /// <param name="state">State to enter.</param>
        /// <exception cref="ArgumentNullException">Thrown when no state is supplied.</exception>
        public void ForceState(IState<TContext> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            ChangeState(state);
        }

        /// <summary>Finds the highest-priority satisfied transition, or null when none apply.</summary>
        private IState<TContext> ResolveTransition()
        {
            foreach (ITransition<TContext> transition in _globalTransitions)
            {
                if (!ReferenceEquals(transition.Target, CurrentState) && transition.IsSatisfied(_context))
                {
                    return transition.Target;
                }
            }

            foreach (ITransition<TContext> transition in _currentTransitions)
            {
                if (transition.IsSatisfied(_context))
                {
                    return transition.Target;
                }
            }

            return null;
        }

        /// <summary>Performs the exit, swap and entry, then notifies listeners.</summary>
        private void ChangeState(IState<TContext> next)
        {
            IState<TContext> previous = CurrentState;

            previous.Exit(_context);
            CurrentState = next;
            TimeInCurrentState = 0f;

            _currentTransitions = _transitions.TryGetValue(next, out List<ITransition<TContext>> list)
                ? list
                : EmptyTransitions;

            CurrentState.Enter(_context);
            StateChanged?.Invoke(previous, next);
        }

        /// <summary>Inserts a transition so the list stays ordered by descending priority.</summary>
        private static void InsertByPriority(
            List<ITransition<TContext>> list,
            ITransition<TContext> transition)
        {
            int index = 0;
            while (index < list.Count && list[index].Priority >= transition.Priority)
            {
                index++;
            }

            list.Insert(index, transition);
        }
    }
}
