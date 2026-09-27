namespace AdaptiveBossArena.Core.Services
{
    /// <summary>The player-facing graphics quality, the same three steps on every platform.</summary>
    /// <remarks>
    /// What each step means differs by platform - the browser's High is below the Windows build's - but the player
    /// only ever chooses among Low, Medium and High on the machine in front of them.
    /// </remarks>
    public enum GraphicsQuality
    {
        /// <summary>For integrated graphics: fewer effects, cheaper shadows, the lightest post-processing.</summary>
        Low = 0,

        /// <summary>The full scene with soft shadows and the full grade.</summary>
        Medium = 1,

        /// <summary>Everything the platform's renderer offers.</summary>
        High = 2
    }
}
