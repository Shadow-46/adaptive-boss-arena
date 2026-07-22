using System;
using AdaptiveBossArena.Utilities;
using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// The mutable numbers that describe how the boss currently fights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the entire surface through which learning changes behaviour. The state machine reads
    /// these values; the adaptation system writes them. Neither knows anything about the other,
    /// which is what keeps a counter-strategy from having to understand the boss's states and keeps
    /// the states from having to understand the player's habits.
    /// </para>
    /// <para>
    /// Values are eased toward their targets rather than assigned. An instant change reads as the
    /// boss being handed information; a change that takes several seconds reads as the boss working
    /// something out. The easing is the difference between the mechanic feeling clever and feeling
    /// like a cheat, and it costs one line.
    /// </para>
    /// <para>
    /// Every parameter also decays back toward its baseline when nothing is reinforcing it. That is
    /// what makes switching tactics a real answer to being countered, rather than a permanent
    /// penalty for having had a habit once.
    /// </para>
    /// </remarks>
    public sealed class BossTuning
    {
        private static readonly int ParameterCount = Enum.GetValues(typeof(BossTuningParameter)).Length;

        private readonly float[] _current;
        private readonly float[] _target;
        private readonly float[] _baseline;

        /// <summary>Creates a tuning set at its baseline values.</summary>
        /// <param name="baselinePreferredRange">Distance the boss holds before it has learned anything.</param>
        /// <param name="baselineAggression">Willingness to press before it has learned anything.</param>
        public BossTuning(float baselinePreferredRange, float baselineAggression)
        {
            _current = new float[ParameterCount];
            _target = new float[ParameterCount];
            _baseline = new float[ParameterCount];

            SetBaseline(BossTuningParameter.PreferredRange, baselinePreferredRange);
            SetBaseline(BossTuningParameter.Aggression, baselineAggression);

            // Everything else starts at zero: the boss begins the fight with none of its
            // counter-behaviours engaged and has to earn each one.
            for (int i = 0; i < ParameterCount; i++)
            {
                _current[i] = _baseline[i];
                _target[i] = _baseline[i];
            }
        }

        /// <summary>Reads the current value of a parameter.</summary>
        /// <param name="parameter">The parameter to read.</param>
        /// <returns>Its present value, part-way toward its target.</returns>
        public float Get(BossTuningParameter parameter) => _current[(int)parameter];

        /// <summary>Reads the value a parameter is currently easing toward.</summary>
        /// <param name="parameter">The parameter to read.</param>
        /// <returns>Its target value.</returns>
        public float GetTarget(BossTuningParameter parameter) => _target[(int)parameter];

        /// <summary>Reads a parameter's untrained resting value.</summary>
        /// <param name="parameter">The parameter to read.</param>
        /// <returns>Its baseline value.</returns>
        public float GetBaseline(BossTuningParameter parameter) => _baseline[(int)parameter];

        /// <summary>Sets the value a parameter should ease toward.</summary>
        /// <param name="parameter">The parameter to adjust.</param>
        /// <param name="value">The value to approach.</param>
        public void SetTarget(BossTuningParameter parameter, float value) =>
            _target[(int)parameter] = value;

        /// <summary>Defines a parameter's resting value.</summary>
        /// <param name="parameter">The parameter to configure.</param>
        /// <param name="value">Its untrained value.</param>
        public void SetBaseline(BossTuningParameter parameter, float value) =>
            _baseline[(int)parameter] = value;

        /// <summary>
        /// Eases every parameter toward its target, and every target back toward its baseline.
        /// </summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        /// <param name="easeHalfLifeSeconds">
        /// Time to close half the distance to a target. Larger values make adaptation more gradual
        /// and therefore more perceptible as a change in the boss rather than a jump.
        /// </param>
        /// <param name="forgetHalfLifeSeconds">
        /// Time for an unreinforced target to fall half-way back to baseline.
        /// </param>
        public void Tick(float deltaTime, float easeHalfLifeSeconds, float forgetHalfLifeSeconds)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (int i = 0; i < ParameterCount; i++)
            {
                _target[i] = MathUtil.Damp(_target[i], _baseline[i], forgetHalfLifeSeconds, deltaTime);
                _current[i] = MathUtil.Damp(_current[i], _target[i], easeHalfLifeSeconds, deltaTime);
            }
        }

        /// <summary>Returns every parameter to its baseline immediately, for a fight restart.</summary>
        public void ResetToBaseline()
        {
            for (int i = 0; i < ParameterCount; i++)
            {
                _current[i] = _baseline[i];
                _target[i] = _baseline[i];
            }
        }

        /// <summary>
        /// Reports how far a parameter has travelled from its baseline, for the debug overlay.
        /// </summary>
        /// <param name="parameter">The parameter to inspect.</param>
        /// <returns>Absolute distance from baseline.</returns>
        public float DeviationFromBaseline(BossTuningParameter parameter) =>
            Mathf.Abs(_current[(int)parameter] - _baseline[(int)parameter]);
    }
}
