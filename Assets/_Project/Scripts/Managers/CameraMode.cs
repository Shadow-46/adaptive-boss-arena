namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// The vantage points the camera can adopt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Worth being honest about the trade-off, because it is not merely aesthetic. This encounter is
    /// designed around a top-down read: attack telegraphs are drawn flat on the ground, the boss's
    /// spacing behaviour assumes you can judge distance at a glance, and its dodge-prediction
    /// adaptation only feels fair when you could have seen the swing coming.
    /// </para>
    /// <para>
    /// Those assumptions weaken as the camera drops. Third person still reads well and is arguably
    /// more dramatic. First person breaks several of them outright — ground telegraphs fall largely
    /// outside your view and an attack from behind is unreadable — so it is included as an option
    /// to try rather than as a supported way to fight.
    /// </para>
    /// </remarks>
    public enum CameraMode
    {
        /// <summary>Angled overhead view. The mode the encounter is designed around.</summary>
        TopDown = 0,

        /// <summary>Over-the-shoulder view, closer and more dramatic.</summary>
        ThirdPerson = 1,

        /// <summary>Eye-level view from the character's head.</summary>
        FirstPerson = 2
    }
}
