using System.Collections.Generic;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>How one attempt went, gathered by the encounter when it ends.</summary>
    /// <remarks>
    /// Everything a record or a trial is judged from, in one value, so the judging is pure and testable
    /// without a scene. Gathered only from the knight's side and from what the fight reported; nothing here
    /// ever reaches the boss.
    /// </remarks>
    public readonly struct AttemptSummary
    {
        /// <summary>Creates a summary.</summary>
        /// <param name="won">Whether Igris was beaten.</param>
        /// <param name="difficulty">The difficulty fought on.</param>
        /// <param name="ascended">Whether it was the Ascended rematch.</param>
        /// <param name="durationSeconds">How long the attempt lasted.</param>
        /// <param name="remainingHealth">The knight's health fraction at the end.</param>
        /// <param name="adaptationsAllowed">Counter-strategies Igris developed.</param>
        /// <param name="hitsTaken">Blows that took health from the knight.</param>
        /// <param name="healsUsed">Flasks drunk.</param>
        /// <param name="deflects">Clean deflects and parries landed.</param>
        /// <param name="perfectDodges">Perfect dodges.</param>
        /// <param name="weaponName">The weapon in hand at the end.</param>
        public AttemptSummary(
            bool won,
            Difficulty difficulty,
            bool ascended,
            float durationSeconds,
            float remainingHealth,
            int adaptationsAllowed,
            int hitsTaken,
            int healsUsed,
            int deflects,
            int perfectDodges,
            string weaponName)
        {
            Won = won;
            Difficulty = difficulty;
            Ascended = ascended;
            DurationSeconds = durationSeconds;
            RemainingHealth = remainingHealth;
            AdaptationsAllowed = adaptationsAllowed;
            HitsTaken = hitsTaken;
            HealsUsed = healsUsed;
            Deflects = deflects;
            PerfectDodges = perfectDodges;
            WeaponName = weaponName ?? string.Empty;
        }

        /// <summary>Whether Igris was beaten.</summary>
        public bool Won { get; }

        /// <summary>The difficulty fought on.</summary>
        public Difficulty Difficulty { get; }

        /// <summary>Whether it was the Ascended rematch.</summary>
        public bool Ascended { get; }

        /// <summary>How long the attempt lasted, in seconds of fight time.</summary>
        public float DurationSeconds { get; }

        /// <summary>The knight's health fraction at the end.</summary>
        public float RemainingHealth { get; }

        /// <summary>Counter-strategies Igris developed.</summary>
        public int AdaptationsAllowed { get; }

        /// <summary>Blows that took health from the knight.</summary>
        public int HitsTaken { get; }

        /// <summary>Flasks drunk.</summary>
        public int HealsUsed { get; }

        /// <summary>Clean deflects and parries landed.</summary>
        public int Deflects { get; }

        /// <summary>Perfect dodges.</summary>
        public int PerfectDodges { get; }

        /// <summary>The weapon in hand when the attempt ended.</summary>
        public string WeaponName { get; }

        /// <summary>Whether the fight was at least the one as balanced: Knight or Nightmare.</summary>
        public bool AtLeastKnight => Difficulty == Difficulty.Knight || Difficulty == Difficulty.Nightmare;
    }

    /// <summary>A colour the knight's armour and blade can wear, unlocked by a trial.</summary>
    /// <remarks>
    /// Cosmetic only. The reward for a trial is something the player can see and show, never power: a stronger
    /// knight would make the records meaningless, and a tint changes nothing about the fight.
    /// </remarks>
    public readonly struct KnightTint
    {
        /// <summary>Creates a tint.</summary>
        /// <param name="id">Stable id.</param>
        /// <param name="name">Display name.</param>
        /// <param name="r">Armour red multiplier.</param>
        /// <param name="g">Armour green multiplier.</param>
        /// <param name="b">Armour blue multiplier.</param>
        /// <param name="glowR">Blade glow red.</param>
        /// <param name="glowG">Blade glow green.</param>
        /// <param name="glowB">Blade glow blue.</param>
        public KnightTint(string id, string name, float r, float g, float b, float glowR, float glowG, float glowB)
        {
            Id = id;
            Name = name;
            R = r;
            G = g;
            B = b;
            GlowR = glowR;
            GlowG = glowG;
            GlowB = glowB;
        }

        /// <summary>Stable id, saved with the choice.</summary>
        public string Id { get; }

        /// <summary>Name shown on the title screen.</summary>
        public string Name { get; }

        /// <summary>Red multiplier on the armour.</summary>
        public float R { get; }

        /// <summary>Green multiplier on the armour.</summary>
        public float G { get; }

        /// <summary>Blue multiplier on the armour.</summary>
        public float B { get; }

        /// <summary>Red of the blade's trail.</summary>
        public float GlowR { get; }

        /// <summary>Green of the blade's trail.</summary>
        public float GlowG { get; }

        /// <summary>Blue of the blade's trail.</summary>
        public float GlowB { get; }

        /// <summary>Whether this is the plain, untinted knight.</summary>
        public bool IsDefault => Id == TrialBook.DefaultTintId;
    }

    /// <summary>A challenge judged at the end of an attempt, and the tint it unlocks.</summary>
    public sealed class Trial
    {
        private readonly System.Func<AttemptSummary, bool> _isMet;

        /// <summary>Creates a trial.</summary>
        /// <param name="id">Stable id.</param>
        /// <param name="title">Short name.</param>
        /// <param name="description">What has to be done.</param>
        /// <param name="rewardTintId">The tint it unlocks, or null for none.</param>
        /// <param name="isMet">The condition on a winning attempt.</param>
        public Trial(string id, string title, string description, string rewardTintId, System.Func<AttemptSummary, bool> isMet)
        {
            Id = id;
            Title = title;
            Description = description;
            RewardTintId = rewardTintId;
            _isMet = isMet;
        }

        /// <summary>Stable id, saved when completed.</summary>
        public string Id { get; }

        /// <summary>Short name.</summary>
        public string Title { get; }

        /// <summary>What has to be done, in the player's terms.</summary>
        public string Description { get; }

        /// <summary>The tint completing it unlocks, or null.</summary>
        public string RewardTintId { get; }

        /// <summary>Whether an attempt meets the trial. Only a win can.</summary>
        /// <param name="summary">The attempt.</param>
        /// <returns>True when met.</returns>
        public bool IsMet(in AttemptSummary summary) => summary.Won && _isMet(summary);
    }

    /// <summary>The trials, the tints they unlock, and the judging of an attempt against them.</summary>
    /// <remarks>
    /// <para>
    /// Each trial asks for a different way of beating the same fight, which is the replay this game can
    /// honestly offer: winning without being read, without being touched, without drinking, on the hard mode,
    /// against the rematch. None of them asks for anything the fight does not already allow.
    /// </para>
    /// <para>
    /// Story wins count only for the first trial. The rest are measured against the fight as balanced, or the
    /// trial would be a matter of picking the easy mode.
    /// </para>
    /// </remarks>
    public static class TrialBook
    {
        /// <summary>The plain knight's tint id.</summary>
        public const string DefaultTintId = "steel";

        /// <summary>Seconds a Swift win must beat.</summary>
        public const float SwiftSeconds = 180f;

        /// <summary>Deflects a Duellist win must land.</summary>
        public const int DuellistDeflects = 10;

        /// <summary>Most answers Igris may develop in an Unread win.</summary>
        public const int UnreadAdaptations = 1;

        /// <summary>Every tint, the plain one first.</summary>
        public static readonly IReadOnlyList<KnightTint> Tints = new[]
        {
            new KnightTint(DefaultTintId, "Steel", 1f, 1f, 1f, 1f, 0.55f, 0.25f),
            new KnightTint("ember", "Ember", 1f, 0.78f, 0.62f, 1f, 0.42f, 0.1f),
            new KnightTint("bloodsworn", "Bloodsworn", 0.95f, 0.5f, 0.48f, 1f, 0.12f, 0.08f),
            new KnightTint("obsidian", "Obsidian", 0.42f, 0.42f, 0.48f, 0.55f, 0.2f, 1f),
            new KnightTint("gilded", "Gilded", 1.15f, 0.95f, 0.55f, 1f, 0.82f, 0.3f),
            new KnightTint("ashen", "Ashen", 0.82f, 0.82f, 0.8f, 0.85f, 0.85f, 0.9f),
            new KnightTint("spectral", "Spectral", 0.7f, 0.9f, 1f, 0.35f, 0.9f, 1f),
            new KnightTint("verdant", "Verdant", 0.72f, 0.95f, 0.7f, 0.4f, 1f, 0.35f),
            new KnightTint("commander", "Commander's Crimson", 0.75f, 0.32f, 0.32f, 1f, 0.05f, 0.15f),
        };

        /// <summary>Every trial.</summary>
        public static readonly IReadOnlyList<Trial> Trials = new[]
        {
            new Trial("first_blood", "First Blood", "Defeat Igris on any difficulty.", "ember", s => true),
            new Trial("knight", "Knighted", "Defeat Igris on Knight or Nightmare.", "bloodsworn", s => s.AtLeastKnight),
            new Trial("nightmare", "Waking Nightmare", "Defeat Igris on Nightmare.", "obsidian",
                s => s.Difficulty == Difficulty.Nightmare),
            new Trial("untouched", "Untouched", "Win without taking a single hit (Knight+).", "gilded",
                s => s.AtLeastKnight && s.HitsTaken == 0),
            new Trial("abstinent", "Abstinent", "Win without drinking a flask (Knight+).", "ashen",
                s => s.AtLeastKnight && s.HealsUsed == 0),
            new Trial("unread", "Unread", "Win while Igris develops at most one answer (Knight+).", "spectral",
                s => s.AtLeastKnight && s.AdaptationsAllowed <= UnreadAdaptations),
            new Trial("duellist", "Duellist", "Land ten deflects or parries in one win (Knight+).", "verdant",
                s => s.AtLeastKnight && s.Deflects >= DuellistDeflects),
            new Trial("swift", "Swift Judgement", "Win in under three minutes (Knight+).", null,
                s => s.AtLeastKnight && s.DurationSeconds < SwiftSeconds),
            new Trial("ascended", "Ascendant", "Defeat Igris Ascended (Knight+).", "commander",
                s => s.Ascended && s.AtLeastKnight),
        };

        /// <summary>The trials an attempt meets.</summary>
        /// <param name="summary">The attempt.</param>
        /// <returns>The met trials, in book order; empty for a loss.</returns>
        public static List<Trial> MetBy(in AttemptSummary summary)
        {
            var met = new List<Trial>();

            foreach (Trial trial in Trials)
            {
                if (trial.IsMet(summary))
                {
                    met.Add(trial);
                }
            }

            return met;
        }

        /// <summary>A tint by id.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The tint, or the plain one when the id is unknown.</returns>
        public static KnightTint TintById(string id)
        {
            foreach (KnightTint tint in Tints)
            {
                if (tint.Id == id)
                {
                    return tint;
                }
            }

            return Tints[0];
        }

        /// <summary>The trial that unlocks a tint, or null for the plain one.</summary>
        /// <param name="tintId">The tint.</param>
        /// <returns>The unlocking trial.</returns>
        public static Trial TrialFor(string tintId)
        {
            foreach (Trial trial in Trials)
            {
                if (trial.RewardTintId == tintId)
                {
                    return trial;
                }
            }

            return null;
        }

        /// <summary>Whether a tint is unlocked: the plain one always, the rest by their trial.</summary>
        /// <param name="tintId">The tint.</param>
        /// <param name="records">The player's records; null counts as nothing completed.</param>
        /// <returns>True when the tint may be worn.</returns>
        public static bool IsTintUnlocked(string tintId, RecordsData records)
        {
            if (tintId == DefaultTintId)
            {
                return true;
            }

            Trial trial = TrialFor(tintId);
            return trial != null && records != null && records.HasCompleted(trial.Id);
        }

        /// <summary>The next unlocked tint after the given one, wrapping, for the title screen's cycle.</summary>
        /// <param name="currentId">The tint worn now.</param>
        /// <param name="records">The player's records.</param>
        /// <returns>The next wearable tint's id.</returns>
        public static string NextUnlockedTint(string currentId, RecordsData records)
        {
            int start = 0;

            for (int i = 0; i < Tints.Count; i++)
            {
                if (Tints[i].Id == currentId)
                {
                    start = i;
                    break;
                }
            }

            for (int step = 1; step <= Tints.Count; step++)
            {
                KnightTint candidate = Tints[(start + step) % Tints.Count];

                if (IsTintUnlocked(candidate.Id, records))
                {
                    return candidate.Id;
                }
            }

            return DefaultTintId;
        }
    }
}
