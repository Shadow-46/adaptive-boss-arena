using System;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// The graphics quality in effect, and whether the player chose it or the game did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In Core, like <see cref="LookSettings"/>, because the settings menu writes it and systems in several
    /// assemblies obey it - the lighting, the effect pools - and none of them may depend on each other.
    /// </para>
    /// <para>
    /// A change is announced, because quality can change mid-fight: the settings menu applies a choice the moment
    /// it is made, and the browser steps itself up or down once it has measured what the machine can hold.
    /// </para>
    /// </remarks>
    public static class GraphicsTier
    {
        /// <summary>The quality in effect.</summary>
        public static GraphicsQuality Current { get; private set; } = GraphicsQuality.Medium;

        /// <summary>True until the player picks a quality; the game may adjust it itself only while this holds.</summary>
        public static bool IsAutomatic { get; private set; } = true;

        /// <summary>Raised whenever the quality in effect changes.</summary>
        public static event Action<GraphicsQuality> Changed;

        /// <summary>Sets the quality the game chose for itself. Ignored once the player has chosen.</summary>
        /// <param name="quality">The quality to use.</param>
        public static void SetAutomatic(GraphicsQuality quality)
        {
            if (IsAutomatic)
            {
                Apply(quality);
            }
        }

        /// <summary>Sets the quality the player chose, which the game never overrides.</summary>
        /// <param name="quality">The quality to use.</param>
        public static void SetChosen(GraphicsQuality quality)
        {
            IsAutomatic = false;
            Apply(quality);
        }

        private static void Apply(GraphicsQuality quality)
        {
            if (quality == Current)
            {
                return;
            }

            Current = quality;
            Changed?.Invoke(quality);
        }
    }
}
