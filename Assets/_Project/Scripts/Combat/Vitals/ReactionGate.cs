using AdaptiveBossArena.Core.Combat;

namespace AdaptiveBossArena.Combat.Vitals
{
    /// <summary>Where a character's body currently is, as far as being knocked about is concerned.</summary>
    public enum Footing
    {
        /// <summary>On their feet, in control or at worst staggered.</summary>
        Standing = 0,

        /// <summary>Thrown and not yet landed.</summary>
        Airborne = 1,

        /// <summary>On the floor.</summary>
        Down = 2,

        /// <summary>Rising from the floor.</summary>
        GettingUp = 3
    }

    /// <summary>
    /// Decides which knockdowns and launches a character actually suffers, so none can be chained.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Knockdowns and launches take control away, and anything that takes control away can be
    /// repeated into a character that never gets it back. The project has already shipped that bug
    /// once: every hit staggered and the stagger extended itself, so a fast enough boss held the
    /// player in place indefinitely. This is the same fault with bigger consequences, so it is ruled
    /// out by construction rather than by tuning.
    /// </para>
    /// <para>
    /// The rules, each one closing a way to be held down: at most one launch per trip through the
    /// air; nothing hurts a body on the floor or getting up; and for a while after standing,
    /// knockdowns and launches arrive as plain knockback. Together they cap how long control can be
    /// lost to <see cref="WorstCaseSecondsWithoutControl"/>, however many hits land and however fast.
    /// Pure, so every rule is a test.
    /// </para>
    /// </remarks>
    public sealed class ReactionGate
    {
        private readonly float _immunitySeconds;
        private float _immuneUntil = float.NegativeInfinity;

        /// <summary>Creates a gate.</summary>
        /// <param name="immunitySecondsAfterGettingUp">
        /// How long after standing up knockdowns and launches are downgraded to knockback.
        /// </param>
        public ReactionGate(float immunitySecondsAfterGettingUp)
        {
            _immunitySeconds = immunitySecondsAfterGettingUp < 0f ? 0f : immunitySecondsAfterGettingUp;
        }

        /// <summary>Where the body currently is.</summary>
        public Footing Footing { get; private set; } = Footing.Standing;

        /// <summary>Whether the character has lost its footing and is not yet back in control.</summary>
        public bool IsReacting => Footing != Footing.Standing;

        /// <summary>Whether a hit may hurt the character at all.</summary>
        /// <remarks>
        /// False on the floor and while rising. Otherwise a body on the ground would be the easiest
        /// target in the game, and a knockdown the strongest opener rather than a breather.
        /// </remarks>
        public bool CanBeHurt => Footing == Footing.Standing || Footing == Footing.Airborne;

        /// <summary>Whether a lesser interruption, such as a stagger, may start now.</summary>
        /// <remarks>Only on their feet: a stagger mid-air or on the floor would cut the reaction short.</remarks>
        public bool AdmitsStagger => Footing == Footing.Standing;

        /// <summary>Decides what a requested reaction becomes, and moves the body into it.</summary>
        /// <param name="requested">What the hit asked for.</param>
        /// <param name="now">Current combat time, in seconds.</param>
        /// <returns>What the character suffers. May be weaker than requested, never stronger.</returns>
        public ImpactReaction Admit(ImpactReaction requested, float now)
        {
            if (!CanBeHurt)
            {
                return ImpactReaction.None;
            }

            if (requested != ImpactReaction.Knockdown && requested != ImpactReaction.Launch)
            {
                return requested;
            }

            // In the air, landing already floors them. A second launch is the juggle, and a knockdown
            // would only cut the fall short; both become a shove.
            if (Footing == Footing.Airborne || now < _immuneUntil)
            {
                return ImpactReaction.Knockback;
            }

            Footing = requested == ImpactReaction.Launch ? Footing.Airborne : Footing.Down;

            return requested;
        }

        /// <summary>The thrown body reached the ground.</summary>
        public void Land()
        {
            if (Footing == Footing.Airborne)
            {
                Footing = Footing.Down;
            }
        }

        /// <summary>The body on the floor began to rise.</summary>
        public void BeginGettingUp()
        {
            if (Footing == Footing.Down)
            {
                Footing = Footing.GettingUp;
            }
        }

        /// <summary>The character is back on their feet, and briefly cannot be floored again.</summary>
        /// <param name="now">Current combat time, in seconds.</param>
        public void FinishGettingUp(float now)
        {
            if (Footing != Footing.GettingUp)
            {
                return;
            }

            Footing = Footing.Standing;
            _immuneUntil = now + _immunitySeconds;
        }

        /// <summary>Returns to standing with no immunity, for a retry.</summary>
        public void Reset()
        {
            Footing = Footing.Standing;
            _immuneUntil = float.NegativeInfinity;
        }

        /// <summary>The longest a single knockdown or launch can keep a character from acting.</summary>
        /// <remarks>
        /// Every term is a hard limit on one stage, and the gate refuses any hit that would add a
        /// stage: no relaunch in the air, no hurt on the floor, no new knockdown after rising. So the
        /// sum is a ceiling, not an estimate.
        /// </remarks>
        /// <param name="maximumAirborneSeconds">Longest the airborne state waits for landing.</param>
        /// <param name="downSeconds">Time spent on the floor.</param>
        /// <param name="getUpSeconds">Time spent rising.</param>
        /// <returns>Seconds without control, at most.</returns>
        public static float WorstCaseSecondsWithoutControl(
            float maximumAirborneSeconds, float downSeconds, float getUpSeconds) =>
            maximumAirborneSeconds + downSeconds + getUpSeconds;
    }
}
