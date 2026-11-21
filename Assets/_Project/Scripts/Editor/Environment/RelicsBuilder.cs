using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Environment
{
    /// <summary>
    /// Dresses the cathedral with what a commander's hall would hold: the blades of every challenger who fell
    /// before the knight, planted in the stone, and chains hanging from the dark between the columns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arena was handsome but empty, a ruin with nothing in it that said whose hall it was. The sword field
    /// tells the story without a word: many came, none left. It reuses the weapon models the knight already
    /// carries, so it costs no new art and every blade matches the fight's own scale.
    /// </para>
    /// <para>
    /// Everything sits outside the fighting surface, carries no collider and is static-batched with the rest of
    /// the ruin, so none of it can block a blow, the camera or a telegraph, and the browser pays a few draw
    /// calls for it.
    /// </para>
    /// </remarks>
    public static class RelicsBuilder
    {
        /// <summary>Name of the planted swords' parent.</summary>
        public const string SwordFieldName = "FallenBlades";

        /// <summary>Name of the hanging chains' parent.</summary>
        public const string ChainsName = "HangingChains";

        /// <summary>How many fallen challengers' blades stand in the stone.</summary>
        public const int SwordCount = 30;

        /// <summary>How many chains hang between the columns.</summary>
        public const int ChainCount = 8;

        private const uint LayoutSeed = 20261122u;

        /// <summary>Nearest a blade stands to the fighting surface's edge, so none intrudes on the fight.</summary>
        private const float SwordInnerGap = 0.8f;

        /// <summary>Furthest out a blade stands.</summary>
        private const float SwordOuterGap = 2.8f;

        /// <summary>How far a blade is driven into the floor, in metres.</summary>
        private const float SwordSink = 0.18f;

        /// <summary>Least and most a planted blade leans from upright, in degrees.</summary>
        private const float SwordMinLean = 4f;
        private const float SwordMaxLean = 22f;

        /// <summary>Blades stand in clusters, like the places the bravest fell; this many.</summary>
        private const int SwordClusters = 7;

        /// <summary>Angular spread of one cluster, in degrees.</summary>
        private const float ClusterSpread = 16f;

        /// <summary>Turns the clusters off the column angles so no blade stands inside a column's foot.</summary>
        private const float ClusterOffsetDegrees = 11f;

        /// <summary>Height the chains hang from: the dark under the vault.</summary>
        private const float ChainTop = 15f;

        /// <summary>Shortest and longest chain, in metres.</summary>
        private const float ChainMinLength = 4f;
        private const float ChainMaxLength = 7.5f;

        /// <summary>Distance between link centres.</summary>
        private const float LinkPitch = 0.2f;

        /// <summary>Distinct chain lengths generated as meshes; chains pick among them.</summary>
        private const int ChainVariants = 3;

        private static readonly Color ChainColor = new Color(0.16f, 0.14f, 0.13f);
        private static readonly Color RustColor = new Color(0.34f, 0.22f, 0.15f);

        private static readonly string[] BladePrefabs =
        {
            "Assets/_Project/Prefabs/Weapons/WeaponBladeModel.prefab",
            "Assets/_Project/Prefabs/Weapons/WeaponGreatswordModel.prefab",
        };

        /// <summary>Builds the sword field and the chains under the cathedral root.</summary>
        /// <param name="root">The cathedral's root.</param>
        /// <param name="radius">The fighting surface's radius.</param>
        /// <param name="columnRingRadius">Where the columns stand, so the chains hang between them.</param>
        /// <param name="columnCount">How many columns there are.</param>
        public static void Build(Transform root, float radius, float columnRingRadius, int columnCount)
        {
            BuildSwordField(root, radius);
            BuildChains(root, columnRingRadius, columnCount);
        }

        private static void BuildSwordField(Transform root, float radius)
        {
            var field = new GameObject(SwordFieldName).transform;
            field.SetParent(root, false);

            var models = new List<GameObject>();

            foreach (string path in BladePrefabs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model != null)
                {
                    models.Add(model);
                }
            }

            if (models.Count == 0)
            {
                Debug.LogWarning("[Adaptive Boss Arena] No weapon models to plant; build the prefabs first.");
                return;
            }

            Material rust = MaterialLibrary.GetOrCreateSurface("RustedBlade", RustColor, metallic: 0.6f, smoothness: 0.22f);
            var random = new XorShiftRandomProvider(LayoutSeed);

            for (int i = 0; i < SwordCount; i++)
            {
                float clusterAngle = (i % SwordClusters) * (360f / SwordClusters) + ClusterOffsetDegrees;
                float angle = clusterAngle + random.NextFloat(-ClusterSpread, ClusterSpread);
                float distance = radius + random.NextFloat(SwordInnerGap, SwordOuterGap);

                GameObject blade = Object.Instantiate(models[random.NextInt(0, models.Count)], field);
                blade.name = $"Blade_{i:D2}";
                Plant(blade.transform, angle, distance, random);
                Dress(blade, rust);
            }
        }

        /// <summary>Stands a blade point-down in the stone, leaning, turned to a random facing.</summary>
        private static void Plant(Transform blade, float angle, float distance, XorShiftRandomProvider random)
        {
            // The models point along their forward axis, grip at the origin, so pointing forward down plants the tip.
            Vector3 facing = Quaternion.Euler(0f, random.NextFloat(0f, 360f), 0f) * Vector3.forward;
            Quaternion upright = Quaternion.LookRotation(Vector3.down, facing);
            Vector3 leanAxis = Quaternion.Euler(0f, random.NextFloat(0f, 360f), 0f) * Vector3.right;
            Quaternion tilt = Quaternion.AngleAxis(random.NextFloat(SwordMinLean, SwordMaxLean), leanAxis);
            blade.rotation = tilt * upright;

            float length = BladeLength(blade);
            blade.position = Ring(angle, distance) - blade.forward * (length - SwordSink);
        }

        /// <summary>The model's reach along its forward axis, from its meshes' bounds.</summary>
        private static float BladeLength(Transform blade)
        {
            float reach = 0f;

            foreach (MeshFilter filter in blade.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 toBlade = blade.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents, Corner(corner));
                    reach = Mathf.Max(reach, toBlade.MultiplyPoint3x4(local).z);
                }
            }

            return reach > 0f ? reach : 1f;
        }

        private static Vector3 Corner(int index) =>
            new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f);

        /// <summary>
        /// Strips a planted blade of everything that belongs to a held weapon: colliders, lights, the character
        /// lighting layer, and the bright steel, which rusts.
        /// </summary>
        private static void Dress(GameObject blade, Material rust)
        {
            foreach (Collider collider in blade.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (Light light in blade.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light.gameObject);
            }

            foreach (Renderer renderer in blade.GetComponentsInChildren<Renderer>(true))
            {
                // Held weapons are lit by the character lights; a relic belongs to the room.
                renderer.renderingLayerMask = 1u;

                var materials = new Material[renderer.sharedMaterials.Length];

                for (int m = 0; m < materials.Length; m++)
                {
                    materials[m] = rust;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static void BuildChains(Transform root, float columnRingRadius, int columnCount)
        {
            var chains = new GameObject(ChainsName).transform;
            chains.SetParent(root, false);

            Material iron = MaterialLibrary.GetOrCreateSurface("ChainIron", ChainColor, metallic: 0.7f, smoothness: 0.3f);
            var random = new XorShiftRandomProvider(LayoutSeed + 1u);
            var meshes = new Mesh[ChainVariants];

            for (int v = 0; v < ChainVariants; v++)
            {
                float length = Mathf.Lerp(ChainMinLength, ChainMaxLength, v / (float)(ChainVariants - 1));
                meshes[v] = GetOrCreateChainMesh(Mathf.RoundToInt(length / LinkPitch));
            }

            float step = 360f / Mathf.Max(1, columnCount);

            for (int i = 0; i < ChainCount; i++)
            {
                // Halfway between two columns, a little inside the ring so they hang over the edge of the fight.
                float angle = i * (360f / ChainCount) + step * 0.5f + random.NextFloat(-6f, 6f);
                float distance = columnRingRadius - random.NextFloat(0.5f, 2f);

                var chain = new GameObject($"Chain_{i:D2}");
                chain.transform.SetParent(chains, false);
                chain.transform.SetPositionAndRotation(
                    Ring(angle, distance) + Vector3.up * ChainTop,
                    Quaternion.Euler(0f, random.NextFloat(0f, 360f), 0f));

                chain.AddComponent<MeshFilter>().sharedMesh = meshes[random.NextInt(0, ChainVariants)];
                var renderer = chain.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = iron;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// One chain as a single mesh: links hanging straight down from the origin, each turned a quarter from
        /// the last as real links are.
        /// </summary>
        private static Mesh GetOrCreateChainMesh(int links)
        {
            string path = $"{TiledMeshes.MeshFolder}/ChainLinks_{links}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                return existing;
            }

            Mesh link = LinkMesh();
            var parts = new CombineInstance[links];

            for (int i = 0; i < links; i++)
            {
                parts[i] = new CombineInstance
                {
                    mesh = link,
                    transform = Matrix4x4.TRS(
                        Vector3.down * (i * LinkPitch), Quaternion.Euler(0f, (i % 2) * 90f, 0f), Vector3.one)
                };
            }

            var mesh = new Mesh { name = $"ChainLinks_{links}" };
            mesh.CombineMeshes(parts, mergeSubMeshes: true, useMatrices: true);
            mesh.RecalculateBounds();
            Object.DestroyImmediate(link);

            AssetAuthoring.EnsureFolderExists(TiledMeshes.MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>One oval link, upright in the XY plane: a low-poly torus stretched along Y.</summary>
        private static Mesh LinkMesh()
        {
            const int Around = 10;
            const int Tube = 5;
            const float RingRadius = 0.065f;
            const float Stretch = 1.6f;
            const float Thickness = 0.018f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int a = 0; a <= Around; a++)
            {
                float u = a / (float)Around * Mathf.PI * 2f;
                var centre = new Vector3(Mathf.Cos(u) * RingRadius, Mathf.Sin(u) * RingRadius * Stretch, 0f);
                var outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);

                for (int t = 0; t <= Tube; t++)
                {
                    float w = t / (float)Tube * Mathf.PI * 2f;
                    vertices.Add(centre + (outward * Mathf.Cos(w) + Vector3.forward * Mathf.Sin(w)) * Thickness);
                }
            }

            for (int a = 0; a < Around; a++)
            {
                for (int t = 0; t < Tube; t++)
                {
                    int i0 = a * (Tube + 1) + t;
                    int i1 = i0 + Tube + 1;
                    triangles.AddRange(new[] { i0, i1, i0 + 1, i0 + 1, i1, i1 + 1 });
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector3 Ring(float angleDegrees, float distance)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians) * distance, 0f, Mathf.Sin(radians) * distance);
        }
    }
}
