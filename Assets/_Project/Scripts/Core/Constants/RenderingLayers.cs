namespace AdaptiveBossArena.Core.Constants
{
    /// <summary>
    /// The rendering layers lights are sorted by: which lights reach which renderers.
    /// </summary>
    /// <remarks>
    /// Separate from the physics layers in <see cref="LayerNames"/>: a rendering layer decides only what a light
    /// touches, never what collides. It exists so the fighters can carry a light of their own - a soft light from
    /// the camera's side that keeps whichever face of them the camera sees readable - without that light also
    /// washing the floor and walls the moonlit nave depends on being dark.
    /// </remarks>
    public static class RenderingLayers
    {
        /// <summary>The layer everything renders on by default, and the only one the arena's own lights use.</summary>
        public const uint DefaultMask = 1u << 0;

        /// <summary>Index of the fighters' own layer.</summary>
        public const int CharacterIndex = 1;

        /// <summary>The fighters and their weapons; the character light reaches only this.</summary>
        public const uint CharacterMask = 1u << CharacterIndex;

        /// <summary>The name shown for <see cref="CharacterIndex"/> in the editor.</summary>
        public const string CharacterName = "Characters";
    }
}
