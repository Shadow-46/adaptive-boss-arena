using UnityEngine;

namespace AdaptiveBossArena.AI
{
    /// <summary>
    /// How fast, and which way, the boss moves between attacks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player asked for a boss with an aura: "slow and heavy walks in between phases", not a fast, twitchy
    /// one. It was the opposite. The motor normalised every direction to full speed, so the brute circled at a
    /// run; and it snapped between closing, backing off and circling at a hard 0.75 m boundary, so its legs
    /// changed their mind every few frames.
    /// </para>
    /// <para>
    /// Now it stalks: a slow walk while it circles and measures the player, a smooth blend between closing and
    /// giving ground, and a run only when there is real distance to cover. Pure, so the pacing can be read and
    /// tested without a scene; the numbers live on <see cref="BossConfig"/>.
    /// </para>
    /// </remarks>
    public static class BossPacing
    {
        /// <summary>
        /// How far from its preferred range the boss must be before it commits fully to closing or retreating.
        /// </summary>
        /// <remarks>Inside this the pull fades smoothly to zero, so there is no edge to flicker across.</remarks>
        public const float RangeBlendMetres = 2f;

        /// <summary>Sideways share of the stalk, so the boss circles rather than walking straight at the player.</summary>
        public const float StrafeWeight = 0.6f;

        /// <summary>
        /// The move while observing: a direction scaled to the fraction of top speed to walk at.
        /// </summary>
        /// <param name="toPlayer">Unit direction to the player.</param>
        /// <param name="distance">Distance to the player.</param>
        /// <param name="preferredRange">Distance the boss wants to hold.</param>
        /// <param name="stalkFraction">Fraction of top speed the stalk walks at.</param>
        /// <param name="strafeSide">
        /// Which way to circle: one to the boss's right, minus one to its left. A boss that has learned which
        /// way the player circles goes the same way, stepping into their path instead of chasing their back.
        /// </param>
        /// <returns>Movement whose length is the fraction of top speed to move at.</returns>
        public static Vector3 Stalk(
            Vector3 toPlayer, float distance, float preferredRange, float stalkFraction, float strafeSide = 1f)
        {
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            toPlayer.Normalize();

            // Positive when too far, negative when too close, easing through zero at the preferred range.
            float pull = Mathf.Clamp((distance - preferredRange) / RangeBlendMetres, -1f, 1f);
            Vector3 strafe = Vector3.Cross(Vector3.up, toPlayer) * (StrafeWeight * Mathf.Clamp(strafeSide, -1f, 1f));
            Vector3 heading = toPlayer * pull + strafe;

            return heading.sqrMagnitude < Mathf.Epsilon
                ? Vector3.zero
                : heading.normalized * Mathf.Clamp01(stalkFraction);
        }

        /// <summary>
        /// Which way the stalk circles: the boss's own way, or with the player once it has learned their side.
        /// </summary>
        /// <param name="cutOffStrength">Learned willingness to cut the player off.</param>
        /// <param name="habitualCirclingSide">The player's habitual side, one right, minus one left.</param>
        /// <returns>The strafe side for <see cref="Stalk"/>.</returns>
        public static float StrafeSideFor(float cutOffStrength, float habitualCirclingSide) =>
            cutOffStrength >= CutOffThreshold && Mathf.Abs(habitualCirclingSide) >= CirclingSideThreshold
                ? Mathf.Sign(habitualCirclingSide)
                : 1f;

        /// <summary>Learned cut-off strength past which the boss circles with the player.</summary>
        public const float CutOffThreshold = 0.5f;

        /// <summary>How one-sided the player's circling must be before there is a side to cut off.</summary>
        public const float CirclingSideThreshold = 0.3f;

        /// <summary>Angle from his facing past which the knight counts as behind him, in degrees.</summary>
        public const float BehindAngleDegrees = 120f;

        /// <summary>Distance inside which the knight is crowding him, in metres.</summary>
        public const float CrowdedMetres = 1.8f;

        /// <summary>
        /// Chooses the next beat of the boss's footwork between blows.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A duellist does not circle one way at one pace until it strikes: it circles, stops to measure, gives a
        /// step of ground, changes direction, and answers a knight who crowds it or slips behind it. Each beat
        /// lasts a second or two, so the rhythm changes as often as a watching player would notice.
        /// </para>
        /// <para>
        /// Everything it reads was perceived, through the delay: the distance and the angle to the knight. A
        /// knight behind him is answered with a turn; a crowding one with a hop or a step back. Otherwise the beat
        /// is drawn by weight, with the last beat discouraged so no rhythm settles. Pure, so it can be tested.
        /// </para>
        /// </remarks>
        /// <param name="distance">Perceived distance to the knight.</param>
        /// <param name="preferredRange">The distance the boss wants to hold.</param>
        /// <param name="angleToKnight">Perceived angle between his facing and the knight, in degrees.</param>
        /// <param name="last">The beat just finished.</param>
        /// <param name="roll">A uniform draw in [0, 1).</param>
        /// <returns>The next beat.</returns>
        public static PacingBeat NextBeat(
            float distance, float preferredRange, float angleToKnight, PacingBeat last, float roll)
        {
            if (angleToKnight >= BehindAngleDegrees && last != PacingBeat.Turn)
            {
                return PacingBeat.Turn;
            }

            if (distance <= CrowdedMetres && last != PacingBeat.HopBack)
            {
                return roll < 0.5f ? PacingBeat.HopBack : PacingBeat.GiveGround;
            }

            float circle = 0.55f;
            float pause = 0.25f;
            float giveGround = distance < preferredRange ? 0.2f : 0f;

            // The beat just played is discouraged, not ruled out: two circles running in opposite directions is
            // still a change of rhythm the player sees.
            switch (last)
            {
                case PacingBeat.Pause:
                    pause *= 0.3f;
                    break;
                case PacingBeat.GiveGround:
                    giveGround *= 0.3f;
                    break;
            }

            float pick = Mathf.Clamp01(roll) * (circle + pause + giveGround);

            return pick < circle ? PacingBeat.Circle
                : pick < circle + pause ? PacingBeat.Pause
                : PacingBeat.GiveGround;
        }

        /// <summary>How long a beat lasts, in seconds, from a uniform draw.</summary>
        /// <param name="beat">The beat.</param>
        /// <param name="roll">A uniform draw in [0, 1).</param>
        /// <returns>The beat's length.</returns>
        public static float BeatSeconds(PacingBeat beat, float roll)
        {
            roll = Mathf.Clamp01(roll);

            switch (beat)
            {
                case PacingBeat.Pause:
                    return Mathf.Lerp(0.5f, 1.1f, roll);
                case PacingBeat.GiveGround:
                    return Mathf.Lerp(0.8f, 1.4f, roll);
                case PacingBeat.HopBack:
                    return HopBackSeconds;
                case PacingBeat.Turn:
                    return TurnSeconds;
                default:
                    return Mathf.Lerp(1.2f, 2.6f, roll);
            }
        }

        /// <summary>How long the hop back takes, airborne travel and landing together.</summary>
        public const float HopBackSeconds = 0.6f;

        /// <summary>The share of the hop spent travelling; the rest is the landing.</summary>
        public const float HopBackTravelShare = 0.6f;

        /// <summary>Speed of the hop's travel, in metres per second: about two metres of ground.</summary>
        public const float HopBackSpeed = 5.5f;

        /// <summary>How long the turn on the spot takes.</summary>
        public const float TurnSeconds = 0.65f;

        /// <summary>
        /// Speed while closing in to attack, as a fraction of top speed.
        /// </summary>
        /// <remarks>
        /// A heavy walk when the blow is almost in reach, rising to a run only across real distance, and scaled
        /// by learned aggression so a boss that has learned to press comes on harder - numbers, not new rules.
        /// </remarks>
        /// <param name="distanceBeyondReach">How much further the player is than the chosen attack reaches.</param>
        /// <param name="runDistance">Distance beyond reach at and past which the boss runs.</param>
        /// <param name="walkFraction">Fraction of top speed it walks at when close.</param>
        /// <param name="aggression">Learned aggression, zero to one.</param>
        /// <returns>Fraction of top speed to approach at.</returns>
        public static float ApproachSpeed(float distanceBeyondReach, float runDistance, float walkFraction, float aggression)
        {
            float urgency = runDistance <= 0f ? 1f : Mathf.Clamp01(distanceBeyondReach / runDistance);
            float speed = Mathf.Lerp(Mathf.Clamp01(walkFraction), 1f, urgency * urgency);

            return speed * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(aggression));
        }
    }

    /// <summary>One beat of the boss's footwork between blows.</summary>
    public enum PacingBeat
    {
        /// <summary>Circling the knight, one way or the other.</summary>
        Circle,

        /// <summary>Standing still, measuring.</summary>
        Pause,

        /// <summary>Walking a step or two back while still facing the knight.</summary>
        GiveGround,

        /// <summary>A quick hop back out of a crowding knight's reach.</summary>
        HopBack,

        /// <summary>A turn on the spot toward a knight who has got behind him.</summary>
        Turn
    }
}
