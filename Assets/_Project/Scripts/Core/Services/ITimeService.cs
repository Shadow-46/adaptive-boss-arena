namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// The single owner of game time, including hit-stop, slow-motion and pause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No other type in the project may write <c>Time.timeScale</c>. Hit-stop, the perfect-dodge
    /// slow-motion reward, and the pause menu all want to control time, and letting them each set
    /// the global scale directly produces the classic bug where unpausing restores the wrong speed
    /// because a hit-stop was in flight. Funnelling every request through one service makes those
    /// effects compose instead of collide.
    /// </para>
    /// <para>
    /// Consumers pick a clock deliberately: combat logic ticks on <see cref="DeltaTime"/> so it
    /// freezes during hit-stop, while UI, camera shake and the pause menu tick on
    /// <see cref="UnscaledDeltaTime"/> so they keep animating.
    /// </para>
    /// </remarks>
    public interface ITimeService
    {
        /// <summary>Frame delta after hit-stop, slow-motion and pause are applied.</summary>
        float DeltaTime { get; }

        /// <summary>Frame delta unaffected by any time manipulation.</summary>
        float UnscaledDeltaTime { get; }

        /// <summary>Physics step after time manipulation is applied.</summary>
        float FixedDeltaTime { get; }

        /// <summary>
        /// Seconds of scaled time elapsed since the fight began.
        /// </summary>
        /// <remarks>
        /// This is the clock every player observation is stamped with, so perception latency is
        /// measured in the same time frame that combat happens in. A hit-stop therefore pauses the
        /// boss's perception too, rather than silently handing it fresher data.
        /// </remarks>
        float CombatTime { get; }

        /// <summary>Current multiplier applied to real time.</summary>
        float TimeScale { get; }

        /// <summary>True while the game is paused.</summary>
        bool IsPaused { get; }

        /// <summary>
        /// Freezes time briefly to sell the weight of an impact.
        /// </summary>
        /// <remarks>
        /// Requests do not queue. A longer request supersedes a shorter one already in flight, so a
        /// heavy hit landing during a light hit's freeze extends it rather than being swallowed.
        /// </remarks>
        /// <param name="seconds">Real-time duration of the freeze.</param>
        void RequestHitStop(float seconds);

        /// <summary>Slows time for a fixed duration, used to reward a perfect dodge.</summary>
        /// <param name="scale">Multiplier to apply, where one is normal speed.</param>
        /// <param name="durationSeconds">Real-time duration of the effect.</param>
        void RequestSlowMotion(float scale, float durationSeconds);

        /// <summary>Cancels any hit-stop or slow-motion currently in effect.</summary>
        void ClearTimeEffects();

        /// <summary>Pauses or resumes the game.</summary>
        /// <param name="paused">True to pause.</param>
        void SetPaused(bool paused);

        /// <summary>Restarts the combat clock, for example when a fight is retried.</summary>
        void ResetCombatClock();
    }
}
