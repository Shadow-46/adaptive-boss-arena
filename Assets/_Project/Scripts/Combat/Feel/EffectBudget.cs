using UnityEngine;

namespace AdaptiveBossArena.Combat.Feel
{
    /// <summary>
    /// How much the combat effects may spend, which is less in a browser.
    /// </summary>
    /// <remarks>
    /// The browser build is played on laptops with integrated graphics, where every live particle system and every
    /// per-pixel light is paid for on the same weak GPU that draws the fight. The pools read this once when they
    /// are built, so nothing checks the platform per hit.
    /// </remarks>
    public static class EffectBudget
    {
        /// <summary>Whether the game is running as the WebGL player.</summary>
        public static bool IsWebPlayer => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Impact bursts alive at once in a browser: enough for a combo and a clash, not a firework.</summary>
        public const int WebImpactBursts = 6;
    }
}
