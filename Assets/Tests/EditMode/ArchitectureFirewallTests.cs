using System.Collections.Generic;
using AdaptiveBossArena.Editor;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Guards the project's central design constraint: the boss cannot read player input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the most important test in the project, and the one least likely to fail for an
    /// ordinary reason. It fails when somebody adds an assembly reference to make an inconvenient
    /// compile error go away, and in doing so quietly converts the boss from something that learns
    /// into something that peeks.
    /// </para>
    /// <para>
    /// If this test fails, the fix is never to relax the test. It is to move whatever needed player
    /// state behind
    /// <see cref="AdaptiveBossArena.Core.Perception.IObservablePlayer"/>, and to check that the
    /// state in question is something a human opponent could actually perceive.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class ArchitectureFirewallTests
    {
        [Test]
        public void AiAndLearningAssemblies_CannotReachPlayerInput()
        {
            IReadOnlyList<string> violations = ArchitectureValidator.FindViolations();

            Assert.IsEmpty(
                violations,
                "The architecture firewall has been breached:\n" + string.Join("\n", violations));
        }
    }
}
