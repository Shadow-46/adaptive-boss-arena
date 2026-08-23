using System.Collections;
using System.Reflection;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Core.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the boss's parry is a window with a punishable tail, not an invincible stance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule itself is covered in edit mode against <c>DefenceResolver</c>. What cannot be covered
    /// there is the wiring: that the boss hands the resolver its own stance timer and its own
    /// configured window, and therefore that the tail is genuinely punishable in the shipped scene.
    /// Before this, the stance refused every hit for half a second, so it could be waited out but
    /// never beaten — there was no moment at which committing to it had been the wrong call.
    /// </para>
    /// <para>
    /// The stance is driven by reflection rather than by waiting for the boss to choose one. Its
    /// entry is a seeded probability against a perceived heavy swing, so provoking it honestly would
    /// make the test a coin flip; and the distinction under test is a tenth of a second wide, which
    /// is not something to leave to frame timing.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class BossParryTests
    {
        private const int WarmUpFrames = 10;

        private BossController _boss;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            _boss = Object.FindAnyObjectByType<BossController>();
        }

        /// <summary>Reads a private field from an instance, failing the test if it is not there.</summary>
        private static object Field(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"{owner.GetType().Name} has no field '{fieldName}'.");

            return field.GetValue(owner);
        }

        /// <summary>The boss's tuning asset, which nothing needs to expose publicly.</summary>
        private BossConfig Config() => (BossConfig)Field(_boss, "_config");

        /// <summary>Puts the boss into its parry stance, a given distance into it.</summary>
        private void ForceStance(float secondsIntoStance)
        {
            var context = (BossContext)Field(_boss, "_context");
            context.IsParrying = true;

            object parryState = Field(_boss, "_parryState");

            // Auto-property with a private setter, so the compiler-generated backing field is the
            // only way in. Named rather than searched so a rename fails loudly here.
            FieldInfo timeInState = parryState.GetType().BaseType?.GetField(
                "<TimeInState>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(timeInState, "StateBase no longer stores TimeInState in a backing field.");
            timeInState.SetValue(parryState, secondsIntoStance);
        }

        /// <summary>An ordinary player swing, of the kind the stance exists to answer.</summary>
        private static DamageInfo Swing() =>
            DamageInfo.Create(30f, DamageType.Heavy, CombatantTeam.Player, 1);

        [UnityTest]
        public IEnumerator AHitInsideTheWindowIsRefused()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            BossConfig config = Config();
            Assert.IsNotNull(config, "The boss has no config.");

            ForceStance(config.ParryWindowSeconds * 0.5f);

            Assert.AreEqual(
                DamageOutcome.Deflected, _boss.TakeDamage(Swing()).Outcome,
                "A hit met inside the parry window was not refused.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AHitInTheTailLandsInFull()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            BossConfig config = Config();
            float healthBefore = _boss.Health.Current;

            // Past the window, still committed to the stance. This is the punish, and the entire
            // reason baiting the stance out is worth doing.
            ForceStance(config.ParryWindowSeconds + config.ParryTailSeconds * 0.5f);

            Assert.AreEqual(
                DamageOutcome.Applied, _boss.TakeDamage(Swing()).Outcome,
                "A hit landed in the parry's tail was still refused.");

            Assert.Less(_boss.Health.Current, healthBefore, "The punishing hit dealt no damage.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AnUnparryableExecutionIsNotRefusedEvenOnTheBeat()
        {
            Assert.IsNotNull(_boss, "No boss in the arena scene.");

            BossConfig config = Config();

            ForceStance(config.ParryWindowSeconds * 0.5f);

            DamageInfo execution = DamageInfo.Create(30f, DamageType.Heavy, CombatantTeam.Player, 1);
            execution = new DamageInfo
            {
                Amount = execution.Amount,
                Type = execution.Type,
                SourceTeam = execution.SourceTeam,
                SourceInstanceId = execution.SourceInstanceId,
                HitDirection = execution.HitDirection,
                Stagger = execution.Stagger,
                Unparryable = true
            };

            // The punish the player earned by breaking its guard. Refusing it with the same stance
            // that failed would take back something already paid for.
            Assert.AreEqual(
                DamageOutcome.Applied, _boss.TakeDamage(execution).Outcome,
                "The boss parried an attack marked unparryable.");

            yield return null;
        }
    }
}
