using AdaptiveBossArena.Combat.Vitals;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the meter that rewards good defence with a stronger special.
    /// </summary>
    /// <remarks>
    /// The contract that matters: focus is earned, never granted; it only empowers a special when
    /// genuinely full; and a stagger takes all of it. Those three rules are what make it a risk worth
    /// managing rather than a bar that fills on its own.
    /// </remarks>
    [TestFixture]
    public sealed class FocusMeterTests
    {
        private static FocusMeter Meter() => new FocusMeter(maximum: 100f, gainPerDeflect: 40f, gainPerPerfectDodge: 30f);

        [Test]
        public void AMeterStartsEmpty()
        {
            Assert.AreEqual(0f, Meter().Current);
            Assert.IsFalse(Meter().IsFull);
        }

        [Test]
        public void DefendingWellFillsIt()
        {
            FocusMeter meter = Meter();
            meter.AddFromDeflect();
            meter.AddFromPerfectDodge();

            Assert.AreEqual(70f, meter.Current, 0.001f);
            Assert.IsFalse(meter.IsFull);
        }

        [Test]
        public void FocusNeverExceedsTheMaximum()
        {
            FocusMeter meter = Meter();
            for (int i = 0; i < 10; i++)
            {
                meter.AddFromDeflect();
            }

            Assert.AreEqual(100f, meter.Current, 0.001f);
            Assert.IsTrue(meter.IsFull);
        }

        [Test]
        public void ASpecialCanOnlyBeEmpoweredWhenTheMeterIsFull()
        {
            FocusMeter meter = Meter();
            meter.AddFromDeflect();

            Assert.IsFalse(meter.TryConsumeFull(), "A partial meter must not empower.");

            meter.AddFromDeflect();
            meter.AddFromDeflect(); // now full

            Assert.IsTrue(meter.TryConsumeFull(), "A full meter should empower and empty.");
            Assert.AreEqual(0f, meter.Current);
        }

        [Test]
        public void AStaggerTakesAllOfIt()
        {
            FocusMeter meter = Meter();
            meter.AddFromDeflect();
            meter.AddFromDeflect();

            meter.Reset();

            Assert.AreEqual(0f, meter.Current);
        }
    }
}
