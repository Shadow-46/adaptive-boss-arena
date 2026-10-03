using AdaptiveBossArena.Core.Constants;
using UnityEngine;

namespace AdaptiveBossArena.Combat
{
    /// <summary>
    /// Whether standing stone shelters a target from a blast thrown out across the floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shockwaves are rings driven out from the brute's body along the ground. A pillar standing between it and
    /// the knight takes the wave, which gives the arena's stone a use beyond the one it already had - baiting a
    /// charge into it - and gives the player an answer to a wave other than the roll.
    /// </para>
    /// <para>
    /// Tested as a level line at a fighter's chest height rather than from the brute's origin to the target's:
    /// the wave travels along the floor, so it is the stone at that height that matters, and a level line never
    /// strikes the floor itself. The wall ring is never between two fighters inside it, so in practice only the
    /// pillars on the floor can shelter anyone.
    /// </para>
    /// <para>
    /// It favours the player, so it is no help to the boss and no breach of the firewall: the boss never asks it
    /// anything, and a broken pillar shelters no one.
    /// </para>
    /// </remarks>
    public static class CoverRules
    {
        /// <summary>Height above the floor the wave is tested at: a fighter's chest.</summary>
        public const float WaveHeight = 1.1f;

        /// <summary>Whether solid stone lies between a blast's source and a target, across the floor.</summary>
        /// <param name="source">Where the blast is thrown from.</param>
        /// <param name="target">Who it is thrown at.</param>
        /// <param name="coverMask">The layers that count as stone.</param>
        /// <returns>True when the target is sheltered.</returns>
        public static bool IsShielded(Vector3 source, Vector3 target, int coverMask)
        {
            float height = GameplayConstants.GroundPlaneHeight + WaveHeight;
            var from = new Vector3(source.x, height, source.z);
            var to = new Vector3(target.x, height, target.z);

            return Physics.Linecast(from, to, coverMask, QueryTriggerInteraction.Ignore);
        }
    }
}
