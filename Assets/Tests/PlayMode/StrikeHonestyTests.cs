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
    /// Asserts a blow lands only where the striking part actually went.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player reported being hurt by swings that visibly passed beside them. Damage came from a wedge in
    /// front of the attacker - up to four metres and 120 degrees - whatever the body did inside it. These tests
    /// swing a blade by hand, exactly as a clip would carry it, against a real trigger collider through the real
    /// executor, so the seam between what is seen and what hurts is the thing under test.
    /// </para>
    /// <para>
    /// One case keeps the old wedge on purpose, to prove the scenario is the one reported: the same swing that
    /// now misses would have hurt.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class StrikeHonestyTests
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

        /// <summary>A clock the test drives by hand.</summary>
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

        private const float Step = 0.01f;

        /// <summary>Where the blade pivots: the attacker's shoulder.</summary>
        private static readonly Vector3 Shoulder = Vector3.up;

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

        [UnityTest]
        public IEnumerator ASwingThatPassesBesideTheTargetDealsNothing()
        {
            // 3.2 m away: well inside the old four-metre wedge, well beyond a two-metre blade.
            RecordingTarget target = SpawnTarget(new Vector3(0f, 1f, 3.2f));
            StrikeVolume blade = SpawnBlade();
            yield return null;

            Swing(StrikerParts.Weapon, blade, degreesPerTick: 10f);

            Assert.IsEmpty(target.Hits, "A blade that never reached the target still hurt it.");
        }

        [UnityTest]
        public IEnumerator TheOldWedgeWouldHaveHurtFromThere()
        {
            RecordingTarget target = SpawnTarget(new Vector3(0f, 1f, 3.2f));
            StrikeVolume blade = SpawnBlade();
            yield return null;

            Swing(StrikerParts.None, blade, degreesPerTick: 10f);

            Assert.AreEqual(1, target.Hits.Count,
                "The old wedge did not reach the target, so the scenario above proves nothing.");
        }

        [UnityTest]
        public IEnumerator ASwingThroughTheTargetLandsExactlyOnce()
        {
            RecordingTarget target = SpawnTarget(new Vector3(0f, 1f, 1.6f));
            StrikeVolume blade = SpawnBlade();
            yield return null;

            Swing(StrikerParts.Weapon, blade, degreesPerTick: 10f);

            Assert.AreEqual(1, target.Hits.Count, "A blade through the target did not land exactly once.");
        }

        [UnityTest]
        public IEnumerator AFastSwingCannotPassThroughUnseen()
        {
            // 60 degrees a frame: the blade is sampled only at -30 and +30, both clear of the target. Only the
            // sweep between the two frames finds it.
            RecordingTarget target = SpawnTarget(new Vector3(0f, 1f, 1.6f));
            StrikeVolume blade = SpawnBlade();
            yield return null;

            Swing(StrikerParts.Weapon, blade, degreesPerTick: 60f);

            Assert.AreEqual(1, target.Hits.Count, "A fast blade passed through the target between two frames.");
        }

        /// <summary>
        /// Runs one attack while carrying the blade from the left of the attacker to its right.
        /// </summary>
        private void Swing(StrikerParts strikers, StrikeVolume blade, float degreesPerTick)
        {
            var time = new ScriptedTime { DeltaTime = Step };
            GameObject attacker = Spawn("Attacker");

            var executor = new AttackExecutor(
                attacker.transform, CombatantTeam.Player, Layers.PlayerAttackMask, time, new CombatEventBus());
            executor.SetStrikers(new[] { blade });

            executor.Begin(MakeAttack(strikers));

            float yaw = -90f;

            for (int i = 0; i < 60 && executor.IsRunning; i++)
            {
                blade.transform.SetPositionAndRotation(Shoulder, Quaternion.Euler(0f, yaw, 0f));

                time.CombatTime += Step;
                executor.Tick(Step);

                yaw = Mathf.Min(90f, yaw + degreesPerTick);
            }
        }

        /// <summary>A blade from half a metre to two metres out from the shoulder.</summary>
        private StrikeVolume SpawnBlade()
        {
            GameObject bladeObject = Spawn("Blade");
            bladeObject.transform.SetPositionAndRotation(Shoulder, Quaternion.Euler(0f, -90f, 0f));

            var blade = bladeObject.AddComponent<StrikeVolume>();
            blade.Configure(StrikerParts.Weapon, 0.5f, 2f, 0.1f);

            return blade;
        }

        private RecordingTarget SpawnTarget(Vector3 position)
        {
            GameObject targetObject = Spawn("Target");
            targetObject.transform.position = position;
            var target = targetObject.AddComponent<RecordingTarget>();

            GameObject hurtboxObject = Spawn("Hurtbox");
            hurtboxObject.transform.SetParent(targetObject.transform, false);
            hurtboxObject.layer = Layers.BossHurtbox;

            SphereCollider collider = hurtboxObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.4f;
            hurtboxObject.AddComponent<Hurtbox>();

            return target;
        }

        /// <summary>A wide, long wedge, so the old volume would reach where the blade does not.</summary>
        private AttackDefinition MakeAttack(StrikerParts strikers)
        {
            _attack = ScriptableObject.CreateInstance<AttackDefinition>();

            // By reflection: this assembly is built for every platform, so it has no access to UnityEditor.
            Set("_startupSeconds", Step);
            Set("_activeSeconds", 0.3f);
            Set("_recoverySeconds", 0.05f);
            Set("_range", 4f);
            Set("_arcDegrees", 160f);
            Set("_damage", 10f);
            Set("_showTelegraph", false);
            Set("_strikers", strikers);

            return _attack;
        }

        private void Set(string field, object value) =>
            typeof(AttackDefinition)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_attack, value);

        private GameObject Spawn(string name)
        {
            var created = new GameObject(name);
            _spawned.Add(created);

            return created;
        }
    }
}
