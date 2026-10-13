using AdaptiveBossArena.Editor.Art;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests which meshes of an imported character are its body, its weapon, or spares to remove.
    /// </summary>
    /// <remarks>
    /// The corrupted knight ships its lower levels of detail and its own sword alongside its body. Drawn together the
    /// levels doubled the figure; stripping the wrong one would leave the boss without a body or without his sword.
    /// </remarks>
    [TestFixture]
    public sealed class SpareMeshTests
    {
        [TestCase("IgrisBoss_Body0_NLOD1")]
        [TestCase("IgrisBoss_Lowmesh0")]
        [TestCase("IgrisBoss_Weapon0_NLOD1")]
        public void LowerDetailIsSpare(string mesh)
        {
            Assert.IsTrue(SpareMeshes.IsSpare(mesh));
        }

        [Test]
        public void TheKnightKeepsHisOwnSword()
        {
            Assert.IsFalse(SpareMeshes.IsSpare("IgrisBoss_Weapon0_NLOD0"), "The sword was stripped.");
            Assert.IsTrue(SpareMeshes.IsWeapon("IgrisBoss_Weapon0_NLOD0"));
            Assert.IsFalse(SpareMeshes.IsBody("IgrisBoss_Weapon0_NLOD0"), "The sword would be painted as armour.");
        }

        [TestCase("IgrisBoss_Body0_NLOD0")]
        [TestCase("Paladin_J_Nordstrom")]
        [TestCase("Warrok")]
        public void TheBodyIsKept(string mesh)
        {
            Assert.IsFalse(SpareMeshes.IsSpare(mesh));
            Assert.IsTrue(SpareMeshes.IsBody(mesh));
        }
    }
}
