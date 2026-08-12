using System.Text;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Learning;
using AdaptiveBossArena.UI;
using UnityEngine;

namespace AdaptiveBossArena.Game
{
    /// <summary>
    /// Owns the lifecycle of a single attempt: start, outcome, records, retry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player and the boss each know how to reset themselves but neither decides when to. That
    /// decision belongs here, in the one place that can see both, which is also why restarting is a
    /// reset rather than a scene reload: reloading would discard the service registry and the
    /// random seed, and a seed that cannot survive a retry is useless for reproducing a fight.
    /// </para>
    /// <para>
    /// It also records the attempt. The statistic worth keeping is how many counter-strategies the
    /// boss managed to develop, since that measures how readable the player was rather than how
    /// fast they were.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EncounterDirector : MonoBehaviour
    {
        [Header("Combatants")]
        [SerializeField]
        [Tooltip("The player. Found automatically when left empty.")]
        private Player.PlayerController _player;

        [SerializeField]
        [Tooltip("The boss. Found automatically when left empty.")]
        private BossController _boss;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Arena configuration supplying the spawn positions.")]
        private ArenaConfig _arenaConfig;

        [Header("Interface")]
        [SerializeField]
        [Tooltip("Screen shown once the fight is decided.")]
        private EndScreen _endScreen;

        [SerializeField]
        [Tooltip("Pause menu, suppressed once the fight ends.")]
        private PauseMenu _pauseMenu;

        [SerializeField]
        [Tooltip("The ready–fight beat played before each attempt. Optional; without it the fight " +
                 "simply begins immediately.")]
        private RoundIntro _roundIntro;

        [Header("Event Channels")]
        [SerializeField]
        [Tooltip("Raised when the player is defeated.")]
        private VoidEventChannel _playerDiedChannel;

        [SerializeField]
        [Tooltip("Raised when the boss is defeated.")]
        private VoidEventChannel _bossDefeatedChannel;

        [SerializeField]
        [Tooltip("Raised on a clean deflect, relayed into posture damage on the boss.")]
        private VoidEventChannel _deflectChannel;

        [SerializeField]
        [Tooltip("Raised on a perfect dodge, counted for the post-fight report.")]
        private VoidEventChannel _perfectDodgeChannel;

        /// <summary>Speed the killing blow plays at, low enough to read as a beat rather than a stutter.</summary>
        private const float OutcomeSlowMotionScale = 0.35f;

        /// <summary>How long the outcome slow-motion holds, roughly matching the end screen's delay.</summary>
        private const float OutcomeSlowMotionSeconds = 1.1f;

        /// <summary>A deeper crawl for an execution — the boss dying with its guard broken.</summary>
        private const float ExecutionSlowMotionScale = 0.14f;

        /// <summary>A longer hold for the execution, so the finishing blow is savoured.</summary>
        private const float ExecutionSlowMotionSeconds = 1.8f;

        /// <summary>Camera trauma added on an execution, so the finisher lands with weight.</summary>
        private const float ExecutionTrauma = 0.8f;

        private ITimeService _time;
        private IScreenShake _screenShake;
        private ISaveService _saveService;
        private RecordsData _records;

        private float _attemptStartedAt;
        private bool _isDecided;

        // Tallied across the current attempt for the post-fight dossier, reset when a new one begins.
        private int _deflectsThisAttempt;
        private int _perfectDodgesThisAttempt;

        /// <summary>Seconds the current attempt has been running.</summary>
        public float AttemptDuration =>
            _time == null ? 0f : Mathf.Max(0f, _time.CombatTime - _attemptStartedAt);

        private void Awake()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<Player.PlayerController>();
            }

            if (_boss == null)
            {
                _boss = FindAnyObjectByType<BossController>();
            }
        }

        private void OnEnable()
        {
            if (_playerDiedChannel != null)
            {
                _playerDiedChannel.Raised += OnPlayerDied;
            }

            if (_bossDefeatedChannel != null)
            {
                _bossDefeatedChannel.Raised += OnBossDefeated;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised += OnDeflected;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised += OnPerfectDodge;
            }
        }

        private void OnDisable()
        {
            if (_playerDiedChannel != null)
            {
                _playerDiedChannel.Raised -= OnPlayerDied;
            }

            if (_bossDefeatedChannel != null)
            {
                _bossDefeatedChannel.Raised -= OnBossDefeated;
            }

            if (_deflectChannel != null)
            {
                _deflectChannel.Raised -= OnDeflected;
            }

            if (_perfectDodgeChannel != null)
            {
                _perfectDodgeChannel.Raised -= OnPerfectDodge;
            }
        }

        private void Start()
        {
            ServiceRegistry.Current.TryGet(out _time);
            ServiceRegistry.Current.TryGet(out _screenShake);

            _saveService = ResolveSaveService();
            _records = LoadRecords();

            if (_endScreen != null)
            {
                _endScreen.RestartRequested += RestartAttempt;
            }

            if (_pauseMenu != null)
            {
                _pauseMenu.RestartRequested += RestartAttempt;
            }

            ApplyRunModifiers();
            StartAttemptWithIntro();
        }

        /// <summary>
        /// Relays the minimum each combatant needs to know about the other.
        /// </summary>
        /// <remarks>
        /// The director is the only object that sees both, which is exactly why this bridging
        /// belongs here. The player is told where the boss stands and whether its guard is broken;
        /// the boss is told nothing at all, and continues to perceive the player solely through the
        /// delayed observation channel.
        /// </remarks>
        private void LateUpdate()
        {
            if (_player == null || _boss == null)
            {
                return;
            }

            _player.SetThreat(_boss.transform);
            _player.SetThreatPostureBroken(_boss.Poise != null && _boss.Poise.IsBroken);
        }

        /// <summary>
        /// Relays a clean deflect into posture damage on the boss.
        /// </summary>
        /// <remarks>
        /// Routed through the director for the same reason as the facing above: the player must not
        /// hold a reference to the boss. The player reports that it deflected; the director decides
        /// what that means for the other combatant.
        /// </remarks>
        private void OnDeflected()
        {
            _deflectsThisAttempt++;

            if (_player == null || _boss == null)
            {
                return;
            }

            _boss.ApplyDeflectPosture(_player.DeflectPostureDamage);
        }

        /// <summary>Counts a perfect dodge for the post-fight report.</summary>
        private void OnPerfectDodge() => _perfectDodgesThisAttempt++;

        /// <summary>
        /// Plays the ready–fight intro, then begins the attempt once it releases.
        /// </summary>
        /// <remarks>
        /// Time is frozen for the duration of the intro so the boss cannot open on a player who has
        /// not yet been given the cue, and the pause menu is suppressed so its input cannot fight the
        /// frozen clock. Without a configured intro the fight simply begins at once.
        /// </remarks>
        private void StartAttemptWithIntro()
        {
            _endScreen?.Hide();

            if (_roundIntro == null)
            {
                BeginAttempt();
                return;
            }

            _pauseMenu?.SetSuppressed(true);
            _time?.SetPaused(true);

            _roundIntro.Begin(OnIntroComplete);
        }

        /// <summary>Releases the fight once the intro has finished.</summary>
        private void OnIntroComplete()
        {
            _time?.SetPaused(false);
            BeginAttempt();
        }

        /// <summary>Starts a fresh attempt.</summary>
        public void BeginAttempt()
        {
            _isDecided = false;
            _attemptStartedAt = _time?.CombatTime ?? 0f;

            _deflectsThisAttempt = 0;
            _perfectDodgesThisAttempt = 0;

            _pauseMenu?.SetSuppressed(false);
            _endScreen?.Hide();
        }

        /// <summary>Resets both combatants and starts another attempt.</summary>
        public void RestartAttempt()
        {
            Vector3 playerSpawn = _arenaConfig != null ? _arenaConfig.PlayerSpawn : Vector3.zero;
            Vector3 bossSpawn = _arenaConfig != null
                ? _arenaConfig.BossSpawn
                : Vector3.forward * GameplayConstants.BossPhaseCount;

            _player?.ResetForNewAttempt(playerSpawn);
            _boss?.ResetForNewAttempt(bossSpawn);

            _time?.ClearTimeEffects();
            _time?.ResetCombatClock();

            StartAttemptWithIntro();
        }

        /// <summary>
        /// Applies the session's challenge modifiers to the combatants and the practice overlay.
        /// </summary>
        /// <remarks>
        /// The director is the composition root — the one place that sees both combatants — so it is
        /// where a modifier chosen on the title screen turns into numbers on the player and the boss.
        /// Nothing in <c>AI</c> or <c>Learning</c> ever learns these exist; each is a scale or a flag
        /// on a value those systems already respect.
        /// </remarks>
        private void ApplyRunModifiers()
        {
            RunModifiers modifiers = RunSettings.Current ?? new RunModifiers();

            _player?.ApplyRunModifiers(
                modifiers.HealingEnabled, modifiers.IncomingDamageMultiplier, modifiers.TrainingMode);
            _boss?.SetAdaptationRateScale(modifiers.AdaptationRateScale);

            if (modifiers.TrainingMode)
            {
                FindAnyObjectByType<AdaptationDebugOverlay>()?.SetForcedVisible(true);
            }
        }

        private void OnPlayerDied() => Decide(won: false);

        private void OnBossDefeated() => Decide(won: true);

        /// <summary>Ends the attempt, records it, and shows the outcome.</summary>
        private void Decide(bool won)
        {
            // Both combatants can die on the same frame, in a trade. Whichever event arrives first
            // decides the attempt; without this guard the second would overwrite the outcome and
            // record the attempt twice.
            if (_isDecided)
            {
                return;
            }

            _isDecided = true;
            _pauseMenu?.SetSuppressed(true);

            // The boss cannot see that the player has died — the firewall means it is never told
            // anything about them directly — so it has to be called off from here. Without this it
            // keeps swinging at a corpse behind the outcome screen.
            _boss?.SetFightConcluded(true);

            // An execution — the boss killed while its guard is broken — earns a distinct, deeper
            // cinematic beat than an ordinary kill: the whole point of the finisher is that it feels
            // earned, so the world crawls further and the camera lands the blow.
            bool executed = won && _boss?.Poise != null && _boss.Poise.IsBroken;

            if (executed)
            {
                _time?.RequestSlowMotion(ExecutionSlowMotionScale, ExecutionSlowMotionSeconds);
                _screenShake?.AddTrauma(ExecutionTrauma);
                _screenShake?.Punch(ExecutionTrauma);
            }
            else
            {
                // Drops the world into slow-motion on the decisive blow. The loser topples on unscaled
                // time regardless, so the beat reads as the world holding its breath while they fall.
                _time?.RequestSlowMotion(OutcomeSlowMotionScale, OutcomeSlowMotionSeconds);
            }

            float duration = AttemptDuration;
            AdaptationManager adaptation = _boss?.GetAdaptation();
            int adaptations = adaptation?.AdoptedStrategies.Count ?? 0;
            float remainingHealth = _player?.Health?.Normalized ?? 0f;

            // An unkillable Training player would otherwise set the fastest victory and the fewest
            // adaptations allowed permanently, with no way to undo it.
            bool countsTowardRecords = RunSettings.Current?.CountsTowardRecords ?? true;
            bool isNewRecord = won && countsTowardRecords && IsImprovement(duration, adaptations);

            if (countsTowardRecords)
            {
                _records.RecordAttempt(won, duration, remainingHealth, adaptations);
                _saveService?.Save(SaveKeys.Records, _records);
            }

            string dossier = BuildDossier(adaptation, remainingHealth);
            _endScreen?.Show(won, duration, adaptations, isNewRecord, dossier);
        }

        /// <summary>
        /// Composes the boss's post-fight dossier: what it read in the player, how it answered, and
        /// the tally that shows how the fight actually went.
        /// </summary>
        /// <remarks>
        /// The habit lines come from the same profile the boss reasoned from, so the report can never
        /// claim the boss knew something it did not act on. The adopted-strategy tells are repeated
        /// here so the passivity-style habits — the ones the habit lines omit because they read as an
        /// absence rather than a strength — are still accounted for.
        /// </remarks>
        private string BuildDossier(AdaptationManager adaptation, float remainingHealth)
        {
            var builder = new StringBuilder();

            if (adaptation != null)
            {
                System.Collections.Generic.IReadOnlyList<string> habits =
                    ProfileReport.HabitLines(adaptation.Profile);

                if (habits.Count > 0)
                {
                    builder.AppendLine("<b>It read you as:</b>");
                    foreach (string habit in habits)
                    {
                        builder.Append("• ").AppendLine(habit);
                    }

                    builder.AppendLine();
                }

                AppendAnswers(builder, adaptation);
            }

            int healthPercent = Mathf.RoundToInt(Mathf.Clamp01(remainingHealth) * 100f);
            builder.Append(
                $"Deflects landed {_deflectsThisAttempt}   ·   Perfect dodges {_perfectDodgesThisAttempt}" +
                $"   ·   Health left {healthPercent}%");

            RunModifiers modifiers = RunSettings.Current;
            if (modifiers != null && modifiers.AnyActive)
            {
                builder.AppendLine().Append("<b>Challenges:</b> ")
                    .Append(string.Join(", ", modifiers.ActiveLabels()));
            }

            return builder.ToString();
        }

        /// <summary>Appends the counters the boss developed, each with its player-facing tell.</summary>
        private static void AppendAnswers(StringBuilder builder, AdaptationManager adaptation)
        {
            if (adaptation.AdoptedStrategies.Count == 0)
            {
                return;
            }

            builder.AppendLine("<b>It answered with:</b>");

            foreach (CounterStrategy strategy in adaptation.AdoptedStrategies)
            {
                if (strategy != null && !string.IsNullOrWhiteSpace(strategy.TellMessage))
                {
                    builder.Append("• \"").Append(strategy.TellMessage).AppendLine("\"");
                }
            }

            builder.AppendLine();
        }

        /// <summary>True when this attempt beats a stored best.</summary>
        private bool IsImprovement(float duration, int adaptations) =>
            !_records.HasWon ||
            duration < _records.FastestVictorySeconds ||
            adaptations < _records.FewestAdaptationsAllowed;

        private ISaveService ResolveSaveService() =>
            ServiceRegistry.Current.TryGet(out ISaveService service) ? service : null;

        private RecordsData LoadRecords()
        {
            if (_saveService != null && _saveService.TryLoad(SaveKeys.Records, out RecordsData loaded))
            {
                return loaded;
            }

            return new RecordsData();
        }

        /// <summary>Assigns the references. Used by the scene generator.</summary>
        /// <param name="arenaConfig">Arena configuration supplying spawn positions.</param>
        /// <param name="endScreen">Outcome screen.</param>
        /// <param name="pauseMenu">Pause menu.</param>
        /// <param name="roundIntro">The ready–fight beat, or null to begin fights immediately.</param>
        /// <param name="playerDied">Player death channel.</param>
        /// <param name="bossDefeated">Boss defeat channel.</param>
        /// <param name="deflect">Clean deflect channel.</param>
        /// <param name="perfectDodge">Perfect dodge channel, counted for the post-fight report.</param>
        public void Bind(
            ArenaConfig arenaConfig,
            EndScreen endScreen,
            PauseMenu pauseMenu,
            RoundIntro roundIntro,
            VoidEventChannel playerDied,
            VoidEventChannel bossDefeated,
            VoidEventChannel deflect,
            VoidEventChannel perfectDodge)
        {
            _arenaConfig = arenaConfig;
            _endScreen = endScreen;
            _pauseMenu = pauseMenu;
            _roundIntro = roundIntro;
            _playerDiedChannel = playerDied;
            _bossDefeatedChannel = bossDefeated;
            _deflectChannel = deflect;
            _perfectDodgeChannel = perfectDodge;
        }
    }
}
