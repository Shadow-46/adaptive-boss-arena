using System;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using UnityEngine;

namespace AdaptiveBossArena.UI
{
    /// <summary>Whether the player has been shown how to fight. Kept apart from settings and records.</summary>
    [Serializable]
    public sealed class OnboardingData
    {
        /// <summary>Schema version this record was written with.</summary>
        public int SchemaVersion = 1;

        /// <summary>Whether the How to Fight panel has been opened once already.</summary>
        public bool SeenHowToFight;
    }

    /// <summary>
    /// The title screen's guide to the fight: the controls, and the handful of mechanics the fight is built on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The deflect, the parry, the posture break and Wrath were all invisible until a player stumbled on them,
    /// and the fight's whole premise, that Igris learns what you repeat, was told in one subtitle line. A boss
    /// fight that punishes without explaining reads as unfair, so the panel opens by itself on the very first
    /// launch and stays one click away after that.
    /// </para>
    /// <para>
    /// Its own save key rather than a field on the settings, because the settings panel writes the whole
    /// settings record whenever it changes, and a second writer of the same file would race it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HowToFightPanel : MonoBehaviour
    {
        /// <summary>The save key the onboarding record lives under.</summary>
        public const string SaveKey = "onboarding";

        [SerializeField]
        [Tooltip("The full-screen panel shown and hidden.")]
        private GameObject _panel;

        private ISaveService _saveService;

        /// <summary>The guide's text: controls, then the mechanics, then the premise.</summary>
        public static string Guide =>
            "<b>CONTROLS</b>\n" +
            "Move  WASD      Look  Mouse      Lock on  Tab / Middle Mouse\n" +
            "Light  Left Mouse      Heavy  Shift + Left Mouse      Special  E\n" +
            "Guard  Right Mouse      Parry  Q      Roll  Space      Flask  R      Swap weapon  V\n\n" +
            "<b>HOW TO SURVIVE</b>\n" +
            "<b>Deflect</b>   tap Guard just as a blow lands. It costs him posture, not you.\n" +
            "<b>Parry</b>   Q in time turns a blow aside and staggers him. Some blows cannot be parried: roll those.\n" +
            "<b>Perfect roll</b>   roll through a blow at the last moment to open a counter.\n" +
            "<b>Posture</b>   fill his posture bar and he breaks. Strike the broken guard for a riposte.\n" +
            "<b>Wrath</b>   with a full focus meter, hold Guard and press E: eight seconds of harder, faster swings.\n" +
            "<b>Air</b>   Heavy out of a roll launches you; Light and Heavy aloft cut and plunge.\n\n" +
            "<b>HE LEARNS</b>\n" +
            $"{BossIdentity.Name} watches what you repeat and answers it, and tells you when he does. " +
            "Change your habit and the answer fades. Lose, and he remembers you on the next attempt.";

        private void Start()
        {
            _saveService = ServiceRegistry.Current != null &&
                           ServiceRegistry.Current.TryGet(out ISaveService service)
                ? service
                : null;

            OnboardingData data = Load();

            if (!data.SeenHowToFight)
            {
                Open();
            }
        }

        /// <summary>Shows the guide, above everything else on the screen, and remembers it was seen.</summary>
        public void Open()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            _saveService?.Save(SaveKey, new OnboardingData { SeenHowToFight = true });
        }

        /// <summary>Hides the guide.</summary>
        public void Close()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private OnboardingData Load()
        {
            if (_saveService != null && _saveService.TryLoad(SaveKey, out OnboardingData loaded) && loaded != null)
            {
                return loaded;
            }

            return new OnboardingData();
        }

        /// <summary>Assigns the panel. Used by the scene generator.</summary>
        /// <param name="panel">The full-screen panel.</param>
        public void Bind(GameObject panel) => _panel = panel;
    }
}
