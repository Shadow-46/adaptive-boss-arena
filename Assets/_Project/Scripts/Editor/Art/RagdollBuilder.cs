using System.Collections.Generic;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Builds a ragdoll onto a humanoid rig from a table, so no body is ever set up by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eleven bodies: hips, chest, head, both upper and lower arms, both upper and lower legs. Enough
    /// for a body to fold at the waist, knees and elbows, few enough to stay well inside the WebGL physics
    /// budget for two corpses. Bones are found by humanoid role, so the same table fits the knight and
    /// the scaled brute, and every size is measured from the rig itself rather than typed in.
    /// </para>
    /// <para>
    /// Everything is built dormant - kinematic bodies with their colliders switched off - and handed to
    /// a <see cref="RagdollActivator"/>, which wakes it on death.
    /// </para>
    /// </remarks>
    public static class RagdollBuilder
    {
        /// <summary>Total mass of a body at a rig scale of one, in kilograms.</summary>
        private const float BaseMass = 70f;

        /// <summary>
        /// The bones, hips and chest first - the activator gives those two the whole blow. Joint limits are
        /// in degrees and deliberately tight: a loose ragdoll folds into poses no body could take, which
        /// reads as a rag rather than as someone falling.
        /// </summary>
        private static readonly Part[] Parts =
        {
            new Part(HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.LastBone, Shape.Box, 0.20f, 0f, 0f, 0f),
            new Part(HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Hips, Shape.Box, 0.22f, -20f, 20f, 15f),
            new Part(HumanBodyBones.Head, HumanBodyBones.LastBone, HumanBodyBones.Chest, Shape.Sphere, 0.08f, -40f, 25f, 30f),
            new Part(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.Chest, Shape.Capsule, 0.04f, -70f, 10f, 50f),
            new Part(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.Chest, Shape.Capsule, 0.04f, -70f, 10f, 50f),
            new Part(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperArm, Shape.Capsule, 0.03f, -90f, 0f, 5f),
            new Part(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightUpperArm, Shape.Capsule, 0.03f, -90f, 0f, 5f),
            new Part(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.Hips, Shape.Capsule, 0.11f, -20f, 70f, 30f),
            new Part(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.Hips, Shape.Capsule, 0.11f, -20f, 70f, 30f),
            new Part(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftUpperLeg, Shape.Capsule, 0.07f, -80f, 0f, 5f),
            new Part(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightUpperLeg, Shape.Capsule, 0.07f, -80f, 0f, 5f)
        };

        private enum Shape
        {
            Box,
            Capsule,
            Sphere
        }

        /// <summary>The number of physical bones a complete ragdoll has.</summary>
        public static int BodyCount => Parts.Length;

        /// <summary>Builds the ragdoll onto a rig and attaches the activator to the visual root.</summary>
        /// <param name="animator">The rig's humanoid Animator.</param>
        /// <param name="visualRoot">The object the character's presentation components live on.</param>
        /// <param name="rigScale">The rig's uniform scale, which scales the mass.</param>
        /// <returns>True when every bone was found and built.</returns>
        public static bool Build(Animator animator, GameObject visualRoot, float rigScale)
        {
            if (animator == null || !animator.isHuman)
            {
                return false;
            }

            var bodies = new Dictionary<HumanBodyBones, Rigidbody>();
            var ordered = new List<Rigidbody>();
            float mass = BaseMass * rigScale * rigScale * rigScale;

            foreach (Part part in Parts)
            {
                Transform bone = animator.GetBoneTransform(part.Bone);

                // Some rigs have no separate chest; the upper spine stands in.
                if (bone == null && part.Bone == HumanBodyBones.Chest)
                {
                    bone = animator.GetBoneTransform(HumanBodyBones.Spine);
                }

                if (bone == null)
                {
                    Debug.LogWarning("[Adaptive Boss Arena] Rig has no " + part.Bone + "; the ragdoll was not built.");
                    return false;
                }

                bone.gameObject.layer = Layers.Ragdoll;

                Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = mass * part.MassFraction;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;

                AddCollider(animator, part, bone).enabled = false;

                if (part.Parent != HumanBodyBones.LastBone && bodies.TryGetValue(part.Parent, out Rigidbody parent))
                {
                    AddJoint(bone.gameObject, parent, part);
                }

                bodies[part.Bone] = body;
                ordered.Add(body);
            }

            var activator = visualRoot.GetComponent<RagdollActivator>();

            if (activator == null)
            {
                activator = visualRoot.AddComponent<RagdollActivator>();
            }

            activator.Bind(ordered.ToArray());

            return true;
        }

        private static Collider AddCollider(Animator animator, Part part, Transform bone)
        {
            Transform towards = part.Towards != HumanBodyBones.LastBone ? animator.GetBoneTransform(part.Towards) : null;

            // Local-space vector to the next bone, which is where this bone's length and direction come from.
            Vector3 span = towards != null
                ? bone.InverseTransformPoint(towards.position)
                : Vector3.up * (0.12f / Mathf.Max(0.01f, bone.lossyScale.y));

            float length = span.magnitude;

            switch (part.Shape)
            {
                case Shape.Sphere:
                {
                    SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = length * 0.9f;
                    sphere.center = span * 0.5f;
                    return sphere;
                }

                case Shape.Box:
                {
                    BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
                    int axis = DominantAxis(span);
                    Vector3 size = Vector3.one * (length * 0.9f);
                    size[axis] = length;
                    box.size = size;
                    box.center = span * 0.5f;
                    return box;
                }

                default:
                {
                    CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                    capsule.direction = DominantAxis(span);
                    capsule.height = length;
                    capsule.radius = length * 0.2f;
                    capsule.center = span * 0.5f;
                    return capsule;
                }
            }
        }

        private static void AddJoint(GameObject bone, Rigidbody parent, Part part)
        {
            CharacterJoint joint = bone.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
            joint.enablePreprocessing = false;

            joint.lowTwistLimit = new SoftJointLimit { limit = part.LowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = part.HighTwist };
            joint.swing1Limit = new SoftJointLimit { limit = part.Swing };
            joint.swing2Limit = new SoftJointLimit { limit = part.Swing };
        }

        private static int DominantAxis(Vector3 vector)
        {
            var magnitude = new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            return magnitude.x >= magnitude.y && magnitude.x >= magnitude.z ? 0 : magnitude.y >= magnitude.z ? 1 : 2;
        }

        /// <summary>One physical bone: what it is, what it hangs from, and how far it may bend.</summary>
        private readonly struct Part
        {
            public Part(HumanBodyBones bone, HumanBodyBones towards, HumanBodyBones parent, Shape shape,
                float massFraction, float lowTwist, float highTwist, float swing)
            {
                Bone = bone;
                Towards = towards;
                Parent = parent;
                Shape = shape;
                MassFraction = massFraction;
                LowTwist = lowTwist;
                HighTwist = highTwist;
                Swing = swing;
            }

            public HumanBodyBones Bone { get; }

            public HumanBodyBones Towards { get; }

            public HumanBodyBones Parent { get; }

            public Shape Shape { get; }

            public float MassFraction { get; }

            public float LowTwist { get; }

            public float HighTwist { get; }

            public float Swing { get; }
        }
    }
}
