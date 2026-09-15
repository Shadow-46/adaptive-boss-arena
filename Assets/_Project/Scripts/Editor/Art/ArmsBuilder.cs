using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Builds the fighters' swords, the knight's shield and the brute's cleaver as real shapes, and mounts them on the rigs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A weapon made of stretched boxes is the single fastest way to make a character read as a prototype: from
    /// any angle it is a plank. These are built as meshes instead - a blade with a diamond cross-section that
    /// narrows to a point, so light breaks along its ridge; a heater shield curved like beaten metal - and cost
    /// a few hundred triangles each.
    /// </para>
    /// <para>
    /// Mounting is read off each rig's own bones rather than from fixed offsets, because a hand bone's axes are
    /// whatever the model's author chose: a sword runs across the knuckles, a shield rides the outside of the
    /// forearm with its point toward the hand.
    /// </para>
    /// </remarks>
    public static class ArmsBuilder
    {
        /// <summary>Folder the generated weapon meshes are saved in.</summary>
        public const string MeshFolder = "Assets/_Project/Meshes/Arms";

        /// <summary>Loads or creates a double-edged blade along +Z from its base, narrowing to a point.</summary>
        /// <param name="length">Blade length, base to point, in metres.</param>
        /// <param name="width">Width at the base.</param>
        /// <param name="thickness">Thickness along the central ridge.</param>
        /// <param name="tipFraction">Fraction of the length taken by the point.</param>
        /// <returns>The mesh asset.</returns>
        public static Mesh Blade(float length, float width, float thickness, float tipFraction)
        {
            string name = Invariant($"Blade_{length:0.##}x{width:0.###}x{thickness:0.###}_{tipFraction:0.##}");

            return LoadOrSave(name, () =>
            {
                float tipStart = length * (1f - tipFraction);

                // Stations along the blade: full width at the base, a slight taper to the start of the point,
                // then nothing at the point itself.
                var stations = new[]
                {
                    (z: 0f, halfWidth: width * 0.5f, halfThickness: thickness * 0.5f),
                    (z: tipStart, halfWidth: width * 0.44f, halfThickness: thickness * 0.45f),
                    (z: length, halfWidth: 0f, halfThickness: 0f)
                };

                var builder = new FacetedMesh(new Vector3(0f, 0f, length * 0.5f));

                for (int i = 0; i < stations.Length - 1; i++)
                {
                    Vector3[] a = Diamond(stations[i]);
                    Vector3[] b = Diamond(stations[i + 1]);

                    for (int k = 0; k < 4; k++)
                    {
                        builder.Quad(a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]);
                    }
                }

                Vector3[] baseRing = Diamond(stations[0]);
                builder.Quad(baseRing[0], baseRing[3], baseRing[2], baseRing[1]);

                return builder.Build();
            });
        }

        /// <summary>Loads or creates a heater shield in the XY plane, face toward +Z, point toward -Y.</summary>
        /// <param name="width">Width across the top.</param>
        /// <param name="height">Height, top edge to point.</param>
        /// <param name="thickness">Thickness at the rim.</param>
        /// <param name="bulge">How far the face domes forward at its centre.</param>
        /// <returns>The mesh asset.</returns>
        public static Mesh HeaterShield(float width, float height, float thickness, float bulge)
        {
            string name = Invariant($"HeaterShield_{width:0.##}x{height:0.##}x{thickness:0.###}_{bulge:0.###}");

            return LoadOrSave(name, () =>
            {
                List<Vector2> outline = HeaterOutline(width, height, 24);
                var builder = new FacetedMesh(Vector3.zero);

                var frontCentre = new Vector3(0f, 0f, thickness * 0.5f + bulge);
                var backCentre = new Vector3(0f, 0f, -thickness * 0.5f);

                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 p = outline[i];
                    Vector2 q = outline[(i + 1) % outline.Count];

                    var frontP = new Vector3(p.x, p.y, thickness * 0.5f);
                    var frontQ = new Vector3(q.x, q.y, thickness * 0.5f);
                    var backP = new Vector3(p.x, p.y, -thickness * 0.5f);
                    var backQ = new Vector3(q.x, q.y, -thickness * 0.5f);

                    builder.Triangle(frontCentre, frontP, frontQ);
                    builder.Triangle(backCentre, backQ, backP);
                    builder.Quad(frontP, backP, backQ, frontQ);
                }

                return builder.Build();
            });
        }

        /// <summary>
        /// How a weapon sits in a closed hand, from the rig's own finger bones.
        /// </summary>
        /// <remarks>
        /// A sword held in a fist runs across the knuckles, from the little finger's side out past the index
        /// finger, so that line is the blade's forward, and the blade's flat faces along the fingers. Weapons are
        /// built along +Z, which is why the result is a look rotation. Without finger bones the hand's own
        /// orientation is kept.
        /// </remarks>
        /// <param name="rig">The rig's humanoid Animator.</param>
        /// <param name="right">True for the right hand.</param>
        /// <returns>The world rotation for a weapon held in that hand.</returns>
        public static Quaternion GripRotation(Animator rig, bool right)
        {
            Transform hand = rig.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = rig.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform little = rig.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            Transform middle = rig.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);

            if (hand == null || index == null || little == null || middle == null)
            {
                return hand != null ? hand.rotation : Quaternion.identity;
            }

            Vector3 alongBlade = index.position - little.position;
            Vector3 alongFingers = middle.position - hand.position;

            return alongBlade.sqrMagnitude < 1e-6f || alongFingers.sqrMagnitude < 1e-6f
                ? hand.rotation
                : Quaternion.LookRotation(alongBlade.normalized, alongFingers.normalized);
        }

        /// <summary>Straps a shield to the outside of the left forearm, point toward the hand.</summary>
        /// <param name="rig">The rig's humanoid Animator.</param>
        /// <param name="material">The shield's material.</param>
        public static void MountShield(Animator rig, Material material)
        {
            Transform forearm = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform chest = rig.GetBoneTransform(HumanBodyBones.Chest);

            if (chest == null)
            {
                chest = rig.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (forearm == null || hand == null || chest == null)
            {
                return;
            }

            Vector3 along = (hand.position - forearm.position).normalized;
            Vector3 outward = Vector3.ProjectOnPlane(forearm.position - chest.position, along).normalized;
            float armLength = Vector3.Distance(forearm.position, hand.position);

            var shield = new GameObject("Shield");
            shield.transform.SetParent(forearm, true);
            shield.transform.position = forearm.position + along * (armLength * 0.55f) + outward * 0.07f;
            shield.transform.rotation = Quaternion.LookRotation(outward, -along);

            shield.AddComponent<MeshFilter>().sharedMesh = HeaterShield(0.52f, 0.72f, 0.035f, 0.05f);
            shield.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>Puts a heavy cleaver in a rig's right hand.</summary>
        /// <param name="rig">The rig's humanoid Animator.</param>
        /// <param name="bladeMaterial">The blade's material.</param>
        /// <param name="gripMaterial">The grip's material.</param>
        public static void MountCleaver(Animator rig, Material bladeMaterial, Material gripMaterial)
        {
            Transform hand = rig.GetBoneTransform(HumanBodyBones.RightHand);

            if (hand == null)
            {
                return;
            }

            var cleaver = new GameObject("Cleaver");
            cleaver.transform.SetParent(hand, false);
            cleaver.transform.localPosition = Vector3.zero;
            cleaver.transform.rotation = GripRotation(rig, right: true);

            // Undoes the hand's inherited scale, so the cleaver's size is its size in metres at any rig scale.
            float scale = 1f / Mathf.Max(0.01f, hand.lossyScale.x);

            AddPart(cleaver.transform, "Grip", gripMaterial, Blade(0.42f, 0.05f, 0.05f, 0.02f), new Vector3(0f, 0f, -0.28f) * scale, scale);
            AddPart(cleaver.transform, "Guard", gripMaterial, Blade(0.12f, 0.4f, 0.08f, 0.02f), new Vector3(0f, 0f, 0.1f) * scale, scale);
            AddPart(cleaver.transform, "Blade", bladeMaterial, Blade(1.45f, 0.24f, 0.05f, 0.1f), new Vector3(0f, 0f, 0.18f) * scale, scale);
        }

        private static void AddPart(Transform parent, string name, Material material, Mesh mesh, Vector3 localPosition, float scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = Vector3.one * scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector3[] Diamond((float z, float halfWidth, float halfThickness) station) => new[]
        {
            new Vector3(-station.halfWidth, 0f, station.z),
            new Vector3(0f, station.halfThickness, station.z),
            new Vector3(station.halfWidth, 0f, station.z),
            new Vector3(0f, -station.halfThickness, station.z)
        };

        /// <summary>A heater shield's outline: straight top, sides that bow out and sweep in to a point.</summary>
        private static List<Vector2> HeaterOutline(float width, float height, int segmentsPerSide)
        {
            float halfWidth = width * 0.5f;
            float top = height * 0.5f;
            float shoulder = height * 0.1f;
            var points = new List<Vector2> { new Vector2(-halfWidth, top), new Vector2(halfWidth, top) };

            for (int i = 0; i <= segmentsPerSide; i++)
            {
                float t = i / (float)segmentsPerSide;
                points.Add(new Vector2(halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)), Mathf.Lerp(shoulder, -top, t)));
            }

            for (int i = segmentsPerSide - 1; i >= 0; i--)
            {
                float t = i / (float)segmentsPerSide;
                points.Add(new Vector2(-halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)), Mathf.Lerp(shoulder, -top, t)));
            }

            return points;
        }

        private static Mesh LoadOrSave(string name, System.Func<Mesh> build)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = build();
            mesh.name = name;
            AssetAuthoring.EnsureFolderExists(MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);

            return mesh;
        }

        private static string Invariant(System.FormattableString text) => System.FormattableString.Invariant(text);

        /// <summary>
        /// Collects flat-shaded faces, each wound to face away from a point inside the solid.
        /// </summary>
        /// <remarks>
        /// Winding is decided per face against the interior point rather than by vertex order, so faces can be
        /// added in whatever order is natural to generate and still render from outside. Unity's front faces are
        /// clockwise as seen from outside, for which <c>Cross(b - a, c - a)</c> points outward.
        /// </remarks>
        private sealed class FacetedMesh
        {
            private readonly Vector3 _inside;
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public FacetedMesh(Vector3 inside)
            {
                _inside = inside;
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);

                if (normal.sqrMagnitude < 1e-12f)
                {
                    return;
                }

                bool alreadyOutward = Vector3.Dot(normal, (a + b + c) / 3f - _inside) > 0f;
                int start = _vertices.Count;

                _vertices.Add(a);
                _vertices.Add(alreadyOutward ? b : c);
                _vertices.Add(alreadyOutward ? c : b);
                _triangles.Add(start);
                _triangles.Add(start + 1);
                _triangles.Add(start + 2);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { vertices = _vertices.ToArray(), triangles = _triangles.ToArray() };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
