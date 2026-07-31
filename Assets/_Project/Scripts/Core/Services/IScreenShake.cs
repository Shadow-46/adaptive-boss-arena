namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Accepts requests to shake the camera.
    /// </summary>
    /// <remarks>
    /// Abstracted so combat code can ask for impact feedback without holding a camera reference, and
    /// so a player who has turned shake down in the settings, or off entirely for accessibility, is
    /// accommodated in one place rather than at every call site.
    /// </remarks>
    public interface IScreenShake
    {
        /// <summary>
        /// Adds trauma, which decays on its own.
        /// </summary>
        /// <remarks>
        /// Trauma accumulates rather than restarting, so several impacts landing together produce one
        /// larger shake instead of a series of small ones fighting each other.
        /// </remarks>
        /// <param name="amount">Trauma to add, roughly 0.1 for a light hit and 0.5 for a heavy one.</param>
        void AddTrauma(float amount);

        /// <summary>
        /// Kicks the camera briefly toward the action, a directional dolly rather than a shake.
        /// </summary>
        /// <remarks>
        /// Where trauma jitters the frame, a punch lunges the whole view in and springs back. Reserved
        /// for the heaviest beats — a landed heavy or a clean deflect — where the extra emphasis reads
        /// as impact rather than noise. Scaled by the same accessibility intensity as shake.
        /// </remarks>
        /// <param name="amount">Punch strength, on the same rough scale as trauma.</param>
        void Punch(float amount);

        /// <summary>
        /// Kicks the camera's field of view briefly, then springs it back.
        /// </summary>
        /// <remarks>
        /// A positive value widens the view for a burst of speed — a dash — while a negative one
        /// narrows it for a moment of focus, such as the slow-motion after a perfect dodge. It rides
        /// the same accessibility intensity as shake, so turning camera motion down calms it too.
        /// </remarks>
        /// <param name="degrees">Signed field-of-view change; positive widens, negative narrows.</param>
        void PunchFov(float degrees);

        /// <summary>
        /// Scales all shake and punch, for the accessibility setting.
        /// </summary>
        /// <remarks>
        /// Zero disables camera motion entirely, which some players need rather than merely prefer.
        /// Kept on the interface so the settings menu can reach it through the service registry
        /// without a reference to the concrete camera.
        /// </remarks>
        /// <param name="intensity">Scale from zero, meaning off, to one.</param>
        void SetUserIntensity(float intensity);

        /// <summary>Stops any shake immediately.</summary>
        void ClearTrauma();
    }
}
