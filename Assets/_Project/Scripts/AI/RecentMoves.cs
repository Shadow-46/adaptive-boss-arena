using AdaptiveBossArena.Combat;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// The last few blows the boss threw, so it does not throw the same one again and again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Weighted selection alone repeats itself: at a given distance one move scores best, the phase's signature
    /// and any learned answer pile on top, and the same draw comes up three and four times running. A fighter
    /// who did that would read as a machine. A human duellist mixes their blows, and leans on a favourite only
    /// as often as it keeps working.
    /// </para>
    /// <para>
    /// Every thrown blow is recorded here: openers, chained follow-ups and openings taken alike, because the
    /// player sees all of them. Each recent move has its weight cut, most for the very last one, and the same
    /// move three times in a row is ruled out whenever anything else is possible. A learned answer still
    /// surfaces far more often than its peers; it simply stops being the only thing the boss does.
    /// </para>
    /// </remarks>
    public sealed class RecentMoves
    {
        /// <summary>How many blows back the boss remembers.</summary>
        public const int Depth = 4;

        /// <summary>
        /// Weight kept by a move thrown one, two, three and four blows ago.
        /// </summary>
        /// <remarks>
        /// Steep for the last blow, so a straight repeat is rare, and gentle further back, so a good move
        /// returns within a few swings rather than vanishing for the rest of the phase.
        /// </remarks>
        private static readonly float[] KeptWeight = { 0.15f, 0.45f, 0.75f, 0.9f };

        private readonly AttackDefinition[] _moves = new AttackDefinition[Depth];
        private int _count;

        /// <summary>The blow thrown most recently, or null.</summary>
        public AttackDefinition Last => _count > 0 ? _moves[0] : null;

        /// <summary>Records a blow the boss has just begun.</summary>
        /// <param name="attack">The blow; null is ignored.</param>
        public void Record(AttackDefinition attack)
        {
            if (attack == null)
            {
                return;
            }

            for (int i = Depth - 1; i > 0; i--)
            {
                _moves[i] = _moves[i - 1];
            }

            _moves[0] = attack;
            _count = System.Math.Min(Depth, _count + 1);
        }

        /// <summary>Forgets every blow, for a new attempt.</summary>
        public void Clear()
        {
            System.Array.Clear(_moves, 0, Depth);
            _count = 0;
        }

        /// <summary>
        /// The share of its weight a move keeps for having been thrown recently.
        /// </summary>
        /// <param name="attack">The candidate move.</param>
        /// <returns>
        /// One for a move not thrown lately; zero for a move thrown twice running, which may not come a third
        /// time; otherwise the kept share for its most recent use.
        /// </returns>
        public float WeightFor(AttackDefinition attack)
        {
            if (attack == null || _count == 0)
            {
                return 1f;
            }

            if (IsThirdInARow(attack))
            {
                return 0f;
            }

            for (int i = 0; i < _count; i++)
            {
                if (_moves[i] == attack)
                {
                    return KeptWeight[i];
                }
            }

            return 1f;
        }

        /// <summary>Whether throwing this move now would make it three in a row.</summary>
        /// <param name="attack">The candidate move.</param>
        /// <returns>True when the last two blows were both this move.</returns>
        public bool IsThirdInARow(AttackDefinition attack) =>
            _count >= 2 && _moves[0] == attack && _moves[1] == attack;
    }
}
