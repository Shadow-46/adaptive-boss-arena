using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AdaptiveBossArena.Editor
{
    /// <summary>
    /// Verifies that the assembly graph still enforces the boss's inability to cheat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The project's central design guarantee is that the boss cannot read player input. That is not
    /// enforced by convention but by the assembly graph: the AI and Learning assemblies do not
    /// reference the Player assembly or the Input System, so the code to cheat cannot be written
    /// there and still compile.
    /// </para>
    /// <para>
    /// A guarantee of that kind decays the moment someone adds a reference to resolve an
    /// inconvenient compile error. This validator makes that change fail loudly instead of
    /// silently dismantling the design. It is exercised by an edit-mode test, so the check runs in
    /// continuous integration rather than depending on anyone remembering to invoke it.
    /// </para>
    /// <para>
    /// References are followed transitively. A reference laundered through an intermediate assembly
    /// is the same violation as a direct one.
    /// </para>
    /// </remarks>
    public static class ArchitectureValidator
    {
        /// <summary>Assemblies that must remain unable to observe the player directly.</summary>
        private static readonly string[] RestrictedAssemblies =
        {
            "AdaptiveBossArena.AI",
            "AdaptiveBossArena.Learning"
        };

        /// <summary>Assemblies the restricted set must never be able to reach.</summary>
        private static readonly string[] ForbiddenAssemblies =
        {
            "AdaptiveBossArena.Player",
            "Unity.InputSystem"
        };

        /// <summary>Runs the check and reports the result to the console.</summary>
        [MenuItem(EditorMenus.Validate + "Check Architecture Firewall")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> violations = FindViolations();

            if (violations.Count == 0)
            {
                Debug.Log(
                    "[Adaptive Boss Arena] Architecture firewall intact: the AI and Learning " +
                    "assemblies cannot reach player input.");
                return;
            }

            foreach (string violation in violations)
            {
                Debug.LogError($"[Adaptive Boss Arena] Architecture violation: {violation}");
            }
        }

        /// <summary>
        /// Finds every path by which a restricted assembly can reach a forbidden one.
        /// </summary>
        /// <returns>Human-readable descriptions of each violation, empty when the graph is sound.</returns>
        public static IReadOnlyList<string> FindViolations()
        {
            Dictionary<string, List<string>> graph = BuildReferenceGraph();
            var violations = new List<string>();

            foreach (string restricted in RestrictedAssemblies)
            {
                if (!graph.ContainsKey(restricted))
                {
                    violations.Add(
                        $"Assembly '{restricted}' was not found. The firewall cannot be verified.");
                    continue;
                }

                foreach (string forbidden in ForbiddenAssemblies)
                {
                    List<string> path = FindPath(graph, restricted, forbidden);

                    if (path != null)
                    {
                        violations.Add(
                            $"'{restricted}' can reach '{forbidden}' via {string.Join(" -> ", path)}. " +
                            "This would let the boss observe player input directly, which the design forbids.");
                    }
                }
            }

            return violations;
        }

        /// <summary>Reads every assembly definition in the project into a reference graph.</summary>
        private static Dictionary<string, List<string>> BuildReferenceGraph()
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var guidToName = new Dictionary<string, string>(StringComparer.Ordinal);
            var parsed = new List<AssemblyDefinitionData>();

            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);

                if (asset == null)
                {
                    continue;
                }

                var data = JsonUtility.FromJson<AssemblyDefinitionData>(asset.text);
                if (data == null || string.IsNullOrEmpty(data.name))
                {
                    continue;
                }

                parsed.Add(data);
                guidToName[guid] = data.name;
            }

            foreach (AssemblyDefinitionData data in parsed)
            {
                var references = new List<string>();

                foreach (string reference in data.references ?? Array.Empty<string>())
                {
                    references.Add(ResolveReference(reference, guidToName));
                }

                graph[data.name] = references;
            }

            return graph;
        }

        /// <summary>
        /// Converts a reference entry to an assembly name.
        /// </summary>
        /// <remarks>
        /// Unity writes references either as plain names or, when "Use GUIDs" is enabled, as
        /// <c>GUID:</c>-prefixed identifiers. Both forms must be understood or the validator would
        /// quietly pass a project that had simply switched representation.
        /// </remarks>
        private static string ResolveReference(string reference, IReadOnlyDictionary<string, string> guidToName)
        {
            const string guidPrefix = "GUID:";

            if (!reference.StartsWith(guidPrefix, StringComparison.Ordinal))
            {
                return reference;
            }

            string guid = reference.Substring(guidPrefix.Length);
            return guidToName.TryGetValue(guid, out string name) ? name : reference;
        }

        /// <summary>Breadth-first search for a reference path between two assemblies.</summary>
        /// <returns>The path including both endpoints, or null when none exists.</returns>
        private static List<string> FindPath(
            IReadOnlyDictionary<string, List<string>> graph,
            string from,
            string to)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { from };
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();

                if (!graph.TryGetValue(current, out List<string> references))
                {
                    continue;
                }

                foreach (string reference in references)
                {
                    if (!visited.Add(reference))
                    {
                        continue;
                    }

                    previous[reference] = current;

                    if (reference == to)
                    {
                        return ReconstructPath(previous, from, to);
                    }

                    queue.Enqueue(reference);
                }
            }

            return null;
        }

        /// <summary>Walks the predecessor map backwards to produce a readable path.</summary>
        private static List<string> ReconstructPath(
            IReadOnlyDictionary<string, string> previous,
            string from,
            string to)
        {
            var path = new List<string> { to };
            string current = to;

            while (current != from && previous.TryGetValue(current, out string parent))
            {
                path.Insert(0, parent);
                current = parent;
            }

            return path;
        }

        /// <summary>Minimal shape of an assembly definition file, for JSON deserialisation.</summary>
        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            /// <summary>Assembly name.</summary>
            public string name = string.Empty;

            /// <summary>Referenced assemblies, by name or GUID.</summary>
            public string[] references = Array.Empty<string>();
        }
    }
}
