using System.Linq;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the fairness promises the fight makes: every blow can be seen coming, and every phase is long enough to
    /// be fought rather than skipped.
    /// </summary>
    /// <remarks>
    /// A boss that learns is only fair if what it throws can be answered by a person. The learning makes the
    /// boss reach for its answers more often, so an answer that could not be reacted to would be cheating by
    /// another name.
    /// </remarks>
    [TestFixture]
    public sealed class BossFairnessTests
    {
        private const string AttackFolder = "Assets/_Project/ScriptableObjects/Attacks";
        private const string BossConfigPath = "Assets/_Project/ScriptableObjects/Config/DefaultBossConfig.asset";

        /// <summary>
        /// The shortest wind-up a blow without a ground warning may have.
        /// </summary>
        /// <remarks>
        /// Around a quarter-second is how long a person takes to see a movement and react to it. With the
        /// boss's own swing read from its body, three-tenths leaves a sliver of margin for the roll itself.
        /// </remarks>
        private const float ReactionFloorSeconds = 0.3f;

        /// <summary>The thinnest share of the boss's health a phase may span.</summary>
        private const float ThinnestPhase = 0.15f;

        [Test]
        public void EveryBossBlowCanBeSeenComing()
        {
            var unfair = AssetDatabase.FindAssets("t:AttackDefinition", new[] { AttackFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(attack => attack != null && attack.name.StartsWith("Boss"))
                .Where(attack => !attack.ShowTelegraph && attack.StartupSeconds < ReactionFloorSeconds)
                .Select(attack => $"{attack.name} ({attack.StartupSeconds:0.00} s)")
                .ToList();

            Assert.IsEmpty(unfair, "Boss blows faster than a person can react, with no warning on the ground.");
        }

        [Test]
        public void EveryPhaseIsLongEnoughToFight()
        {
            var config = AssetDatabase.LoadAssetAtPath<BossConfig>(BossConfigPath);
            Assume.That(config, Is.Not.Null, "Run setup to generate the boss config.");

            for (int i = 0; i < config.Phases.Length; i++)
            {
                float start = config.Phases[i].HealthThreshold;
                float end = i + 1 < config.Phases.Length ? config.Phases[i + 1].HealthThreshold : 0f;

                Assert.GreaterOrEqual(start - end, ThinnestPhase,
                    $"Phase '{config.Phases[i].Name}' spans {start - end:P0} of the health: over before it is fought.");
            }
        }
    }
}
