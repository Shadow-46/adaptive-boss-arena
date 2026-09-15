using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// A piece of the arena that a heavy blow can break into falling stone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Breaking hides the intact stone and throws pieces from the debris pool out of the space it filled.
    /// It never touches a collider: what breaks is dressing, so the fight's geometry is the same before
    /// and after, and a broken parapet cannot open a gap in the arena's boundary.
    /// </para>
    /// <para>
    /// Restored for every retry, so an attempt always starts in the same room.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Destructible : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Every visible part of the intact stone, hidden when this breaks.")]
        private Renderer[] _intact = new Renderer[0];

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra reach for stone that stands back from where blows land, such as a column behind the wall.")]
        private float _reachBonus;

        [SerializeField]
        [Tooltip("Dimensions of the intact stone, in metres, which the pieces are cut from.")]
        private Vector3 _size = Vector3.one;

        [SerializeField]
        [Range(1, 16)]
        [Tooltip("How many pieces it breaks into.")]
        private int _pieceCount = 5;

        /// <summary>Whether this has been broken this attempt.</summary>
        public bool IsBroken { get; private set; }

        /// <summary>Centre of the intact stone.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>How much further than an impact's own reach this stone can be broken from.</summary>
        /// <remarks>
        /// The columns stand three metres behind the wall ring, where no blow ever lands. Without this they
        /// could only break from impacts the fight cannot produce; with it, a charge into the wall right in
        /// front of one brings it down.
        /// </remarks>
        public float ReachBonus => _reachBonus;

        /// <summary>Assigns the stone and its size. Used by the scene generator.</summary>
        /// <param name="intact">Every visible part of the intact stone.</param>
        /// <param name="size">Its dimensions, in metres, which the pieces are cut from.</param>
        /// <param name="pieceCount">How many pieces it breaks into.</param>
        /// <param name="reachBonus">Extra reach, for stone standing back from where blows land.</param>
        public void Bind(Renderer[] intact, Vector3 size, int pieceCount, float reachBonus = 0f)
        {
            _intact = intact ?? new Renderer[0];
            _size = size;
            _pieceCount = pieceCount;
            _reachBonus = reachBonus;
        }

        /// <summary>Breaks the stone, throwing its pieces away from the impact.</summary>
        /// <param name="impact">Where the blow landed.</param>
        /// <param name="pool">Where the pieces come from.</param>
        /// <param name="random">The seeded source of every scatter.</param>
        /// <param name="speed">Typical launch speed of a piece.</param>
        /// <returns>True when this broke now; false if it was already broken.</returns>
        public bool Break(Vector3 impact, DebrisPool pool, IRandomProvider random, float speed)
        {
            if (IsBroken)
            {
                return false;
            }

            IsBroken = true;
            SetVisible(false);

            if (pool == null || random == null)
            {
                return true;
            }

            Vector3 half = _size * 0.5f;

            // Pieces scaled so together they hold roughly the stone's volume, not n copies of it.
            float pieceScale = Mathf.Pow(1f / Mathf.Max(1, _pieceCount), 1f / 3f);

            for (int i = 0; i < _pieceCount; i++)
            {
                var local = new Vector3(
                    random.NextFloat(-half.x, half.x) * 0.6f,
                    random.NextFloat(-half.y, half.y) * 0.6f,
                    random.NextFloat(-half.z, half.z) * 0.6f);

                Vector3 start = transform.TransformPoint(local);
                Vector3 size = _size * (pieceScale * random.NextFloat(0.7f, 1f));
                var spin = new Vector3(random.NextFloat(-6f, 6f), random.NextFloat(-6f, 6f), random.NextFloat(-6f, 6f));

                pool.Launch(start, transform.rotation, size,
                    DestructionRules.PieceVelocity(random, impact, start, speed), spin);
            }

            return true;
        }

        /// <summary>Makes the stone whole again, for a retry.</summary>
        public void Restore()
        {
            IsBroken = false;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer part in _intact)
            {
                if (part != null)
                {
                    part.enabled = visible;
                }
            }
        }
    }
}
