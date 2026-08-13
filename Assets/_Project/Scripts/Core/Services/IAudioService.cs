using UnityEngine;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>Mixer buses that volume settings map onto.</summary>
    public enum AudioBus
    {
        /// <summary>Overall output level.</summary>
        Master = 0,

        /// <summary>Background score.</summary>
        Music = 1,

        /// <summary>Combat and world sound effects.</summary>
        Effects = 2,

        /// <summary>Menu and heads-up display sounds.</summary>
        Interface = 3
    }

    /// <summary>
    /// Central playback surface for all game audio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sounds are requested by identifier rather than by <c>AudioClip</c> reference, so gameplay code
    /// carries no dependency on audio assets. The implementation resolves the identifier through a
    /// lookup asset, which is also where a placeholder beep gets swapped for a real recording without
    /// touching a single gameplay script.
    /// </para>
    /// <para>
    /// Voice limiting lives behind this interface too. A boss combo landing four hits in eight frames
    /// must not layer four copies of the same impact sound, which is a common source of harshness in
    /// fast combat.
    /// </para>
    /// </remarks>
    public interface IAudioService
    {
        /// <summary>Plays a one-shot sound at a world position.</summary>
        /// <param name="cueId">Identifier of the cue to play.</param>
        /// <param name="worldPosition">Position to play it from.</param>
        void PlayCue(string cueId, Vector3 worldPosition);

        /// <summary>Plays a one-shot sound without spatialisation, for interface feedback.</summary>
        /// <param name="cueId">Identifier of the cue to play.</param>
        void PlayCue2D(string cueId);

        /// <summary>Crossfades to a music track.</summary>
        /// <param name="trackId">Identifier of the track, or null to fade to silence.</param>
        /// <param name="fadeSeconds">Crossfade duration.</param>
        void PlayMusic(string trackId, float fadeSeconds = 1f);

        /// <summary>
        /// Sets how intense the music is, layering higher parts in as the fight escalates.
        /// </summary>
        /// <remarks>
        /// The score is built from stacked loops rather than separate tracks so it can thicken without
        /// a jarring cut. Level zero is the bare bed; each step up fades another layer in. Driven off
        /// the boss's phase index, so the music rises exactly as the boss does.
        /// </remarks>
        /// <param name="level">Intensity level, clamped to the number of layers available.</param>
        void SetMusicIntensity(int level);

        /// <summary>
        /// Sets the level of a bus.
        /// </summary>
        /// <remarks>
        /// Applied linearly, which is not how loudness is perceived — halfway on the slider sounds
        /// louder than half. A perceptual curve would be the improvement, but it is a change that has
        /// to be made by ear rather than by reasoning, so the honest description is this one. This
        /// comment previously claimed a decibel conversion that the implementation never performed.
        /// </remarks>
        /// <param name="bus">Bus to adjust.</param>
        /// <param name="linearVolume">Volume from zero to one.</param>
        void SetBusVolume(AudioBus bus, float linearVolume);

        /// <summary>Gets the current level of a mixer bus.</summary>
        /// <param name="bus">Bus to query.</param>
        /// <returns>Volume from zero to one.</returns>
        float GetBusVolume(AudioBus bus);
    }
}
