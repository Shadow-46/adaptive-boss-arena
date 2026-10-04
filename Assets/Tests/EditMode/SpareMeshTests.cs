using AdaptiveBossArena.Editor.Art;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests which meshes of an imported character are kept as its body.
    /// </summary>
    /// <remarks>
    /// The corrupted knight ships its lower levels of detail and its own sword alongside its body. Drawn together
    /// they doubled the figure; stripping the wrong one would leave the boss without a body.
    /// </remarks>
    [TestFixture]
    public sealed class SpareMeshTests
    {
        [TestCase("IgrisBoss_Body0_NLOD1")]
        [TestCase("IgrisBoss_Lowmesh0")]
        [TestCase("IgrisBoss_Weapon0_NLOD0")]
        [TestCase("IgrisBoss_Weapon0_NLOD1")]
        public void LowerDetailAndBuiltInWeaponsAreSpare(string mesh)
        {
            Assert.IsTrue(SpareMeshes.IsSpare(mesh));
        }

        [TestCase("IgrisBoss_Body0_NLOD0")]
        [TestCase("Paladin_J_Nordstrom")]
        [TestCase("Warrok")]
        public void TheBodyIsKept(string mesh)
        {
            Assert.IsFalse(SpareMeshes.IsSpare(mesh));
        }
    }
}
