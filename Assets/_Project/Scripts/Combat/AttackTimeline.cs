using System;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Drives an attack through startup, active and recovery on a stopwatch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The concrete stand-in for animation events. Because it is ticked with an explicit delta, an
    /// attack's frame data can be verified in an edit-mode test, and hit-stop freezes an attack's
    /// progress simply by supplying scaled time.
    /// </para>
    /// <para>
    /// Phase transitions are evaluated once per tick and can skip a phase entirely if the frame was
    /// long enough. That is deliberate: a hitch must never leave an attack stuck mid-swing with its
    /// hitbox open. It does mean a very long frame can pass over a short active window without ever
    /// reporting it, which is why <see cref="Tick"/> reports phases in order rather than only
    /// announcing the final one.
    /// </para>
    /// </remarks>
    public sealed class AttackTimeline : IAttackTimeline
    {
        private AttackDefinition _attack;
        private float _elapsed;
        private AttackPhase _phase = AttackPhase.Inactive;

        /// <inheritdoc />
        public AttackPhase Phase => _phase;

        /// <inheritdoc />
        public bool IsRunning => _phase != AttackPhase.Inactive;

        /// <inheritdoc />
        public float ElapsedSeconds => _elapsed;

        /// <inheritdoc />
        public float NormalizedTime =>
            _attack == null || _attack.TotalSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(_elapsed / _attack.TotalSeconds);

        /// <inheritdoc />
        public AttackDefinition CurrentAttack => _attack;

        /// <inheritdoc />
        public bool IsInComboWindow =>
            _attack != null &&
            _elapsed >= _attack.ComboWindowStartSeconds &&
            _elapsed <= _attack.ComboWindowEndSeconds;

        /// <inheritdoc />
        public event Action<AttackPhase> PhaseChanged;

        /// <inheritdoc />
        public event Action Completed;

        /// <inheritdoc />
        public void Begin(AttackDefinition attack)
        {
            _attack = attack != null ? attack : throw new ArgumentNullException(nameof(attack));
            _elapsed = 0f;

            TransitionTo(AttackPhase.Startup);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f)
            {
                return;
            }

            _elapsed += deltaTime;

            // Advance one phase at a time so that a long frame cannot skip past the active window
            // without the hitbox ever having been opened.
            while (IsRunning && _phase != ResolvePhase())
            {
                TransitionTo(NextPhaseAfter(_phase));
            }
        }

        /// <inheritdoc />
        public void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            _attack = null;
            _elapsed = 0f;
            _phase = AttackPhase.Inactive;

            // Completed is not raised. A cancelled attack did not finish, and anything waiting on
            // completion to hand control back must distinguish the two.
            PhaseChanged?.Invoke(AttackPhase.Inactive);
        }

        /// <summary>The phase the elapsed time currently falls within.</summary>
        private AttackPhase ResolvePhase()
        {
            if (_attack == null)
            {
                return AttackPhase.Inactive;
            }

            if (_elapsed < _attack.ActiveStartSeconds)
            {
                return AttackPhase.Startup;
            }

            if (_elapsed < _attack.ActiveEndSeconds)
            {
                return AttackPhase.Active;
            }

            return _elapsed < _attack.TotalSeconds ? AttackPhase.Recovery : AttackPhase.Inactive;
        }

        /// <summary>The next phase in the fixed startup, active, recovery, inactive order.</summary>
        private static AttackPhase NextPhaseAfter(AttackPhase phase)
        {
            switch (phase)
            {
                case AttackPhase.Startup: return AttackPhase.Active;
                case AttackPhase.Active: return AttackPhase.Recovery;
                default: return AttackPhase.Inactive;
            }
        }

        /// <summary>Enters a phase, announcing it and completing the attack when it ends.</summary>
        private void TransitionTo(AttackPhase phase)
        {
            _phase = phase;

            if (phase == AttackPhase.Inactive)
            {
                _attack = null;
                _elapsed = 0f;

                PhaseChanged?.Invoke(AttackPhase.Inactive);
                Completed?.Invoke();
                return;
            }

            PhaseChanged?.Invoke(phase);
        }
    }
}
