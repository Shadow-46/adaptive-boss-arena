using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Core.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that an attack actually resolves against a real collider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The timeline's behaviour under a long frame was already covered by an edit-mode test, and it
    /// was correct: it announces the active window even when a single frame swallows the whole
    /// attack. What nothing covered was the executor throwing that guarantee away by reading the
    /// phase only after the stepping had finished — by which point the attack had passed into
    /// recovery and no hit was ever queried.
    /// </para>
    /// <para>
    /// A bug can sit exactly in the seam between two well-tested pieces, which is why this drives the
    /// real executor against a real collider rather than either half in isolation.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class AttackExecutorHitTests
    {
        /// <summary>Records what it was hit with, standing in for a combatant.</summary>
        private sealed class RecordingTarget : MonoBehaviour, IDamageable
        {
            public readonly List<DamageInfo> Hits = new List<DamageInfo>();

            public CombatantTeam Team => CombatantTeam.Boss;

            public bool IsAlive => true;

            public DamageResult TakeDamage(in DamageInfo damage)
            {
                Hits.Add(damage);
                return DamageResult.Applied(damage.Amount, false);
            }
        }

        /// <summary>A clock the test drives by hand, so frame length is an input.</summary>
        private sealed class ScriptedTime : ITimeService
        {
            public float DeltaTime { get; set; }

            public float UnscaledDeltaTime => DeltaTime;

            public float FixedDeltaTime => 0.02f;

            public float CombatTime { get; set; }

            public float TimeScale => 1f;

            public bool IsPaused => false;

            public void RequestHitStop(float seconds) { }

            public void RequestSlowMotion(float scale, float seconds) { }

            public void SetPaused(bool paused) { }

            public void ClearTimeEffects() { }

            public void ResetCombatClock() => CombatTime = 0f;

            public void Tick() { }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private AttackDefinition _attack;

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject spawned in _spawned)
            {
                Object.DestroyImmediate(spawned);
            }

            _spawned.Clear();

            if (_attack != null)
            {
                Object.DestroyImmediate(_attack);
                _attack = null;
            }
        }

        private GameObject Spawn(string name)
        {
            var created = new GameObject(name);
            _spawned.Add(created);

            return created;
        }

        /// <summary>Builds a wide, close-range arc so aiming is not what is under test.</summary>
        private AttackDefinition MakeAttack()
        {
            _attack = ScriptableObject.CreateInstance<AttackDefinition>();

            // Set by reflection rather than through SerializedObject: this assembly is built for
            // every platform, so it has no access to UnityEditor.
            Set("_startupSeconds", 0.09f);
            Set("_activeSeconds", 0.07f);
            Set("_recoverySeconds", 0.2f);
            Set("_range", 3f);
            Set("_arcDegrees", 160f);
            Set("_damage", 10f);
            Set("_showTelegraph", false);

            return _attack;
        }

        /// <summary>Writes one of the attack's serialized fields.</summary>
        private void Set(string fieldName, object value)
        {
            FieldInfo field = typeof(AttackDefinition).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"AttackDefinition has no field '{fieldName}'.");
            field.SetValue(_attack, value);
        }

        /// <summary>Places a target with a hurtbox the attacker's layer mask will find.</summary>
        private RecordingTarget SpawnTarget(Vector3 position)
        {
            GameObject target = Spawn("Target");
            target.transform.position = position;

            var owner = target.AddComponent<RecordingTarget>();

            GameObject hurtboxObject = Spawn("Hurtbox");
            hurtboxObject.transform.SetParent(target.transform, false);
            hurtboxObject.layer = Layers.BossHurtbox;

            SphereCollider collider = hurtboxObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.85f;

            hurtboxObject.AddComponent<Hurtbox>();

            return owner;
        }

        [UnityTest]
        public IEnumerator AnAttackLandsOnATargetInFront()
        {
            // The baseline. If this fails the harness is wrong, not the executor.
            var time = new ScriptedTime();
            var events = new CombatEventBus();

            GameObject attacker = Spawn("Attacker");
            RecordingTarget target = SpawnTarget(Vector3.forward * 1.5f);

            // Awake has to run before the hurtbox can resolve its owner.
            yield return null;

            var executor = new AttackExecutor(
                attacker.transform, CombatantTeam.Player, Layers.PlayerAttackMask, time, events);

            executor.Begin(MakeAttack(), 1.5f);

            for (int i = 0; i < 40; i++)
            {
                time.DeltaTime = 0.01f;
                time.CombatTime += 0.01f;
                executor.Tick(0.01f);
            }

            Assert.AreEqual(1, target.Hits.Count, "A swing at a target directly in front did not land.");
        }

        [UnityTest]
        public IEnumerator AnAttackStillLandsWhenOneFrameSwallowsTheWholeSwing()
        {
            // The regression. A light attack's active window is roughly four frames at sixty per
            // second, so a single hitch can cover startup, active and recovery together. Before the
            // fix the executor read the phase only after the timeline had finished stepping, found
            // recovery, and never queried at all -- the swing passed through the target and nothing
            // happened, which reads as the game ignoring the input.
            var time = new ScriptedTime();
            var events = new CombatEventBus();

            GameObject attacker = Spawn("Attacker");
            RecordingTarget target = SpawnTarget(Vector3.forward * 1.5f);

            yield return null;

            var executor = new AttackExecutor(
                attacker.transform, CombatantTeam.Player, Layers.PlayerAttackMask, time, events);

            executor.Begin(MakeAttack(), 1.5f);

            // One frame longer than the entire attack.
            time.DeltaTime = 1f;
            time.CombatTime += 1f;
            executor.Tick(1f);

            Assert.AreEqual(
                1, target.Hits.Count,
                "A frame that spanned the whole attack registered no hit at all.");
        }

        [UnityTest]
        public IEnumerator ATargetIsStruckOnlyOncePerSwing()
        {
            // The active window spans several frames, and every one of them queries. Without the
            // already-struck set a single swing would deal its damage once per frame.
            var time = new ScriptedTime();
            var events = new CombatEventBus();

            GameObject attacker = Spawn("Attacker");
            RecordingTarget target = SpawnTarget(Vector3.forward * 1.5f);

            yield return null;

            var executor = new AttackExecutor(
                attacker.transform, CombatantTeam.Player, Layers.PlayerAttackMask, time, events);

            executor.Begin(MakeAttack(), 1.5f);

            for (int i = 0; i < 40; i++)
            {
                time.DeltaTime = 0.01f;
                time.CombatTime += 0.01f;
                executor.Tick(0.01f);
            }

            Assert.AreEqual(1, target.Hits.Count, "One swing dealt damage more than once.");
        }
    }
}
