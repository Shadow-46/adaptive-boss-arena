using AdaptiveBossArena.Core.ScriptableObjects;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Dimensions, spawn placement and camera framing for the arena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editor scene builder reads this asset to generate the arena, so the scene is a product of
    /// these values rather than a hand-placed layout that has quietly drifted from them. Changing the
    /// radius and regenerating is the whole workflow.
    /// </para>
    /// <para>
    /// Arena size is a combat-design value, not set dressing. It sets the ceiling on how far the
    /// player can retreat, which is what gives the boss's preferred-range adaptation something to
    /// work against.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ArenaConfig",
        menuName = "Adaptive Boss Arena/Game/Arena Config",
        order = 0)]
    public sealed class ArenaConfig : IdentifiableSO
    {
        [Header("Dimensions")]
        [SerializeField]
        [Range(8f, 40f)]
        [Tooltip("Radius of the fighting surface. Large enough to permit spacing play, small enough " +
                 "that the player can never simply run away.")]
        private float _radius = 16f;

        [SerializeField]
        [Range(1f, 8f)]
        [Tooltip("Height of the boundary wall.")]
        private float _wallHeight = 3f;

        [SerializeField]
        [Range(8, 64)]
        [Tooltip("Number of wall segments approximating the circle. Higher values are smoother and " +
                 "cost more colliders.")]
        private int _wallSegments = 32;

        [Header("Environment")]
        [SerializeField]
        [Tooltip("Optional set-dressing prefab instantiated at the arena centre. Empty today, so the " +
                 "arena is the procedural floor and wall ring; drop a dark-fantasy environment in to " +
                 "dress it, with the same collision kept underneath.")]
        private GameObject _environmentPrefab;

        [SerializeField]
        [Tooltip("When an environment prefab is supplied, hide the procedural floor and wall meshes " +
                 "but keep their colliders. Leave off to show both, on to let the imported set fully " +
                 "replace the placeholder look while the proven collision still bounds the fight.")]
        private bool _hideProceduralMeshesWithEnvironment = true;

        [Header("Spawns")]
        [SerializeField]
        [Tooltip("Player spawn position, relative to the arena centre.")]
        private Vector3 _playerSpawn = new Vector3(0f, 0f, -6f);

        [SerializeField]
        [Tooltip("Boss spawn position, relative to the arena centre.")]
        private Vector3 _bossSpawn = new Vector3(0f, 0f, 6f);

        [Header("Camera")]
        [SerializeField]
        [Range(6f, 40f)]
        [Tooltip("Height of the camera above the arena floor. Together with the distance below this " +
                 "sets how large the characters read; the two should keep the ratio implied by the " +
                 "pitch, or the camera will not be looking at what it is framing.")]
        private float _cameraHeight = 10.5f;

        [SerializeField]
        [Range(0f, 20f)]
        [Tooltip("How far back the camera sits from what it is framing. Zero looks straight down.")]
        private float _cameraDistance = 6f;

        [SerializeField]
        [Range(30f, 90f)]
        [Tooltip("Downward pitch of the camera. Around sixty degrees reads as top-down while still " +
                 "giving characters visible silhouettes.")]
        private float _cameraPitchDegrees = 60f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("Half-life of the camera's follow easing. Shorter is tighter and more responsive.")]
        private float _cameraFollowHalfLife = 0.18f;

        [SerializeField]
        [Range(0f, 0.6f)]
        [Tooltip("How far the camera biases from the player toward the boss, keeping both framed.")]
        private float _cameraBiasTowardBoss = 0.3f;

        /// <summary>Optional set-dressing prefab instantiated at the arena centre, or null.</summary>
        public GameObject EnvironmentPrefab => _environmentPrefab;

        /// <summary>Whether the procedural floor/wall meshes are hidden when an environment is present.</summary>
        public bool HideProceduralMeshesWithEnvironment => _hideProceduralMeshesWithEnvironment;

        /// <summary>Radius of the fighting surface.</summary>
        public float Radius => _radius;

        /// <summary>Height of the boundary wall.</summary>
        public float WallHeight => _wallHeight;

        /// <summary>Number of segments approximating the boundary circle.</summary>
        public int WallSegments => _wallSegments;

        /// <summary>Player spawn position relative to the arena centre.</summary>
        public Vector3 PlayerSpawn => _playerSpawn;

        /// <summary>Boss spawn position relative to the arena centre.</summary>
        public Vector3 BossSpawn => _bossSpawn;

        /// <summary>Height of the camera above the floor.</summary>
        public float CameraHeight => _cameraHeight;

        /// <summary>Distance the camera sits back from its framing target.</summary>
        public float CameraDistance => _cameraDistance;

        /// <summary>Downward pitch of the camera, in degrees.</summary>
        public float CameraPitchDegrees => _cameraPitchDegrees;

        /// <summary>Half-life of the camera's follow easing.</summary>
        public float CameraFollowHalfLife => _cameraFollowHalfLife;

        /// <summary>Bias of the camera from the player toward the boss.</summary>
        public float CameraBiasTowardBoss => _cameraBiasTowardBoss;

        /// <summary>Clamps a world position to remain inside the arena boundary.</summary>
        /// <param name="position">Position to clamp.</param>
        /// <param name="agentRadius">Radius of the agent being clamped, kept clear of the wall.</param>
        /// <returns>A position inside the boundary.</returns>
        public Vector3 ClampToArena(Vector3 position, float agentRadius = 0.5f)
        {
            float usableRadius = Mathf.Max(0f, _radius - agentRadius);
            Vector2 planar = new Vector2(position.x, position.z);

            if (planar.sqrMagnitude <= usableRadius * usableRadius)
            {
                return position;
            }

            planar = planar.normalized * usableRadius;
            return new Vector3(planar.x, position.y, planar.y);
        }
    }
}
