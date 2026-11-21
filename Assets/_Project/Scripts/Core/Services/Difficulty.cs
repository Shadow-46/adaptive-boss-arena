namespace AdaptiveBossArena.Core.Services
{
    /// <summary>How hard the fight is, chosen on the title screen.</summary>
    /// <remarks>
    /// Knight is the zero value on purpose. <c>JsonUtility</c> leaves a field it cannot find at its
    /// default, so a run-modifier save written before difficulty existed loads as the souls-hard fight
    /// the game was tuned for, never as Story.
    /// </remarks>
    public enum Difficulty
    {
        /// <summary>The fight as designed: souls-hard, a three-to-five-minute win.</summary>
        Knight = 0,

        /// <summary>For learning the fight: he hits softer, waits longer and reads you slower.</summary>
        Story = 1,

        /// <summary>For players who have beaten Knight: he hits harder, rests less and reads you sooner.</summary>
        Nightmare = 2,
    }

    /// <summary>The numbers one difficulty changes, all multipliers on values the fight already uses.</summary>
    /// <remarks>
    /// <para>
    /// Difficulty follows the rule adaptation follows: numbers, never rules. Each field scales a value an
    /// existing system already respects, so a Nightmare Igris is the same boss with the same moves, the
    /// same telegraphs and the same perception delay. He is never given a capability, and in particular
    /// he never reacts faster than a human could: perception latency is deliberately not on this list.
    /// </para>
    /// <para>
    /// Knight is the identity profile, every multiplier one and no extra flasks, so the default fight is
    /// exactly the fight that was balanced. A test pins that.
    /// </para>
    /// </remarks>
    public readonly struct DifficultyProfile
    {
        /// <summary>Creates a profile.</summary>
        /// <param name="difficulty">The difficulty this describes.</param>
        /// <param name="incomingDamageScale">Multiplier on damage the knight takes.</param>
        /// <param name="bossDamageTakenScale">Multiplier on damage Igris takes; below one makes him last longer.</param>
        /// <param name="bossCooldownScale">Multiplier on Igris's rest between swings; below one presses harder.</param>
        /// <param name="adaptationRateScale">Multiplier on how fast Igris learns.</param>
        /// <param name="healChargeBonus">Flasks added to (or removed from) the knight's usual count.</param>
        public DifficultyProfile(
            Difficulty difficulty,
            float incomingDamageScale,
            float bossDamageTakenScale,
            float bossCooldownScale,
            float adaptationRateScale,
            int healChargeBonus)
        {
            Difficulty = difficulty;
            IncomingDamageScale = incomingDamageScale;
            BossDamageTakenScale = bossDamageTakenScale;
            BossCooldownScale = bossCooldownScale;
            AdaptationRateScale = adaptationRateScale;
            HealChargeBonus = healChargeBonus;
        }

        /// <summary>The difficulty this profile describes.</summary>
        public Difficulty Difficulty { get; }

        /// <summary>Multiplier on damage the knight takes.</summary>
        public float IncomingDamageScale { get; }

        /// <summary>Multiplier on damage Igris takes. Below one makes him last longer.</summary>
        public float BossDamageTakenScale { get; }

        /// <summary>Multiplier on Igris's rest between swings. Below one presses harder.</summary>
        public float BossCooldownScale { get; }

        /// <summary>Multiplier on how fast Igris learns the knight's habits.</summary>
        public float AdaptationRateScale { get; }

        /// <summary>Flasks added to, or taken from, the knight's usual count.</summary>
        public int HealChargeBonus { get; }

        /// <summary>The Story profile.</summary>
        /// <remarks>
        /// Half damage and two more flasks make a mistake survivable while the fight is still being
        /// learned. Igris rests a third longer between swings and learns at a little over half speed, so
        /// the tells have time to be read and a habit takes longer to be punished. He takes a third more
        /// damage, so a learning fight still ends in about the time a Knight win does.
        /// </remarks>
        public static readonly DifficultyProfile Story = new DifficultyProfile(
            Difficulty.Story, 0.5f, 1.35f, 1.35f, 0.6f, 2);

        /// <summary>The Knight profile: the fight as balanced. Every value is the identity.</summary>
        public static readonly DifficultyProfile Knight = new DifficultyProfile(
            Difficulty.Knight, 1f, 1f, 1f, 1f, 0);

        /// <summary>The Nightmare profile.</summary>
        /// <remarks>
        /// Each step is modest because the steps compound. Thirty percent more damage, fifteen percent less
        /// rest and fifteen percent more health to cut through already make the fight about half again as
        /// long and far less forgiving. One flask fewer makes every heal a decision. Learning half again as
        /// fast is the Nightmare signature: repeat yourself and he has you.
        /// </remarks>
        public static readonly DifficultyProfile Nightmare = new DifficultyProfile(
            Difficulty.Nightmare, 1.3f, 0.85f, 0.85f, 1.5f, -1);

        /// <summary>The profile for a difficulty.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <returns>Its profile. An unknown value reads as Knight, the fight as balanced.</returns>
        public static DifficultyProfile For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Story:
                    return Story;
                case Difficulty.Nightmare:
                    return Nightmare;
                default:
                    return Knight;
            }
        }

        /// <summary>The name shown on the title screen and in the records.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <returns>Its display name.</returns>
        public static string Label(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Story:
                    return "Story";
                case Difficulty.Nightmare:
                    return "Nightmare";
                default:
                    return "Knight";
            }
        }

        /// <summary>A one-line description for the title screen.</summary>
        /// <param name="difficulty">The difficulty.</param>
        /// <returns>What the difficulty changes, in the player's terms.</returns>
        public static string Blurb(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Story:
                    return "Learn the fight. He hits softer, rests longer and reads you slower.";
                case Difficulty.Nightmare:
                    return "He hits harder, rests less and reads you sooner. One flask fewer.";
                default:
                    return "The fight as it was forged. Souls-hard.";
            }
        }

        /// <summary>The difficulty after this one in the title screen's cycle: Story, Knight, Nightmare.</summary>
        /// <param name="difficulty">The current difficulty.</param>
        /// <returns>The next one, wrapping from Nightmare back to Story.</returns>
        public static Difficulty Next(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Story:
                    return Difficulty.Knight;
                case Difficulty.Knight:
                    return Difficulty.Nightmare;
                default:
                    return Difficulty.Story;
            }
        }
    }
}
