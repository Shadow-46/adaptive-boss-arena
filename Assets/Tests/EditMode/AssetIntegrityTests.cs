using System.Collections.Generic;
using AdaptiveBossArena.Editor;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts that the shipped configuration assets actually resolve their references.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test exists because of a failure that every other check missed. Regenerating the attack
    /// assets orphaned their identifiers, leaving both the player and the boss with attack lists
    /// full of nulls. Compilation succeeded, the full suite passed, the scene built, and the game
    /// launched — but the boss could not attack and neither could the player.
    /// </para>
    /// <para>
    /// The lesson worth encoding: a project whose data is broken can look perfectly healthy from
    /// code. Asset integrity needs asserting explicitly, exactly like behaviour does.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class AssetIntegrityTests
    {
        [Test]
        public void EveryConfigurationAssetResolvesItsReferences()
        {
            IReadOnlyList<string> problems = AssetIntegrityValidator.FindBrokenReferences();

            Assert.IsEmpty(
                problems,
                "Configuration assets contain unresolved references. The game will run but be " +
                "unplayable:\n" + string.Join("\n", problems));
        }
    }
}
