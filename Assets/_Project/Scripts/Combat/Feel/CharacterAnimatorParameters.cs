namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// The state and parameter names shared by the generated Animator controller and the bridge.
    /// </summary>
    /// <remarks>
    /// One definition, because the controller is built by the editor and driven by the runtime, and a
    /// name spelled differently on either side fails silently: a crossfade to a state that does not exist
    /// logs nothing and leaves the character frozen in its last pose.
    /// </remarks>
    public static class CharacterAnimatorParameters
    {
        /// <summary>Planar speed from zero to one, driving the locomotion blend.</summary>
        public const string Speed = "Speed";

        /// <summary>Normalised clip time for the attack state playing, set from the attack's timeline.</summary>
        public const string AttackTime = "AttackTime";

        /// <summary>
        /// Normalised progress through rising from the floor, scrubbing the get-up clip.
        /// </summary>
        /// <remarks>
        /// Held at zero while lying down, which is the clip's first frame: the one clip serves as both the
        /// body on the floor and the rise from it, so the two can never disagree about the pose.
        /// </remarks>
        public const string ReactionTime = "ReactionTime";

        /// <summary>Idle, walk, jog and sprint, blended by speed.</summary>
        public const string LocomotionState = "Locomotion";

        /// <summary>The dodge roll.</summary>
        public const string RollState = "Roll";

        /// <summary>The raised guard.</summary>
        public const string GuardState = "Guard";

        /// <summary>A broken stance.</summary>
        public const string StaggerState = "Stagger";

        /// <summary>Thrown through the air.</summary>
        public const string AirborneState = "Airborne";

        /// <summary>On the floor, and rising from it.</summary>
        public const string KnockedDownState = "KnockedDown";

        /// <summary>Defeat.</summary>
        public const string DeathState = "Death";

        /// <summary>The attack state used for a light swing when no binding names one.</summary>
        public const string DefaultLightState = "Light1";

        /// <summary>The attack state used for a heavy swing when no binding names one.</summary>
        public const string DefaultHeavyState = "Heavy";

        /// <summary>The attack state used for a special when no binding names one.</summary>
        public const string DefaultSpecialState = "Special";
    }
}
