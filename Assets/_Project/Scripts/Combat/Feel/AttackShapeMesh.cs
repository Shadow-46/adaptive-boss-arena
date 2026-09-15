using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// Builds flat ground meshes matching an attack's hit volume.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are generated from the same numbers hit detection uses, so what the player sees is
    /// exactly what will be tested. A visual that merely approximates the hitbox is worse than none,
    /// because it teaches the player a range that is not real.
    /// </para>
    /// <para>
    /// Meshes are flat and lie on the ground rather than being volumetric. From a top-down camera
    /// the ground plane is the only thing whose extent is readable at a glance, and a floating
    /// volume would obscure both combatants.
    /// </para>
    /// </remarks>
    public static class AttackShapeMesh
    {
        /// <summary>Segments used to approximate a full circle. Higher is smoother.</summary>
        private const int SegmentsPerCircle = 48;

        /// <summary>
        /// Rings from the shape's centre to its edge, as a fraction of the way out and the opacity there.
        /// </summary>
        /// <remarks>
        /// Faint inside, bright in a narrow band just short of the edge, softening at the edge itself. A filled
        /// shape at one opacity read as a coloured disc lying on the floor; a lit rim reads as a boundary,
        /// and still shows exactly where the danger ends.
        /// </remarks>
        public static readonly (float Scale, float Alpha)[] Rings =
        {
            (0f, 0.08f), (0.78f, 0.16f), (0.93f, 1f), (1f, 0.35f)
        };

        /// <summary>Builds a mesh matching the attack's shape, centred on the attacker's origin.</summary>
        /// <param name="attack">Attack supplying the shape and dimensions.</param>
        /// <returns>A newly created mesh. The caller owns it.</returns>
        public static Mesh Build(AttackDefinition attack)
        {
            if (attack == null)
            {
                return null;
            }

            switch (attack.Shape)
            {
                case AttackShape.Box:
                    return BuildBox(attack.BoxHalfExtents, attack.Offset);

                case AttackShape.Sphere:
                    return BuildFan(attack.Range, 360f, Vector3.zero);

                default:
                    // An arc is centred on the attacker, matching how hit detection tests it.
                    return BuildFan(attack.Range, attack.ArcDegrees, Vector3.zero);
            }
        }

        /// <summary>Builds a wedge lying on the ground, opening along local forward.</summary>
        private static Mesh BuildFan(float radius, float arcDegrees, Vector3 centre)
        {
            arcDegrees = Mathf.Clamp(arcDegrees, 1f, 360f);

            int segments = Mathf.Max(3, Mathf.RoundToInt(SegmentsPerCircle * (arcDegrees / 360f)));
            bool fullCircle = arcDegrees >= 360f;

            // A wedge's outline runs along the arc and back through its point; a circle's is the arc alone.
            int outlineCount = fullCircle ? segments : segments + 2;
            var outline = new Vector3[outlineCount];

            float startDegrees = -arcDegrees * 0.5f;
            float stepDegrees = arcDegrees / segments;
            int arcPoints = fullCircle ? segments : segments + 1;

            for (int i = 0; i < arcPoints; i++)
            {
                float radians = (startDegrees + stepDegrees * i) * Mathf.Deg2Rad;

                // Local space has forward along +Z, so the sweep is measured from that axis.
                outline[i] = centre + new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
            }

            Vector3 middle = centre;

            if (!fullCircle)
            {
                outline[outlineCount - 1] = centre;

                // Rings shrink toward the wedge's middle rather than its point, so the rim follows both straight sides.
                middle = centre + new Vector3(0f, 0f, radius * 0.5f);
            }

            return BuildRinged(outline, middle, "AttackArc");
        }

        /// <summary>Builds a flat rectangle lying on the ground.</summary>
        private static Mesh BuildBox(Vector3 halfExtents, Vector3 offset)
        {
            float x = halfExtents.x;
            float z = halfExtents.z;
            var centre = new Vector3(offset.x, 0f, offset.z);

            var outline = new[]
            {
                centre + new Vector3(-x, 0f, -z),
                centre + new Vector3(-x, 0f, z),
                centre + new Vector3(x, 0f, z),
                centre + new Vector3(x, 0f, -z)
            };

            return BuildRinged(outline, centre, "AttackBox");
        }

        /// <summary>
        /// Fills a closed outline with rings shrunk toward a middle point, each carrying its opacity as vertex alpha.
        /// </summary>
        private static Mesh BuildRinged(Vector3[] outline, Vector3 middle, string name)
        {
            int n = outline.Length;
            var vertices = new Vector3[n * Rings.Length];
            var colours = new Color[vertices.Length];

            for (int ring = 0; ring < Rings.Length; ring++)
            {
                for (int i = 0; i < n; i++)
                {
                    vertices[ring * n + i] = Vector3.Lerp(middle, outline[i], Rings[ring].Scale);
                    colours[ring * n + i] = new Color(1f, 1f, 1f, Rings[ring].Alpha);
                }
            }

            var triangles = new int[(Rings.Length - 1) * n * 6];
            int t = 0;

            for (int ring = 0; ring < Rings.Length - 1; ring++)
            {
                for (int i = 0; i < n; i++)
                {
                    int inner = ring * n + i, innerNext = ring * n + (i + 1) % n;
                    int outer = inner + n, outerNext = innerNext + n;

                    // Winding is left to chance by the outline's direction; the overlay material draws both faces.
                    triangles[t++] = inner; triangles[t++] = outer; triangles[t++] = outerNext;
                    triangles[t++] = inner; triangles[t++] = outerNext; triangles[t++] = innerNext;
                }
            }

            return Finalise(vertices, colours, triangles, name);
        }

        /// <summary>Assembles and prepares a mesh for rendering.</summary>
        private static Mesh Finalise(Vector3[] vertices, Color[] colours, int[] triangles, string name)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = vertices,
                colors = colours,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
