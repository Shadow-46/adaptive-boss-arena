using AdaptiveBossArena.Combat.Vitals;
using AdaptiveBossArena.Core.Combat;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the rules that stop knockdowns and launches being chained into a character who never recovers.
    /// </summary>
    [TestFixture]
    public sealed class ReactionGateTests
    {
        private const float Immunity = 2.5f;

        private static ReactionGate Floored(float at)
        {
            var gate = new ReactionGate(Immunity);
            gate.Admit(ImpactReaction.Knockdown, at);
            return gate;
        }

        [Test]
        public void AKnockdownOnAStandingCharacterLands()
        {
            var gate = new ReactionGate(Immunity);

            Assert.AreEqual(ImpactReaction.Knockdown, gate.Admit(ImpactReaction.Knockdown, 0f));
            Assert.AreEqual(Footing.Down, gate.Footing);
        }

        [Test]
        public void OnlyOneLaunchPerTripThroughTheAir()
        {
            var gate = new ReactionGate(Immunity);

            Assert.AreEqual(ImpactReaction.Launch, gate.Admit(ImpactReaction.Launch, 0f));
            Assert.AreEqual(ImpactReaction.Knockback, gate.Admit(ImpactReaction.Launch, 0.2f));
            Assert.AreEqual(ImpactReaction.Knockback, gate.Admit(ImpactReaction.Knockdown, 0.3f));
            Assert.AreEqual(Footing.Airborne, gate.Footing);
        }

        [Test]
        public void NothingHurtsABodyOnTheFloorOrGettingUp()
        {
            ReactionGate gate = Floored(0f);
            Assert.IsFalse(gate.CanBeHurt);
            Assert.AreEqual(ImpactReaction.None, gate.Admit(ImpactReaction.Launch, 0.1f));

            gate.BeginGettingUp();
            Assert.IsFalse(gate.CanBeHurt);
            Assert.IsFalse(gate.AdmitsStagger);
        }

        [Test]
        public void LandingFromALaunchFloorsTheCharacter()
        {
            var gate = new ReactionGate(Immunity);
            gate.Admit(ImpactReaction.Launch, 0f);

            Assert.IsTrue(gate.CanBeHurt, "A thrown body is still hittable; only the relaunch is refused.");

            gate.Land();

            Assert.AreEqual(Footing.Down, gate.Footing);
            Assert.IsFalse(gate.CanBeHurt);
        }

        [Test]
        public void KnockdownsBecomeKnockbackForAWhileAfterGettingUp()
        {
            ReactionGate gate = Floored(0f);
            gate.BeginGettingUp();
            gate.FinishGettingUp(1.5f);

            Assert.AreEqual(ImpactReaction.Knockback, gate.Admit(ImpactReaction.Knockdown, 1.6f));
            Assert.AreEqual(ImpactReaction.Knockback, gate.Admit(ImpactReaction.Launch, 1.5f + Immunity - 0.01f));
            Assert.AreEqual(Footing.Standing, gate.Footing);

            Assert.AreEqual(
                ImpactReaction.Knockdown, gate.Admit(ImpactReaction.Knockdown, 1.5f + Immunity + 0.01f),
                "Immunity that never ends would make the knockdown a one-off per fight.");
        }

        [Test]
        public void OrdinaryReactionsPassThroughUntouched()
        {
            var gate = new ReactionGate(Immunity);

            Assert.AreEqual(ImpactReaction.Knockback, gate.Admit(ImpactReaction.Knockback, 0f));
            Assert.AreEqual(ImpactReaction.None, gate.Admit(ImpactReaction.None, 0f));
            Assert.AreEqual(Footing.Standing, gate.Footing);
        }

        [Test]
        public void ASustainedBarrageCannotHoldTheCharacterDownPastTheCap()
        {
            // A launch or knockdown every tenth of a second for thirty seconds, against a character
            // whose states take the configured times. Every stretch without control must stay inside
            // the cap, however the hits fall.
            const float Air = 1.2f, Down = 0.9f, GetUp = 0.6f, Step = 0.1f;
            float cap = ReactionGate.WorstCaseSecondsWithoutControl(Air, Down, GetUp);

            var gate = new ReactionGate(Immunity);
            float lostSince = float.NaN, longest = 0f, stageEnds = 0f;

            for (int tick = 0; tick < 300; tick++)
            {
                float now = tick * Step;
                gate.Admit(tick % 2 == 0 ? ImpactReaction.Launch : ImpactReaction.Knockdown, now);

                if (gate.IsReacting && float.IsNaN(lostSince))
                {
                    lostSince = now;
                    stageEnds = now + (gate.Footing == Footing.Airborne ? Air : Down);
                }

                if (gate.IsReacting && now >= stageEnds)
                {
                    switch (gate.Footing)
                    {
                        case Footing.Airborne: gate.Land(); stageEnds = now + Down; break;
                        case Footing.Down: gate.BeginGettingUp(); stageEnds = now + GetUp; break;
                        case Footing.GettingUp: gate.FinishGettingUp(now); break;
                    }
                }

                if (!gate.IsReacting && !float.IsNaN(lostSince))
                {
                    longest = System.Math.Max(longest, now - lostSince);
                    lostSince = float.NaN;
                }
            }

            Assert.Greater(longest, 0f, "The barrage never floored the character, so the test proved nothing.");
            Assert.LessOrEqual(longest, cap + Step * 3f);
        }
    }
}
