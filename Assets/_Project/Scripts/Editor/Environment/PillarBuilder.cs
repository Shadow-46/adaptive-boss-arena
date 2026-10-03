using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Environment
{
    /// <summary>
    /// Raises four broken stone pillars on the fighting floor: cover from the shockwaves, and bait for a charge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cathedral's own columns stand behind the wall ring, where neither fighter ever reaches them, so the
    /// stone in the room could only be scenery. These stand in the fight. A charge or leap driven into one is a
    /// lunge into something solid, which the boss already pays for - a stagger and lost poise - and the blow
    /// brings the pillar down. A pillar standing between the brute and the knight takes a shockwave for them.
    /// Both reward the player for reading where they stand, which is the read the fight is built around.
    /// </para>
    /// <para>
    /// The collider is a capsule, round in plan, so a body walking into one slides around it instead of
    /// catching on a corner - the boss steers straight at the knight and must not be pinned behind stone. It is
    /// its own uniform-scale component rather than a scaled primitive's, which collapses when scaled flat.
    /// Broken, a pillar leaves only a low stump and its collider goes with it, so it never becomes an invisible
    /// wall; every retry raises it again.
    /// </para>
    /// <para>
    /// On the diagonals, well clear of both spawns and of the line between them, so the opening exchange is never
    /// fought around stone.
    /// </para>
    /// </remarks>
    public static class PillarBuilder
    {
        /// <summary>Name of the object the pillars are gathered under.</summary>
        public const string RootName = "Pillars";

        /// <summary>How many pillars stand on the floor.</summary>
        public const int PillarCount = 4;

        /// <summary>How far from the centre they stand: inside the ring, with room to fight behind them.</summary>
        public const float RingRadius = 9f;

        /// <summary>Radius of a pillar's collider, which is also the stone a body slides around.</summary>
        public const float ColliderRadius = 0.8f;

        private const float ShaftWidth = 1.2f;
        private const float ShaftHeight = 4.2f;
        private const float StumpHeight = 0.3f;
        private const int PieceCount = 8;

        /// <summary>Extra reach, so a lunge stopped by the curve of the collider still breaks the stone it hit.</summary>
        private const float ReachBonus = 0.8f;

        /// <summary>Builds the pillars under the arena.</summary>
        /// <param name="arenaRoot">The arena's root.</param>
        public static void Build(Transform arenaRoot)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(arenaRoot, false);

            Material stone = MaterialLibrary.GetOrCreateTexturedLit("CathedralBlock", "medieval_blocks_02", CathedralBuilder.BlockTint);
            float step = 360f / PillarCount;

            for (int i = 0; i < PillarCount; i++)
            {
                float radians = (step * 0.5f + step * i) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * RingRadius;

                BuildPillar(root, i, position, stone);
            }
        }

        private static void BuildPillar(Transform root, int index, Vector3 position, Material stone)
        {
            var pillar = new GameObject($"Pillar_{index}");
            pillar.layer = Layers.Arena;
            pillar.transform.SetParent(root, false);
            pillar.transform.position = position;

            // Each pillar turned differently, so the four do not read as one shape stamped four times.
            Quaternion facing = Quaternion.Euler(0f, 25f + index * 37f, 0f);

            var collider = pillar.AddComponent<CapsuleCollider>();
            collider.radius = ColliderRadius;
            collider.height = ShaftHeight + StumpHeight;
            collider.center = Vector3.up * ((ShaftHeight + StumpHeight) * 0.5f);

            // The stump stays when the pillar breaks; only what stood on it comes down.
            Piece(pillar.transform, "Stump", new Vector3(1.7f, StumpHeight, 1.7f), stone,
                Vector3.up * (StumpHeight * 0.5f), facing);

            Renderer shaft = Piece(pillar.transform, "Shaft", new Vector3(ShaftWidth, ShaftHeight, ShaftWidth), stone,
                Vector3.up * (StumpHeight + ShaftHeight * 0.5f), facing);

            // Its top snapped off long ago: a tilted block where a capital stood, so it reads as ruin.
            Renderer broken = Piece(pillar.transform, "BrokenTop", new Vector3(1.4f, 0.5f, 1.3f), stone,
                Vector3.up * (StumpHeight + ShaftHeight + 0.15f), facing * Quaternion.Euler(9f, 0f, -12f));

            pillar.AddComponent<Destructible>().Bind(
                new[] { shaft, broken },
                new Vector3(ShaftWidth, ShaftHeight, ShaftWidth),
                PieceCount,
                ReachBonus,
                new Collider[] { collider });
        }

        private static Renderer Piece(Transform parent, string name, Vector3 size, Material material, Vector3 localPosition, Quaternion rotation)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = rotation;
            piece.AddComponent<MeshFilter>().sharedMesh = TiledMeshes.Box(size, 1.5f);

            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            return renderer;
        }
    }
}
