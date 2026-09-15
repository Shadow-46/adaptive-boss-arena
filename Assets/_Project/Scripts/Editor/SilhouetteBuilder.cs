using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Builds the two combatants' bodies out of primitives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A capsule is a shape, not a character. These assemble a readable silhouette instead — a
    /// helmed knight and a hulking horned brute — from the same primitives, because the project
    /// generates its assets from code and a jointed figure is only a handful more parts than a
    /// single capsule.
    /// </para>
    /// <para>
    /// Everything is parented under the visual root, which the procedural animator already moves as
    /// a whole. A multi-part body therefore inherits every existing lunge, lean, crouch and topple
    /// for nothing, and the character controller and hurtbox on the root above are untouched — so
    /// none of this changes a single frame of combat timing.
    /// </para>
    /// <para>
    /// Silhouette is the entire point. These are lit by one directional light against a dark floor,
    /// so what reads at a glance is the outline: the knight is narrow and upright with a crest, the
    /// brute is wide and low with horns. Colour does far less work than shape here.
    /// </para>
    /// </remarks>
    internal static class SilhouetteBuilder
    {
        /// <summary>
        /// Instantiates a rigged model under the visual root, if one has been supplied.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The route for real downloaded art. It is driven from a configuration asset rather than by
        /// adding the model to the prefab by hand, because these builders rewrite the entire
        /// hierarchy on every run — anything added directly is destroyed the next time the project is
        /// set up, which is precisely the trap the integration guide used to walk people into.
        /// </para>
        /// <para>
        /// Colliders are stripped from whatever arrives. A downloaded character routinely ships with
        /// them, and one inside the character controller would fight it.
        /// </para>
        /// </remarks>
        /// <param name="visualRoot">Visual root to parent the model under.</param>
        /// <param name="config">The character's animation config, carrying the rig, its controller and scale.</param>
        /// <param name="bodyMaterial">Material every surface of the rig is drawn with.</param>
        /// <param name="animator">The rig's Animator, for finding bones by role.</param>
        /// <returns>True when a rig was instantiated and the generated body should be skipped.</returns>
        public static bool TryBuildRig(
            Transform visualRoot,
            Combat.Feel.CharacterAnimationConfig config,
            Material bodyMaterial,
            out Animator animator)
        {
            animator = null;

            if (config == null || config.RigPrefab == null)
            {
                return false;
            }

            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(config.RigPrefab, visualRoot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * config.RigScale;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            animator = instance.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                animator.runtimeAnimatorController = config.AnimatorController;

                // The motors move the character; a clip must never. Root motion off, and animated even
                // when off screen so a pose never snaps when the camera swings back to it.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                FaceRigForward(animator, instance.transform, visualRoot);

                // Dormant until death. Built here so the knight and the brute get the same body from
                // the same table, scaled by the same number that sizes the rig.
                Art.RagdollBuilder.Build(animator, visualRoot.gameObject, config.RigScale);
            }

            // The imported model's own materials are placeholders and are not imported, so every
            // surface would otherwise render with the engine's default. The body takes the character's
            // generated material, which knows the render pipeline and the art direction.
            if (bodyMaterial != null)
            {
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length];

                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = bodyMaterial;
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            return true;
        }

        /// <summary>
        /// Turns an attached rig so its body faces the character's forward.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Art tools disagree about which way is forward. The Quaternius mannequin is authored facing -Z, and
        /// attaching it unrotated left both fighters turned away from where they were facing: the boss
        /// swung behind itself while its hits landed in front. The motors, the hitboxes and the boss's
        /// perception all use the character root's forward, so the body is the thing that must agree.
        /// </para>
        /// <para>
        /// Measured rather than assumed: the bind pose's hips give the body's facing, and the rig is turned
        /// to match, snapped to a quarter turn so a slightly asymmetric pelvis cannot tilt it off square.
        /// A differently authored model therefore lines up too, with nothing to remember to change.
        /// </para>
        /// </remarks>
        /// <param name="animator">The rig's humanoid Animator.</param>
        /// <param name="rig">The rig instance's root.</param>
        /// <param name="visualRoot">The character's visual root, whose forward is the character's.</param>
        private static void FaceRigForward(Animator animator, Transform rig, Transform visualRoot)
        {
            if (!animator.isHuman)
            {
                return;
            }

            animator.Rebind();

            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

            if (left == null || right == null)
            {
                return;
            }

            // A body facing +Z has its right side at +X, and +X cross +Y is +Z: right x up is the facing.
            Vector3 facing = Vector3.Cross(right.position - left.position, Vector3.up);
            facing.y = 0f;

            if (facing.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float yaw = Vector3.SignedAngle(facing, visualRoot.forward, Vector3.up);
            float snapped = Mathf.Round(yaw / 90f) * 90f;

            rig.localRotation = Quaternion.Euler(0f, snapped, 0f) * rig.localRotation;
        }

        /// <summary>The material a rigged knight's body is drawn with.</summary>
        /// <remarks>
        /// The licensed Paladin's own textured plate when present; otherwise the flat steel the CC0 mannequin
        /// has always worn, which has no textures of its own to show.
        /// </remarks>
        /// <returns>The knight's armour material.</returns>
        public static Material KnightRigMaterial() =>
            Art.LicensedArtMaterials.Knight() ??
            MaterialLibrary.GetOrCreateSurface("KnightArmour", ArmourColor, metallic: 0.85f, smoothness: 0.45f);

        /// <summary>The material a rigged brute's body is drawn with.</summary>
        /// <remarks>The licensed Warrok's own textured hide when present; otherwise the mannequin's flat hide.</remarks>
        /// <returns>The brute's hide material.</returns>
        public static Material BruteRigMaterial() =>
            Art.LicensedArtMaterials.Brute() ??
            MaterialLibrary.GetOrCreateSurface("BruteHide", HideColor, metallic: 0.05f, smoothness: 0.18f);

        /// <summary>
        /// Mounts the brute's glowing core on a rigged body's chest bone.
        /// </summary>
        /// <remarks>
        /// The core is the boss's facing indicator, the visible source of its phase aura and parry flash,
        /// and marks where its weak point is. A rigged body has no core of its own, so it is carried over
        /// onto the chest - where it moves with the body instead of floating where a primitive torso was.
        /// </remarks>
        /// <param name="animator">The rigged brute's Animator.</param>
        /// <param name="size">Diameter of the core, in world units.</param>
        public static void AttachBruteCore(Animator animator, float size)
        {
            Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;

            if (chest == null)
            {
                return;
            }

            Material core = MaterialLibrary.GetOrCreateSurface(
                "BruteCore", Color.black, metallic: 0f, smoothness: 0.6f, emission: CoreGlow);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Core";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(chest, false);

            // Local scale is divided out of the chest's world scale, so the core keeps its size however
            // the rig is scaled.
            float parentScale = Mathf.Max(0.0001f, chest.lossyScale.x);
            sphere.transform.localScale = Vector3.one * (size / parentScale);
            sphere.transform.position = chest.position + chest.root.forward * (size * 0.9f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = core;
        }

        /// <summary>Steel plate. Metallic enough for the single light to catch an edge.</summary>
        private static readonly Color ArmourColor = new Color(0.46f, 0.50f, 0.58f);

        /// <summary>Cloth and cloak, deliberately matte so it reads as a different material.</summary>
        private static readonly Color ClothColor = new Color(0.26f, 0.32f, 0.47f);

        /// <summary>
        /// Visor glow, above one so it blooms.
        /// </summary>
        /// <remarks>
        /// Doubles as the facing indicator the old white cube provided. A lit slit reads as a face
        /// without needing one, and it tells the player which way they are pointing.
        /// </remarks>
        private static readonly Color VisorGlow = new Color(1.7f, 1.15f, 0.45f);

        /// <summary>Dark, bloodied hide for the brute's mass.</summary>
        private static readonly Color HideColor = new Color(0.56f, 0.24f, 0.26f);

        /// <summary>Blackened iron for the brute's plating.</summary>
        private static readonly Color IronColor = new Color(0.33f, 0.30f, 0.33f);

        /// <summary>Old bone, for horns.</summary>
        private static readonly Color BoneColor = new Color(0.86f, 0.83f, 0.76f);

        /// <summary>The brute's core, hot enough to bloom and to explain the aura around it.</summary>
        private static readonly Color CoreGlow = new Color(2.3f, 0.55f, 0.30f);

        /// <summary>
        /// Builds an armoured, upright figure for the player.
        /// </summary>
        /// <param name="visualRoot">Visual root the parts are parented to.</param>
        /// <param name="height">Full standing height, matching the character controller.</param>
        /// <param name="radius">Body radius, matching the character controller.</param>
        public static void BuildKnight(Transform visualRoot, float height, float radius)
        {
            Material armour = MaterialLibrary.GetOrCreateSurface(
                "KnightArmour", ArmourColor, metallic: 0.85f, smoothness: 0.45f);

            Material cloth = MaterialLibrary.GetOrCreateSurface(
                "KnightCloth", ClothColor, metallic: 0f, smoothness: 0.12f);

            Material visor = MaterialLibrary.GetOrCreateSurface(
                "KnightVisor", Color.black, metallic: 0f, smoothness: 0.8f, emission: VisorGlow);

            float shoulder = radius * 0.78f;

            AddPart(visualRoot, "Torso", PrimitiveType.Cube, armour,
                new Vector3(0f, height * 0.63f, 0f),
                new Vector3(radius * 1.28f, height * 0.34f, radius * 0.82f));

            AddPart(visualRoot, "Hips", PrimitiveType.Cube, armour,
                new Vector3(0f, height * 0.44f, 0f),
                new Vector3(radius * 1.05f, height * 0.12f, radius * 0.74f));

            AddPart(visualRoot, "Head", PrimitiveType.Sphere, armour,
                new Vector3(0f, height * 0.89f, 0f),
                Vector3.one * (radius * 0.82f));

            // The crest is what makes the head read as a helm from directly above, which is the
            // angle this camera almost always sees it from.
            AddPart(visualRoot, "Crest", PrimitiveType.Cube, cloth,
                new Vector3(0f, height * 0.98f, -radius * 0.06f),
                new Vector3(radius * 0.12f, height * 0.07f, radius * 0.62f));

            // Forward-facing and lit: the facing marker, wearing a different hat.
            AddPart(visualRoot, "Visor", PrimitiveType.Cube, visor,
                new Vector3(0f, height * 0.89f, radius * 0.66f),
                new Vector3(radius * 0.46f, height * 0.028f, radius * 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "L" : "R";

                AddPart(visualRoot, $"Pauldron{suffix}", PrimitiveType.Sphere, armour,
                    new Vector3(shoulder * side, height * 0.75f, 0f),
                    Vector3.one * (radius * 0.62f));

                AddPart(visualRoot, $"Arm{suffix}", PrimitiveType.Cube, armour,
                    new Vector3(shoulder * side, height * 0.56f, 0f),
                    new Vector3(radius * 0.34f, height * 0.30f, radius * 0.34f));

                AddPart(visualRoot, $"Leg{suffix}", PrimitiveType.Cube, armour,
                    new Vector3(radius * 0.38f * side, height * 0.19f, 0f),
                    new Vector3(radius * 0.42f, height * 0.40f, radius * 0.46f));
            }

            // Hangs behind the shoulders and widens downward, which is most of what separates this
            // outline from a stack of boxes when it is moving.
            AddPart(visualRoot, "Cloak", PrimitiveType.Cube, cloth,
                new Vector3(0f, height * 0.56f, -radius * 0.52f),
                new Vector3(radius * 1.5f, height * 0.52f, radius * 0.12f));
        }

        /// <summary>
        /// Builds a wide, low, horned figure for the boss.
        /// </summary>
        /// <param name="visualRoot">Visual root the parts are parented to.</param>
        /// <param name="height">Full standing height, matching the character controller.</param>
        /// <param name="radius">Body radius, matching the character controller.</param>
        public static void BuildBrute(Transform visualRoot, float height, float radius)
        {
            Material hide = MaterialLibrary.GetOrCreateSurface(
                "BruteHide", HideColor, metallic: 0.05f, smoothness: 0.18f);

            Material iron = MaterialLibrary.GetOrCreateSurface(
                "BruteIron", IronColor, metallic: 0.9f, smoothness: 0.35f);

            Material bone = MaterialLibrary.GetOrCreateSurface(
                "BruteBone", BoneColor, metallic: 0f, smoothness: 0.25f);

            Material core = MaterialLibrary.GetOrCreateSurface(
                "BruteCore", Color.black, metallic: 0f, smoothness: 0.6f, emission: CoreGlow);

            float shoulder = radius * 0.82f;

            AddPart(visualRoot, "Torso", PrimitiveType.Cube, hide,
                new Vector3(0f, height * 0.62f, 0f),
                new Vector3(radius * 1.62f, height * 0.36f, radius * 1.05f));

            AddPart(visualRoot, "Hips", PrimitiveType.Cube, hide,
                new Vector3(0f, height * 0.38f, 0f),
                new Vector3(radius * 1.3f, height * 0.16f, radius * 0.95f));

            // Sunk between the shoulders rather than sitting on top of them. A low head is the
            // single clearest way to read something as a brute rather than a person.
            AddPart(visualRoot, "Head", PrimitiveType.Sphere, hide,
                new Vector3(0f, height * 0.83f, radius * 0.18f),
                Vector3.one * (radius * 0.72f));

            // The one part that must read from every angle, so it is the boss's silhouette signature.
            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "L" : "R";

                var horn = AddPart(visualRoot, $"Horn{suffix}", PrimitiveType.Cube, bone,
                    new Vector3(radius * 0.34f * side, height * 0.94f, radius * 0.1f),
                    new Vector3(radius * 0.14f, height * 0.20f, radius * 0.14f));

                horn.transform.localRotation = Quaternion.Euler(-18f, 0f, 26f * side);

                AddPart(visualRoot, $"Shoulder{suffix}", PrimitiveType.Sphere, iron,
                    new Vector3(shoulder * side, height * 0.72f, 0f),
                    Vector3.one * (radius * 0.86f));

                AddPart(visualRoot, $"Arm{suffix}", PrimitiveType.Cube, hide,
                    new Vector3(shoulder * 1.05f * side, height * 0.45f, 0f),
                    new Vector3(radius * 0.44f, height * 0.38f, radius * 0.44f));

                AddPart(visualRoot, $"Leg{suffix}", PrimitiveType.Cube, hide,
                    new Vector3(radius * 0.46f * side, height * 0.15f, 0f),
                    new Vector3(radius * 0.56f, height * 0.32f, radius * 0.62f));
            }

            // Forward-facing, and the reason the phase aura has a visible source. It is also the
            // facing indicator, replacing the yellow cube that used to do the job.
            AddPart(visualRoot, "Core", PrimitiveType.Sphere, core,
                new Vector3(0f, height * 0.63f, radius * 0.56f),
                Vector3.one * (radius * 0.34f));
        }

        /// <summary>
        /// Creates one body part.
        /// </summary>
        /// <remarks>
        /// The collider is destroyed on every part without exception. Collision belongs to the
        /// character controller on the root and damage to the hurtbox beside it; a stray collider
        /// down here would fight both. Note also that a sphere and a cube are one unit across while
        /// a capsule and a cylinder are two units tall — the scales above are written in those terms
        /// deliberately, which is why nothing here uses a cylinder.
        /// </remarks>
        private static GameObject AddPart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Material material,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Object.DestroyImmediate(part.GetComponent<Collider>());

            if (material != null)
            {
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return part;
        }
    }
}
