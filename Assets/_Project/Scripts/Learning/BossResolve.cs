using UnityEngine;

namespace AdaptiveBossArena.Learning
{
    /// <summary>
    /// A meter the boss fills over a fight and spends on a signature gambit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolve is a designed resource, not an adaptation: it gates <em>when</em> the boss commits to a
    /// set-piece it always had, rather than changing what it can do. It builds from two things the
    /// boss legitimately witnesses — time spent fighting, and its own attacks whiffing (the player
    /// slipping its swings) — so a purely evasive player winds the gambit up faster, which reads as
    /// the boss growing frustrated at being dodged. Nothing here touches player input.
    /// </para>
    /// <para>
    /// Pure like <see cref="BossTuning"/>, so its accumulation and threshold can be exercised in an
    /// edit-mode test without standing up a boss. The visible cue that it is filling — the boss's aura
    /// winding up — lives on the presentation side; this only decides when the gambit is ready.
    /// </para>
    /// </remarks>
    public sealed class BossResolve
    {
        private readonly float _threshold;
        private readonly float _buildPerSecond;
        private readonly float _buildPerWhiff;

        private float _current;

        /// <summary>Creates an empty meter.</summary>
        /// <param name="threshold">Resolve required before the gambit is ready. Must be positive.</param>
        /// <param name="buildPerSecond">Resolve gained per second of fighting.</param>
        /// <param name="buildPerWhiff">Resolve gained each time one of the boss's attacks whiffs.</param>
        public BossResolve(float threshold, float buildPerSecond, float buildPerWhiff)
        {
            _threshold = Mathf.Max(1f, threshold);
            _buildPerSecond = Mathf.Max(0f, buildPerSecond);
            _buildPerWhiff = Mathf.Max(0f, buildPerWhiff);
        }

        /// <summary>How full the meter is, zero to one, for the charge glow.</summary>
        public float Normalized => Mathf.Clamp01(_current / _threshold);

        /// <summary>True once the gambit may be unleashed.</summary>
        public bool IsReady => _current >= _threshold;

        /// <summary>Accrues resolve from the passage of the fight.</summary>
        /// <param name="deltaTime">Elapsed scaled time.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                Add(_buildPerSecond * deltaTime);
            }
        }

        /// <summary>Accrues resolve because one of the boss's own attacks was avoided.</summary>
        public void RegisterBossWhiff() => Add(_buildPerWhiff);

        /// <summary>Empties the meter after the gambit is spent.</summary>
        public void Spend() => _current = 0f;

        /// <summary>Empties the meter for a fresh attempt.</summary>
        public void Reset() => _current = 0f;

        /// <summary>Adds resolve, capped at the threshold so a ready meter simply stays full.</summary>
        private void Add(float amount) => _current = Mathf.Min(_threshold, _current + Mathf.Max(0f, amount));
    }
}
