using System;

namespace AdaptiveBossArena.Core.StateMachine
{
    /// <summary>
    /// Transition whose condition is supplied as a delegate.
    /// </summary>
    /// <remarks>
    /// Covers the majority of cases, where a condition is a single expression over the context and
    /// does not deserve a named type. Conditions that carry tuning data or need to be authored as
    /// assets should implement <see cref="ITransition{TContext}"/> directly instead.
    /// </remarks>
    /// <typeparam name="TContext">Data and services the condition inspects.</typeparam>
    public sealed class FuncTransition<TContext> : ITransition<TContext>
    {
        private readonly Func<TContext, bool> _condition;

        /// <summary>Creates a delegate-backed transition.</summary>
        /// <param name="target">State to move into.</param>
        /// <param name="condition">Predicate deciding whether to fire.</param>
        /// <param name="priority">Evaluation order; higher is tested first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the target or condition is missing.</exception>
        public FuncTransition(IState<TContext> target, Func<TContext, bool> condition, int priority = 0)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Priority = priority;
        }

        /// <inheritdoc />
        public IState<TContext> Target { get; }

        /// <inheritdoc />
        public int Priority { get; }

        /// <inheritdoc />
        public bool IsSatisfied(TContext context) => _condition(context);
    }
}
