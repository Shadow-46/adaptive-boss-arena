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
        [Tooltip("The intact stone, hidden when this breaks.")]
        private Renderer _intact;

        [SerializeField]
        [Tooltip("Dimensions of the intact stone, in metres, which the pieces are cut from.")]
        private Vector3 _size = Vector3.one;

        [SerializeField]
        [Range(1, 12)]
        [Tooltip("How many pieces it breaks into.")]
        private int _pieceCount = 5;

        /// <summary>Whether this has been broken this attempt.</summary>
        public bool IsBroken { get; private set; }

        /// <summary>Centre of the intact stone.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>Assigns the stone and its size. Used by the scene generator.</summary>
        /// <param name="intact">The intact stone's renderer.</param>
        /// <param name="size">Its dimensions, in metres.</param>
        /// <param name="pieceCount">How many pieces it breaks into.</param>
        public void Bind(Renderer intact, Vector3 size, int pieceCount)
        {
            _intact = intact;
            _size = size;
            _pieceCount = pieceCount;
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

            if (_intact != null)
            {
                _intact.enabled = false;
            }

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

            if (_intact != null)
            {
                _intact.enabled = true;
            }
        }
    }
}
