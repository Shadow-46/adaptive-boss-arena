namespace AdaptiveBossArena.Core.Perception
{
    /// <summary>
    /// Implemented by the player to expose the only view of itself the boss may ever hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player assembly implements this; the AI and Learning assemblies consume it; the game's
    /// composition root wires the two together. Because neither AI nor Learning references the
    /// player assembly, this interface is not merely the preferred path to player state, it is the
    /// only reachable one.
    /// </para>
    /// <para>
    /// Nothing should ever be added here that a human opponent could not perceive by watching the
    /// screen. Every extension is a hole in the firewall.
    /// </para>
    /// </remarks>
    public interface IObservablePlayer
    {
        /// <summary>Captures what an onlooker could perceive about the player at this instant.</summary>
        /// <param name="timestamp">Combat-clock time to stamp the snapshot with.</param>
        /// <returns>A populated snapshot.</returns>
        PlayerObservation CaptureObservation(float timestamp);
    }

    /// <summary>
    /// Supplies the boss with player observations that are deliberately out of date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second layer of the firewall. Even restricted to visible information, a boss acting on
    /// the current frame's data would react with inhuman immediacy, which reads as cheating whether
    /// or not it technically is. Every observation handed to the AI is therefore held back by a
    /// perception latency, so the boss is always responding to what the player was doing a moment
    /// ago.
    /// </para>
    /// <para>
    /// A consequence worth keeping: the player can genuinely bait the boss, because a feint the boss
    /// commits to during the latency window cannot be taken back.
    /// </para>
    /// </remarks>
    public interface IPerceptionSource
    {
        /// <summary>How far behind the present the perceived data is, in seconds.</summary>
        float LatencySeconds { get; }

        /// <summary>Retrieves the freshest observation the boss is allowed to act on.</summary>
        /// <param name="observation">Receives the delayed snapshot.</param>
        /// <returns>False before enough history has accumulated to satisfy the latency.</returns>
        bool TryGetPerceived(out PlayerObservation observation);

        /// <summary>
        /// Retrieves an older observation, for estimating how the player's movement is changing.
        /// </summary>
        /// <param name="secondsFurtherBack">Additional time to look back beyond the standard latency.</param>
        /// <param name="observation">Receives the delayed snapshot.</param>
        /// <returns>False when history does not reach back that far.</returns>
        bool TryGetPerceivedAt(float secondsFurtherBack, out PlayerObservation observation);
    }
}
