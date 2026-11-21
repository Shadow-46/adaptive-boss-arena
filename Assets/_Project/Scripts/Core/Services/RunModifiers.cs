using System;
using System.Collections.Generic;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Optional challenge rules chosen before a fight, changing the encounter without changing its
    /// mechanics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every modifier is expressed as a <em>number</em> the existing systems already respect — an
    /// adaptation-rate scale, a damage multiplier, a pair of flags — never a new rule. That is the
    /// same discipline the boss's own adaptation follows, and it keeps a challenge run readable: the
    /// boss you fight on Fast Learner is the same boss, working you out sooner.
    /// </para>
    /// <para>
    /// A plain serialisable record like <see cref="SettingsData"/>, with defaults chosen so an
    /// unmodified run is the ordinary fight. Held for the session in <see cref="RunSettings"/> so the
    /// choice made on the title screen survives the load into the arena.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RunModifiers
    {
        /// <summary>Schema version this record was written with.</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Version the current code writes and understands.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>How hard the fight is. Knight, the zero value, is the fight as balanced.</summary>
        public Difficulty Difficulty = Difficulty.Knight;

        /// <summary>
        /// The rematch, unlocked by a win: Igris opens in his second phase, tougher and quicker to swing.
        /// </summary>
        public bool Ascended;

        /// <summary>The tint the knight wears, by id. Cosmetic; unlocked by trials.</summary>
        public string KnightTintId = TrialBook.DefaultTintId;

        /// <summary>The boss works the player out faster, for a run that adapts sooner.</summary>
        public bool FastLearner;

        /// <summary>The player cannot heal, so every hit taken has to be earned back in the exchange.</summary>
        public bool NoHealing;

        /// <summary>The player takes more damage, punishing loose defence harder.</summary>
        public bool FragilePlayer;

        /// <summary>Practice: the tell overlay stays on and the player cannot be killed.</summary>
        public bool TrainingMode;

        /// <summary>How much faster the boss adapts under Fast Learner.</summary>
        private const float FastLearnerScale = 2f;

        /// <summary>How much harder a fragile player is hit.</summary>
        private const float FragileDamageMultiplier = 1.5f;

        /// <summary>How much less damage an Ascended Igris takes: a quarter more health to cut through.</summary>
        private const float AscendedDamageTakenScale = 0.8f;

        /// <summary>How much shorter an Ascended Igris rests between swings.</summary>
        private const float AscendedCooldownScale = 0.9f;

        /// <summary>The phase an Ascended Igris opens in: his second, the sword already burning.</summary>
        public const int AscendedOpeningPhase = 1;

        /// <summary>The numbers the chosen difficulty changes.</summary>
        public DifficultyProfile Profile => DifficultyProfile.For(Difficulty);

        /// <summary>Multiplier on the boss's adaptation rate: difficulty times Fast Learner.</summary>
        public float AdaptationRateScale => Profile.AdaptationRateScale * (FastLearner ? FastLearnerScale : 1f);

        /// <summary>Multiplier on damage the player takes: difficulty times Fragile.</summary>
        public float IncomingDamageMultiplier =>
            Profile.IncomingDamageScale * (FragilePlayer ? FragileDamageMultiplier : 1f);

        /// <summary>Multiplier on damage the boss takes: difficulty times Ascended.</summary>
        public float BossDamageTakenScale =>
            Profile.BossDamageTakenScale * (Ascended ? AscendedDamageTakenScale : 1f);

        /// <summary>Multiplier on the boss's rest between swings: difficulty times Ascended.</summary>
        public float BossCooldownScale => Profile.BossCooldownScale * (Ascended ? AscendedCooldownScale : 1f);

        /// <summary>Flasks added to or taken from the knight's usual count.</summary>
        public int HealChargeBonus => Profile.HealChargeBonus;

        /// <summary>The phase the boss opens the fight in.</summary>
        public int OpeningPhase => Ascended ? AscendedOpeningPhase : 0;

        /// <summary>Whether the player may heal.</summary>
        public bool HealingEnabled => !NoHealing;

        /// <summary>True when any modifier departs from the ordinary fight.</summary>
        public bool AnyActive =>
            FastLearner || NoHealing || FragilePlayer || TrainingMode || Ascended || Difficulty != Difficulty.Knight;

        /// <summary>
        /// Whether an attempt under these rules may be written to the permanent records.
        /// </summary>
        /// <remarks>
        /// Training makes the player unkillable, so a "victory" there is not a victory — recording it
        /// would permanently set the fastest time and the fewest adaptations allowed with a run that
        /// could not be lost, and there is no way to take that back. The other three modifiers all
        /// make the fight <em>harder</em>, so a win under them is worth at least as much as an
        /// ordinary one and counts normally.
        /// </remarks>
        public bool CountsTowardRecords => !TrainingMode;

        /// <summary>Short names of the active modifiers, for the post-fight report.</summary>
        /// <returns>One label per active modifier, empty for an unmodified run.</returns>
        public IReadOnlyList<string> ActiveLabels()
        {
            var labels = new List<string>();

            if (Difficulty != Difficulty.Knight)
            {
                labels.Add(DifficultyProfile.Label(Difficulty));
            }

            if (Ascended)
            {
                labels.Add("Ascended");
            }

            if (FastLearner)
            {
                labels.Add("Fast Learner");
            }

            if (NoHealing)
            {
                labels.Add("No Healing");
            }

            if (FragilePlayer)
            {
                labels.Add("Fragile");
            }

            if (TrainingMode)
            {
                labels.Add("Training");
            }

            return labels;
        }
    }

    /// <summary>
    /// The modifiers in effect for the current session, carried from the title screen into the fight.
    /// </summary>
    /// <remarks>
    /// A static holder because the title and the arena are separate scenes with separate service
    /// registries; a static survives the scene load that a scene reference could not. It is written
    /// only on the title screen and read only by the encounter's composition root, so nothing in
    /// <c>AI</c> or <c>Learning</c> ever depends on it.
    /// </remarks>
    public static class RunSettings
    {
        /// <summary>The modifiers chosen for this session. Never null.</summary>
        public static RunModifiers Current { get; set; } = new RunModifiers();
    }
}
