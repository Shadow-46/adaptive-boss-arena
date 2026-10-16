using System.Collections.Generic;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// What the boss carries from one attempt to the next while the game stays open: a faded copy of its read
    /// on the player and of the answers it had settled on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A player who dies and tries again is the same player, with the same habits, and a boss that met them
    /// blank every time could never feel like it knew them. So a lost fight is remembered at half strength: the
    /// boss opens the rematch half-ready against what it saw, the way a rival who beat you once expects you to
    /// do it again.
    /// </para>
    /// <para>
    /// Half, not all, so the memory never decides the fight. The counts behind every habit are halved, so a
    /// player who has changed overwrites the old read within the first half-minute, and a remembered answer
    /// lapses like any other once its habit is not seen. Nothing is written to disk: closing the game ends the
    /// rivalry, and beating the boss ends it too.
    /// </para>
    /// </remarks>
    public static class SessionMemory
    {
        /// <summary>The share of everything learned that survives into the next attempt.</summary>
        public const float RetainedShare = 0.5f;

        private static AdaptationSnapshot _snapshot;

        /// <summary>Whether the boss remembers an earlier attempt.</summary>
        public static bool HasMemory => _snapshot != null;

        /// <summary>The habits behind the answers it remembers, strongest first, for telling the player.</summary>
        public static IReadOnlyList<BehaviorFeature> RememberedHabits =>
            _snapshot != null ? _snapshot.Habits : (IReadOnlyList<BehaviorFeature>)new BehaviorFeature[0];

        /// <summary>Keeps a faded copy of what this fight taught the boss.</summary>
        /// <param name="adaptation">The boss's learning loop.</param>
        public static void Remember(AdaptationManager adaptation)
        {
            if (adaptation != null)
            {
                _snapshot = adaptation.TakeSnapshot(RetainedShare);
            }
        }

        /// <summary>Opens a new attempt with what the boss remembers, if anything.</summary>
        /// <param name="adaptation">The boss's learning loop, freshly reset.</param>
        /// <returns>True when there was a memory to recall.</returns>
        public static bool TryRecall(AdaptationManager adaptation)
        {
            if (_snapshot == null || adaptation == null)
            {
                return false;
            }

            adaptation.Recall(_snapshot);
            return true;
        }

        /// <summary>Lets go of everything remembered: the player has beaten the boss, or a test needs a clean slate.</summary>
        public static void Forget() => _snapshot = null;
    }

    /// <summary>A faded copy of the boss's learning, carried between attempts by <see cref="SessionMemory"/>.</summary>
    public sealed class AdaptationSnapshot
    {
        /// <summary>Creates a snapshot.</summary>
        /// <param name="tallies">Every faded tally, already scaled down.</param>
        /// <param name="dodgeWeights">The roll-direction histogram's bins, already scaled down.</param>
        /// <param name="strengths">The counters in force and their strengths, already scaled down.</param>
        /// <param name="habits">The habits behind those counters, strongest first.</param>
        public AdaptationSnapshot(
            float[] tallies,
            float[] dodgeWeights,
            IReadOnlyDictionary<CounterStrategy, float> strengths,
            IReadOnlyList<BehaviorFeature> habits)
        {
            Tallies = tallies;
            DodgeWeights = dodgeWeights;
            Strengths = strengths;
            Habits = habits;
        }

        /// <summary>Every faded tally.</summary>
        public float[] Tallies { get; }

        /// <summary>The roll-direction histogram's bins.</summary>
        public float[] DodgeWeights { get; }

        /// <summary>The counters in force and how hard each is applied.</summary>
        public IReadOnlyDictionary<CounterStrategy, float> Strengths { get; }

        /// <summary>The habits behind those counters, strongest first.</summary>
        public IReadOnlyList<BehaviorFeature> Habits { get; }
    }
}
