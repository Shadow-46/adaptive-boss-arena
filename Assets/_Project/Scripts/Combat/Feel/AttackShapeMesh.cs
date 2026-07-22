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
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];

            vertices[0] = centre;

            float startDegrees = -arcDegrees * 0.5f;
            float stepDegrees = arcDegrees / segments;

            for (int i = 0; i <= segments; i++)
            {
                float radians = (startDegrees + stepDegrees * i) * Mathf.Deg2Rad;

                // Local space has forward along +Z, so the sweep is measured from that axis.
                vertices[i + 1] = centre + new Vector3(
                    Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            return Finalise(vertices, triangles, "AttackArc");
        }

        /// <summary>Builds a flat rectangle lying on the ground.</summary>
        private static Mesh BuildBox(Vector3 halfExtents, Vector3 offset)
        {
            float x = halfExtents.x;
            float z = halfExtents.z;
            var centre = new Vector3(offset.x, 0f, offset.z);

            var vertices = new[]
            {
                centre + new Vector3(-x, 0f, -z),
                centre + new Vector3(-x, 0f, z),
                centre + new Vector3(x, 0f, z),
                centre + new Vector3(x, 0f, -z)
            };

            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            return Finalise(vertices, triangles, "AttackBox");
        }

        /// <summary>Assembles and prepares a mesh for rendering.</summary>
        private static Mesh Finalise(Vector3[] vertices, int[] triangles, string name)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
