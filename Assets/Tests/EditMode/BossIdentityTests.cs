using AdaptiveBossArena.Core.Constants;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the boss's name as it is lettered on screen.
    /// </summary>
    [TestFixture]
    public sealed class BossIdentityTests
    {
        [Test]
        public void LettersAreSpacedAndWordsSpacedWider()
        {
            Assert.AreEqual("T H E   B L O O D - R E D", BossIdentity.Spaced("The Blood-Red"));
        }

        [Test]
        public void TheHealthBarCarriesTheNameAlone()
        {
            // The full title, spaced, is wider than the bar.
            Assert.AreEqual("I G R I S", BossIdentity.HealthBarTitle);
        }

        [Test]
        public void TheEntranceGivesTheTitleItsOwnLine()
        {
            StringAssert.StartsWith("I G R I S\n", BossIdentity.EntranceTitle);
            StringAssert.Contains("C O M M A N D E R", BossIdentity.EntranceTitle);
        }
    }
}
