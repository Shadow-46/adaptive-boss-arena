using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Environment
{
    /// <summary>
    /// Box meshes whose texture repeats by world size rather than stretching to fit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's cube maps a whole texture onto every face, so a 14-metre column and a half-metre chunk of
    /// rubble show the same bricks at wildly different sizes, and a long wall shows each brick stretched
    /// into a plank. A shared material cannot fix that, because tiling on a material applies to every
    /// object using it. Scaling the UVs by each face's real size can, and lets the whole cathedral share
    /// three materials.
    /// </para>
    /// <para>
    /// Saved as assets, one per distinct size, so scenes reference them rather than embedding copies,
    /// and regenerating the scene does not multiply them.
    /// </para>
    /// </remarks>
    public static class TiledMeshes
    {
        /// <summary>Folder the generated meshes are saved in.</summary>
        public const string MeshFolder = "Assets/_Project/Meshes";

        /// <summary>Loads or creates a centred box of the given size.</summary>
        /// <param name="size">Width, height and depth in metres.</param>
        /// <param name="metresPerTile">How many metres one repeat of the texture covers.</param>
        /// <returns>The mesh asset.</returns>
        public static Mesh Box(Vector3 size, float metresPerTile)
        {
            string name = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "TiledBox_{0:0.##}x{1:0.##}x{2:0.##}_{3:0.##}", size.x, size.y, size.z, metresPerTile);
            string path = MeshFolder + "/" + name + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = BuildBox(size, Mathf.Max(0.01f, metresPerTile));
            mesh.name = name;

            AssetAuthoring.EnsureFolderExists(MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);

            return mesh;
        }

        private static Mesh BuildBox(Vector3 size, float tile)
        {
            Vector3 half = size * 0.5f;
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            // Each face: its normal, then the axis the texture's u runs along (normal x v, so the face is
            // wound to be seen from outside), then v.
            AddFace(0, Vector3.right, Vector3.forward, Vector3.up, half, size.z, size.y, tile, vertices, normals, uvs, triangles);
            AddFace(1, Vector3.left, Vector3.back, Vector3.up, half, size.z, size.y, tile, vertices, normals, uvs, triangles);
            AddFace(2, Vector3.up, Vector3.right, Vector3.forward, half, size.x, size.z, tile, vertices, normals, uvs, triangles);
            AddFace(3, Vector3.down, Vector3.right, Vector3.back, half, size.x, size.z, tile, vertices, normals, uvs, triangles);
            AddFace(4, Vector3.forward, Vector3.left, Vector3.up, half, size.x, size.y, tile, vertices, normals, uvs, triangles);
            AddFace(5, Vector3.back, Vector3.right, Vector3.up, half, size.x, size.y, tile, vertices, normals, uvs, triangles);

            var mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles
            };

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddFace(
            int face, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector3 half,
            float uLength, float vLength, float tile,
            Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles)
        {
            int v = face * 4;
            Vector3 centre = Vector3.Scale(normal, half);
            Vector3 u = Vector3.Scale(uAxis, half);
            Vector3 w = Vector3.Scale(vAxis, half);
            float uTiles = uLength / tile;
            float vTiles = vLength / tile;

            vertices[v] = centre - u - w;
            vertices[v + 1] = centre + u - w;
            vertices[v + 2] = centre + u + w;
            vertices[v + 3] = centre - u + w;

            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(uTiles, 0f);
            uvs[v + 2] = new Vector2(uTiles, vTiles);
            uvs[v + 3] = new Vector2(0f, vTiles);

            for (int i = 0; i < 4; i++)
            {
                normals[v + i] = normal;
            }

            // Clockwise as seen from outside, which is Unity's front face.
            int t = face * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }
    }
}
