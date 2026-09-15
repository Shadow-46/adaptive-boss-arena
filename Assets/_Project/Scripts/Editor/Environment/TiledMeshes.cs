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

        /// <summary>
        /// Loads or creates the wall above a pointed window: a slab whose underside is a Gothic arch.
        /// </summary>
        /// <remarks>
        /// Laid out like <see cref="Box"/> with size (thickness, height, span), centred, so it drops into the
        /// place of the flat-bottomed block it replaces. Rectangular window heads read as a warehouse; the
        /// pointed arch is most of what says cathedral from the fighting floor.
        /// </remarks>
        /// <param name="thickness">Wall thickness in metres, along x.</param>
        /// <param name="height">Slab height from the arch's springing to its top, along y.</param>
        /// <param name="span">Window width in metres, along z.</param>
        /// <param name="rise">Height of the arch's point above its springing; at least half the span.</param>
        /// <param name="metresPerTile">How many metres one repeat of the texture covers.</param>
        /// <returns>The mesh asset.</returns>
        public static Mesh PointedArchHead(float thickness, float height, float span, float rise, float metresPerTile)
        {
            string name = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "ArchHead_{0:0.##}x{1:0.##}x{2:0.##}_r{3:0.##}_{4:0.##}", thickness, height, span, rise, metresPerTile);
            string path = MeshFolder + "/" + name + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = BuildPointedArchHead(thickness, height, span, rise, metresPerTile);
            mesh.name = name;

            AssetAuthoring.EnsureFolderExists(MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);

            return mesh;
        }

        /// <summary>Height of a pointed arch's underside above its springing, at a point across its span.</summary>
        /// <remarks>
        /// Two circular arcs of equal radius, each centred on the far side of the middle, meeting in a point.
        /// The radius is the one that brings the point to the requested rise.
        /// </remarks>
        /// <param name="z">Position across the span, from minus half the span to plus half.</param>
        /// <param name="span">Window width.</param>
        /// <param name="rise">Height of the point.</param>
        /// <returns>The underside's height.</returns>
        public static float PointedArchHeight(float z, float span, float rise)
        {
            float half = span * 0.5f;
            float radius = (rise * rise + half * half) / span;
            float centre = radius - half;
            float fromCentre = Mathf.Abs(z) + centre;

            return Mathf.Sqrt(Mathf.Max(0f, radius * radius - fromCentre * fromCentre));
        }

        /// <summary>Builds the arch head without saving it; see <see cref="PointedArchHead"/>.</summary>
        /// <param name="thickness">Wall thickness in metres, along x.</param>
        /// <param name="height">Slab height from the arch's springing to its top, along y.</param>
        /// <param name="span">Window width in metres, along z.</param>
        /// <param name="rise">Height of the arch's point above its springing.</param>
        /// <param name="metresPerTile">How many metres one repeat of the texture covers.</param>
        /// <returns>A new mesh the caller owns.</returns>
        public static Mesh BuildPointedArchHead(float thickness, float height, float span, float rise, float metresPerTile)
        {
            rise = Mathf.Clamp(rise, span * 0.5f, height * 0.95f);
            float tile = Mathf.Max(0.01f, metresPerTile);
            const int Slices = 24;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            float bottom = -height * 0.5f, top = height * 0.5f, halfT = thickness * 0.5f;

            Vector3 Underside(int i, float x)
            {
                float z = -span * 0.5f + span * i / Slices;
                return new Vector3(x, bottom + PointedArchHeight(z, span, rise), z);
            }

            // The two faces of the wall: each slice is a quad from the arch up to the top.
            foreach (float x in new[] { halfT, -halfT })
            {
                var outward = new Vector3(Mathf.Sign(x), 0f, 0f);

                for (int i = 0; i < Slices; i++)
                {
                    Vector3 a = Underside(i, x), b = Underside(i + 1, x);
                    AddQuad(a, b, new Vector3(x, top, b.z), new Vector3(x, top, a.z), outward,
                        p => new Vector2(p.z / tile, p.y / tile), vertices, uvs, triangles);
                }
            }

            // The arch's underside, facing down into the opening and in toward its middle.
            float along = 0f;

            for (int i = 0; i < Slices; i++)
            {
                Vector3 a = Underside(i, halfT), b = Underside(i + 1, halfT);
                float length = Vector3.Distance(a, b);
                Vector3 midpoint = (a + b) * 0.5f;
                Vector3 away = new Vector3(0f, bottom - 1f, 0f) - new Vector3(0f, midpoint.y, midpoint.z * 0.5f);
                float start = along;

                AddQuad(a, b, Underside(i + 1, -halfT), Underside(i, -halfT), away,
                    p => new Vector2(Mathf.Approximately(p.z, a.z) ? start / tile : (start + length) / tile, (p.x + halfT) / tile),
                    vertices, uvs, triangles);

                along += length;
            }

            // The top, in case a high camera ever looks down on it.
            AddQuad(new Vector3(halfT, top, -span * 0.5f), new Vector3(halfT, top, span * 0.5f),
                new Vector3(-halfT, top, span * 0.5f), new Vector3(-halfT, top, -span * 0.5f), Vector3.up,
                p => new Vector2(p.z / tile, p.x / tile), vertices, uvs, triangles);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>Adds a quad with its own vertices, wound so its front faces the given direction.</summary>
        private static void AddQuad(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, System.Func<Vector3, Vector2> uv,
            System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<int> triangles)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(uv(a)); uvs.Add(uv(b)); uvs.Add(uv(c)); uvs.Add(uv(d));

            // Unity's front face is clockwise seen from outside, where Cross(b - a, c - a) points outward.
            bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) > 0f;

            if (outward)
            {
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            else
            {
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
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
