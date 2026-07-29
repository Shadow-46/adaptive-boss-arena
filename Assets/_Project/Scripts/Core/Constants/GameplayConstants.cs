namespace AdaptiveBossArena.Core.Constants
{
    /// <summary>
    /// Fixed values that are structural rather than tunable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anything a designer should be able to adjust without a recompile belongs in a ScriptableObject
    /// instead. What lives here are constants other code reasons about: the physics rate the frame
    /// data is authored against, engine-level ceilings, and defaults used when configuration is
    /// missing.
    /// </para>
    /// <para>
    /// This type exists to satisfy the project's no-magic-numbers rule at the level where a bare
    /// literal would otherwise appear in several files and drift apart.
    /// </para>
    /// </remarks>
    public static class GameplayConstants
    {
        /// <summary>Physics steps per second the project runs at.</summary>
        /// <remarks>
        /// Attack timings are authored in seconds but conceived in frames at this rate. Changing it
        /// silently re-times every attack in the game.
        /// </remarks>
        public const int PhysicsStepsPerSecond = 60;

        /// <summary>Duration of one physics step, in seconds.</summary>
        public const float FixedTimeStep = 1f / PhysicsStepsPerSecond;

        /// <summary>Duration of one frame at the reference rate, useful for expressing frame counts.</summary>
        public const float OneFrame = FixedTimeStep;

        /// <summary>
        /// How long an input is remembered and replayed once the character can act again.
        /// </summary>
        /// <remarks>
        /// Roughly nine frames. Without buffering, an attack pressed one frame before a dash recovery
        /// ends is discarded, and the player correctly perceives that as the game dropping inputs.
        /// This single value does more for perceived responsiveness than any animation work.
        /// </remarks>
        public const float InputBufferSeconds = 0.15f;

        /// <summary>
        /// Window after a boss attack becomes active during which a dash counts as a perfect dodge.
        /// </summary>
        public const float PerfectDodgeWindowSeconds = 0.12f;

        /// <summary>How long the slow-motion reward for a perfect dodge lasts, in real time.</summary>
        /// <remarks>
        /// Long enough to register and act on, short enough that a player who dodges well is not
        /// spending most of the fight in slow motion.
        /// </remarks>
        public const float PerfectDodgeSlowMotionSeconds = 0.45f;

        /// <summary>Maximum hit-stop any single hit may request, as a safety ceiling.</summary>
        public const float MaxHitStopSeconds = 0.2f;

        /// <summary>Height above the arena floor at which combatant origins sit.</summary>
        public const float GroundPlaneHeight = 0f;

        /// <summary>
        /// Number of boss phases in the encounter, including the Last Stand desperation phase.
        /// </summary>
        public const int BossPhaseCount = 4;

        /// <summary>Health fraction below which the boss enters its second phase.</summary>
        public const float BossPhaseTwoThreshold = 0.66f;

        /// <summary>Health fraction below which the boss enters its third phase.</summary>
        public const float BossPhaseThreeThreshold = 0.33f;

        /// <summary>Health fraction below which the boss enters its Last Stand desperation phase.</summary>
        public const float BossPhaseFourThreshold = 0.15f;

        /// <summary>
        /// How often the learning system recomputes the player's behaviour profile, in seconds.
        /// </summary>
        /// <remarks>
        /// Deliberately coarse. Recomputing every frame would let the boss respond to a single
        /// action, which is the reactive behaviour this design is trying to avoid. Analysis on an
        /// interval forces the boss to act on trends.
        /// </remarks>
        public const float BehaviourAnalysisIntervalSeconds = 2.5f;

        /// <summary>Shortest permitted gap between two adaptations taking effect, in seconds.</summary>
        /// <remarks>
        /// Adaptations that stack too quickly read as the boss cheating rather than learning. The
        /// player needs time to notice a change and respond to it before the next one lands.
        /// </remarks>
        public const float MinSecondsBetweenAdaptations = 12f;
    }
}
