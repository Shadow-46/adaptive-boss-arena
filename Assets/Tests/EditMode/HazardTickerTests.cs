using AdaptiveBossArena.Combat;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the two fairness rules of a ground hazard: it warns before it hurts, and it bites on a
    /// fixed cadence rather than every frame.
    /// </summary>
    [TestFixture]
    public sealed class HazardTickerTests
    {
        private const float Step = 0.02f;

        private static HazardTicker Ticker() =>
            new HazardTicker(warningSeconds: 0.5f, activeSeconds: 2f, fadeSeconds: 0.5f, damageIntervalSeconds: 0.4f);

        [Test]
        public void AHazardWarnsBeforeItHurts()
        {
            HazardTicker ticker = Ticker();
            Assert.AreEqual(HazardTicker.Stage.Warning, ticker.CurrentStage);

            // 24 steps = 0.48s, still inside the 0.5s warning: no damage may fire.
            bool damaged = false;
            for (int i = 0; i < 24; i++)
            {
                damaged |= ticker.Advance(Step);
            }

            Assert.IsFalse(damaged, "The warning window must never deal damage.");
        }

        [Test]
        public void ItBitesPromptlyOnceActive()
        {
            HazardTicker ticker = Ticker();

            float firstDamageAt = -1f;
            float t = 0f;
            for (int i = 0; i < 200 && firstDamageAt < 0f; i++)
            {
                t += Step;
                if (ticker.Advance(Step))
                {
                    firstDamageAt = t;
                }
            }

            // The first bite lands just after the 0.5s warning, not a full interval into the active
            // window — a zone forming under a standing player hurts at once.
            Assert.GreaterOrEqual(firstDamageAt, 0.5f);
            Assert.Less(firstDamageAt, 0.58f);
        }

        [Test]
        public void DamageTicksArePacedByTheInterval()
        {
            HazardTicker ticker = Ticker();

            int ticks = 0;
            for (int i = 0; i < 200; i++) // 4s, well past the 3s total life
            {
                if (ticker.Advance(Step))
                {
                    ticks++;
                }
            }

            // 2s of active at a 0.4s interval is about five ticks plus the immediate one on entry;
            // bounded rather than exact so a boundary frame either way does not fail the contract.
            Assert.GreaterOrEqual(ticks, 5);
            Assert.LessOrEqual(ticks, 7);
        }

        [Test]
        public void AHazardEventuallyExpires()
        {
            HazardTicker ticker = Ticker();

            for (int i = 0; i < 200; i++)
            {
                ticker.Advance(Step);
            }

            Assert.IsTrue(ticker.IsExpired);
            Assert.IsFalse(ticker.Advance(Step), "An expired hazard deals no further damage.");
        }
    }
}
