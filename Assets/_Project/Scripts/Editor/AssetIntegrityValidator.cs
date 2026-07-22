using System.Collections.Generic;
using AdaptiveBossArena.AI;
using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Player;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Checks that every configuration asset's references actually resolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exists because of a failure that cost a whole playtest. Regenerating the attack assets
    /// deleted their <c>.meta</c> files, so the new assets received new identifiers while both
    /// character configurations still pointed at the old ones. Every attack reference became null.
    /// </para>
    /// <para>
    /// Nothing complained. The project compiled, all tests passed, the scene built, and the game ran
    /// — but the boss had an empty attack catalogue so it circled the arena forever, and the
    /// player's attack button resolved to null and did nothing at all. From the outside it looked
    /// like an AI bug and a missing-visuals bug; it was neither.
    /// </para>
    /// <para>
    /// A null reference in a configuration asset is never acceptable, so this fails loudly rather
    /// than warning. It runs as part of setup and is asserted by an edit-mode test.
    /// </para>
    /// </remarks>
    public static class AssetIntegrityValidator
    {
        /// <summary>Runs the check and reports to the console.</summary>
        [MenuItem(EditorMenus.Validate + "Check Asset References")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> problems = FindBrokenReferences();

            if (problems.Count == 0)
            {
                Debug.Log("[Adaptive Boss Arena] Asset references intact.");
                return;
            }

            foreach (string problem in problems)
            {
                Debug.LogError($"[Adaptive Boss Arena] Broken reference: {problem}");
            }
        }

        /// <summary>
        /// Finds every unresolved reference across the shipped configuration assets.
        /// </summary>
        /// <returns>Human-readable descriptions, empty when everything resolves.</returns>
        public static IReadOnlyList<string> FindBrokenReferences()
        {
            var problems = new List<string>();

            ValidatePlayerConfigs(problems);
            ValidateBossConfigs(problems);

            return problems;
        }

        private static void ValidatePlayerConfigs(ICollection<string> problems)
        {
            foreach (PlayerConfig config in LoadAll<PlayerConfig>())
            {
                AttackDefinition[] chain = config.LightComboChain;

                if (chain == null || chain.Length == 0)
                {
                    problems.Add($"{config.name}: light combo chain is empty.");
                }
                else
                {
                    for (int i = 0; i < chain.Length; i++)
                    {
                        if (chain[i] == null)
                        {
                            problems.Add($"{config.name}: light combo chain entry {i} is unresolved.");
                        }
                    }
                }

                if (config.HeavyAttack == null)
                {
                    problems.Add($"{config.name}: heavy attack is unresolved.");
                }

                if (config.SpecialAttack == null)
                {
                    problems.Add($"{config.name}: special attack is unresolved.");
                }
            }
        }

        private static void ValidateBossConfigs(ICollection<string> problems)
        {
            foreach (BossConfig config in LoadAll<BossConfig>())
            {
                if (config.ReactionProfile == null)
                {
                    // Without this the boss refuses to run at all, by design.
                    problems.Add($"{config.name}: reaction profile is unresolved.");
                }

                BossPhaseDefinition[] phases = config.Phases;

                if (phases == null || phases.Length == 0)
                {
                    problems.Add($"{config.name}: has no phases.");
                    continue;
                }

                for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
                {
                    ValidatePhase(config.name, phaseIndex, phases[phaseIndex], problems);
                }

                CounterStrategyIntegrity(config, problems);
            }
        }

        private static void ValidatePhase(
            string configName,
            int phaseIndex,
            BossPhaseDefinition phase,
            ICollection<string> problems)
        {
            AttackDefinition[] attacks = phase.Attacks;

            if (attacks == null || attacks.Length == 0)
            {
                problems.Add(
                    $"{configName}: phase {phaseIndex} ('{phase.Name}') has no attacks, so the boss " +
                    "can never attack during it.");
                return;
            }

            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] == null)
                {
                    problems.Add($"{configName}: phase {phaseIndex} attack {i} is unresolved.");
                }
            }
        }

        private static void CounterStrategyIntegrity(BossConfig config, ICollection<string> problems)
        {
            Learning.CounterStrategy[] strategies = config.CounterStrategies;

            if (strategies == null)
            {
                return;
            }

            for (int i = 0; i < strategies.Length; i++)
            {
                if (strategies[i] == null)
                {
                    problems.Add($"{config.name}: counter-strategy {i} is unresolved.");
                    continue;
                }

                // A strategy the player cannot perceive is indistinguishable from the boss cheating,
                // so a missing tell is a genuine defect rather than a cosmetic omission.
                if (string.IsNullOrWhiteSpace(strategies[i].TellMessage))
                {
                    problems.Add(
                        $"{strategies[i].name}: has no tell message, so its adaptation would be invisible.");
                }
            }
        }

        /// <summary>Loads every asset of a type in the project.</summary>
        private static IEnumerable<TAsset> LoadAll<TAsset>() where TAsset : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }
}
