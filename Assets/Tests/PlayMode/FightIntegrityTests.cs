using System.Collections;
using System.Reflection;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Events;
using AdaptiveBossArena.Core.Services;
using AdaptiveBossArena.Player;
using AdaptiveBossArena.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Asserts that the assembled fight actually functions, in the real scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These exist because of a failure the entire edit-mode suite was blind to: a play-tester
    /// reported that no bar moved for a whole fight, nobody died and the outcome screen never
    /// appeared — with a clean console and 237 green tests. Every unit in isolation was correct.
    /// </para>
    /// <para>
    /// The gap is that edit-mode tests never load a scene, never instantiate the prefabs, never run
    /// physics and never touch the interface. Everything between "the pool arithmetic is right" and
    /// "the player can see their health drop" was untested. That is precisely where the fault lives,
    /// so this walks the whole path in the shipped scene.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class FightIntegrityTests
    {
        /// <summary>Frames to let <c>Awake</c>/<c>Start</c> and the first physics step settle.</summary>
        private const int WarmUpFrames = 10;

        /// <summary>A hit big enough to be unmistakable, but not lethal.</summary>
        private const float TestDamage = 25f;

        private PlayerController _player;
        private BossController _boss;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            _player = Object.FindAnyObjectByType<PlayerController>();
            _boss = Object.FindAnyObjectByType<BossController>();
        }

        [UnityTest]
        public IEnumerator TheArenaBringsUpBothCombatants()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            // A controller that bailed during setup still answers TakeDamage — it just ignores every
            // hit — so an uninitialised combatant is indistinguishable from an untouchable one.
            Assert.IsTrue(_player.IsInitialised, "The player failed to initialise, silently.");
            Assert.IsTrue(_boss.IsInitialised, "The boss failed to initialise, silently.");

            Assert.Greater(_player.Health.Maximum, 0f);
            Assert.Greater(_boss.Health.Maximum, 0f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDrawnWeaponDecidesWhatADeflectIsWorth()
        {
            Assert.IsNotNull(_player, "No player in the arena scene.");

            // Found rather than injected: they are loaded precisely because the player config
            // references them, so this also fails if the weapon list itself came up empty.
            WeaponDefinition[] weapons = Resources.FindObjectsOfTypeAll<WeaponDefinition>();
            Assert.AreEqual(3, weapons.Length, "The three generated weapons are not all loaded.");

            // Equipping is private, and deliberately so - nothing outside the controller should be
            // choosing the player's weapon. Reflection is the honest way to drive it from a test
            // rather than widening the API for one assertion.
            MethodInfo equip = typeof(PlayerController).GetMethod(
                "EquipWeapon", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(equip, "PlayerController has no EquipWeapon method.");

            foreach (WeaponDefinition weapon in weapons)
            {
                equip.Invoke(_player, new object[] { weapon });

                // The regression: this property used to read the character config, so all three
                // weapons deflected for the same posture and the authored values never applied.
                Assert.AreEqual(
                    weapon.DeflectPostureDamage, _player.DeflectPostureDamage, 0.001f,
                    $"With '{weapon.DisplayName}' drawn, a deflect pays out the wrong posture.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBossInTheSceneCarriesItsWeakPoint()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            Hurtbox[] hurtboxes = _boss.GetComponentsInChildren<Hurtbox>();

            // The weak point lives on the prefab, and the scene is built from the prefab. Every part
            // of that chain has broken silently at least once in this project, and a weak point that
            // does not exist looks exactly like one the player keeps failing to hit.
            Assert.AreEqual(
                1,
                System.Array.FindAll(hurtboxes, h => h.DamageMultiplier > 1f).Length,
                "The boss in the arena has no weak point, or more than one.");

            Assert.AreEqual(
                1,
                System.Array.FindAll(hurtboxes, h => Mathf.Approximately(h.DamageMultiplier, 1f)).Length,
                "The boss lost the ordinary body hurtbox its weak point is meant to sit inside.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AHitOnThePlayerReducesTheirHealth()
        {
            Assert.IsNotNull(_player);

            Hurtbox hurtbox = _player.GetComponentInChildren<Hurtbox>();
            Assert.IsNotNull(hurtbox, "The player has no hurtbox, so nothing can ever hit them.");
            Assert.IsTrue(hurtbox.IsValid, "The player's hurtbox found no owner.");

            float before = _player.Health.Current;
            hurtbox.Receive(DamageFrom(CombatantTeam.Boss));

            yield return null;

            Assert.Less(
                _player.Health.Current, before,
                "A clean hit on the player's hurtbox did not reduce their health.");
        }

        [UnityTest]
        public IEnumerator AHitOnTheBossReducesItsHealth()
        {
            Assert.IsNotNull(_boss);

            Hurtbox hurtbox = _boss.GetComponentInChildren<Hurtbox>();
            Assert.IsNotNull(hurtbox, "The boss has no hurtbox, so nothing can ever hit it.");
            Assert.IsTrue(hurtbox.IsValid, "The boss's hurtbox found no owner.");

            float before = _boss.Health.Current;
            hurtbox.Receive(DamageFrom(CombatantTeam.Player));

            yield return null;

            Assert.Less(
                _boss.Health.Current, before,
                "A clean hit on the boss's hurtbox did not reduce its health.");
        }

        [UnityTest]
        public IEnumerator ThePlayerHealthBarFollowsTheirHealth()
        {
            // The reported symptom, end to end: pool → event channel → bar. This is the assertion the
            // edit-mode suite could never make, because it never builds the interface.
            ResourceBar bar = FindBar("PlayerVitals", "Health");
            Assert.IsNotNull(bar, "No player health bar in the heads-up display.");

            float shownBefore = bar.DisplayedFraction;

            _player.GetComponentInChildren<Hurtbox>().Receive(DamageFrom(CombatantTeam.Boss));

            yield return null;

            Assert.Less(
                bar.DisplayedFraction, shownBefore,
                "The player's health fell but the bar did not follow it.");
        }

        [UnityTest]
        public IEnumerator TheBossHealthBarFollowsItsHealth()
        {
            ResourceBar bar = FindBar("BossVitals", "Health");
            Assert.IsNotNull(bar, "No boss health bar in the heads-up display.");

            float shownBefore = bar.DisplayedFraction;

            _boss.GetComponentInChildren<Hurtbox>().Receive(DamageFrom(CombatantTeam.Player));

            yield return null;

            Assert.Less(
                bar.DisplayedFraction, shownBefore,
                "The boss's health fell but the bar did not follow it.");
        }

        [UnityTest]
        public IEnumerator TheBossAttacksAPlayerWhoJustStandsThere()
        {
            // No input at all. If the boss cannot produce a single attack against a stationary target
            // in this long, the fight is not a fight — and the tester's static bars are explained
            // without any damage bug existing at all.
            Assert.IsTrue(
                ServiceRegistry.Current.TryGet(out ICombatEventBus events),
                "No combat event bus, so nothing can be learned from or reacted to.");

            int attacksStarted = 0;
            void Count(CombatEvent recorded)
            {
                if (recorded.Kind == CombatEventKind.AttackStarted &&
                    recorded.Actor == CombatantTeam.Boss)
                {
                    attacksStarted++;
                }
            }

            events.EventRecorded += Count;

            try
            {
                yield return new WaitForSeconds(25f);
            }
            finally
            {
                events.EventRecorded -= Count;
            }

            Assert.Greater(
                attacksStarted, 0,
                "The boss threw no attacks in 25 seconds against a stationary player.");
        }

        [UnityTest]
        public IEnumerator TheHealthChannelStillHasItsListener()
        {
            // Narrows the previous two failures. The bar applies a raise immediately, so a bar that
            // never moved either has no listener attached or is never raised at. This tells the two
            // apart, and the channel type keeps a listener count for exactly this purpose.
            FloatEventChannel[] loaded = Resources.FindObjectsOfTypeAll<FloatEventChannel>();
            var names = new System.Text.StringBuilder();

            foreach (FloatEventChannel c in loaded)
            {
                names.Append(c.name).Append('(').Append(c.ListenerCount).Append(") ");
            }

            Debug.Log($"[DIAG] float channels loaded={loaded.Length}: {names}");

            ResourceBar bar = FindBar("PlayerVitals", "Health");
            Assert.IsNotNull(bar);
            Debug.Log($"[DIAG] player health bar hasChannel={bar.HasChannel} " +
                      $"shows={bar.DisplayedFraction:0.000}");

            FloatEventChannel channel = FindChannel("OnPlayerHealthChanged");
            Assert.IsNotNull(channel, "The player health channel asset is not loaded.");

            Debug.Log($"[DIAG] {channel.name} listeners={channel.ListenerCount} " +
                      $"barShows={bar.DisplayedFraction:0.000}");

            Assert.Greater(
                channel.ListenerCount, 0,
                "Nothing is listening to the player health channel, so no bar can ever move.");

            // And prove the listener is the bar: raise the channel by hand.
            channel.Raise(0.5f);
            yield return null;

            Assert.AreEqual(
                0.5f, bar.DisplayedFraction, 0.001f,
                "The channel has a listener, but raising it did not move the bar.");
        }

        [UnityTest]
        public IEnumerator TheEndScreenTextDoesNotOverlapItself()
        {
            // The play-tester's screenshot showed the summary drawn straight through the headline.
            // Every UiBuilder text has a centre pivot, so a rect at y spans y +/- height/2 and a
            // top-anchored block starts at the rect's top edge — easy to get wrong by eye, and
            // invisible to every other test.
            RectTransform headline = FindText("Headline");
            RectTransform summary = FindText("Summary");
            RectTransform dossier = FindText("Dossier");

            Assert.IsNotNull(headline, "No end-screen headline.");
            Assert.IsNotNull(summary, "No end-screen summary.");
            Assert.IsNotNull(dossier, "No end-screen dossier.");

            Assert.GreaterOrEqual(
                BottomOf(headline), TopOf(summary),
                "The end-screen summary overlaps the headline.");

            Assert.GreaterOrEqual(
                BottomOf(summary), TopOf(dossier),
                "The end-screen dossier overlaps the summary.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayerHoldsAVisibleWeapon()
        {
            // The socket, the equip call and the model field all existed from the start with nothing
            // to put in them, so a weapon swap changed a label and a swing colour and nothing the
            // player could see. This asserts the hand is no longer empty.
            var socket = Object.FindAnyObjectByType<Combat.Feel.WeaponSocket>();
            Assert.IsNotNull(socket, "The player has no weapon socket.");

            MeshRenderer[] held = socket.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

            Assert.Greater(held.Length, 0, "The weapon socket is empty; no model was equipped.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator NothingDecorativeCanBeCollidedWith()
        {
            // The bodies and the arena dressing are made of primitives, and a Unity primitive ships
            // with a collider. Any that survived would sit inside the character controller or out on
            // the floor changing the fight, so every one of them has to have been stripped.
            foreach (string rootName in new[] { "Dressing" })
            {
                GameObject root = GameObject.Find(rootName);

                if (root == null)
                {
                    continue;
                }

                Assert.IsEmpty(
                    root.GetComponentsInChildren<Collider>(true),
                    $"{rootName} carries colliders and can interfere with the fight.");
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            Transform visual = player.transform.Find("Visual");

            Assert.IsNotNull(visual, "The player has no visual root.");
            Assert.IsEmpty(
                visual.GetComponentsInChildren<Collider>(true),
                "The player's body carries colliders, which would fight the character controller.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryVisibleSurfaceHasAMaterial()
        {
            // Deleting the generated materials to re-tune them left the weapon models pointing at
            // assets that no longer existed, and the whole suite stayed green because nothing
            // asserted that a renderer's material actually resolves. A missing material renders as
            // Unity's magenta error shader, which is unmissable when playing and invisible to tests.
            foreach (Renderer renderer in
                     Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                // Trails and particles bring their own materials and are allowed to differ.
                if (renderer is TrailRenderer || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Assert.IsNotNull(
                    renderer.sharedMaterial,
                    $"'{renderer.name}' has no material and will render as the magenta error shader.");

                Assert.IsNotNull(
                    renderer.sharedMaterial.shader,
                    $"'{renderer.name}' has a material with no shader.");
            }

            yield return null;
        }

        private static float TopOf(RectTransform rect) =>
            rect.anchoredPosition.y + (rect.sizeDelta.y * 0.5f);

        private static float BottomOf(RectTransform rect) =>
            rect.anchoredPosition.y - (rect.sizeDelta.y * 0.5f);

        /// <summary>Finds a named text element anywhere in the interface, including inactive panels.</summary>
        private static RectTransform FindText(string objectName)
        {
            foreach (UnityEngine.UI.Text text in
                     Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
            {
                // Inactive because the end panel is hidden until the fight resolves; scene-only
                // because FindObjectsOfTypeAll also returns the prefab assets behind them.
                if (text.name == objectName && text.gameObject.scene.IsValid())
                {
                    return text.rectTransform;
                }
            }

            return null;
        }

        /// <summary>Finds a loaded channel asset by name.</summary>
        private static FloatEventChannel FindChannel(string assetName)
        {
            foreach (FloatEventChannel candidate in
                     Resources.FindObjectsOfTypeAll<FloatEventChannel>())
            {
                if (candidate.name == assetName)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Builds a plain, unavoidable hit from one side.</summary>
        /// <remarks>
        /// Ignores invulnerability deliberately: this is testing whether the damage pipeline works at
        /// all, not whether the defensive guards work, and a dash i-frame that happened to be active
        /// would otherwise make the result depend on timing.
        /// </remarks>
        private static DamageInfo DamageFrom(CombatantTeam source) => new DamageInfo
        {
            Amount = TestDamage,
            Type = DamageType.Light,
            SourceTeam = source,
            SourceInstanceId = -1,
            HitPoint = Vector3.zero,
            HitDirection = Vector3.forward,
            KnockbackSpeed = 0f,
            PoiseDamage = 0f,
            Stagger = StaggerStrength.None,
            HitStopSeconds = 0f,
            IgnoresInvulnerability = true
        };

        /// <summary>Finds a named bar inside a named heads-up display group.</summary>
        private static ResourceBar FindBar(string groupName, string barName)
        {
            foreach (ResourceBar bar in Object.FindObjectsByType<ResourceBar>(FindObjectsSortMode.None))
            {
                if (bar.name == barName && bar.transform.parent != null &&
                    bar.transform.parent.name == groupName)
                {
                    return bar;
                }
            }

            return null;
        }
    }
}
