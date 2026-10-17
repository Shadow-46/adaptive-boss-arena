using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Core.Combat;
using AdaptiveBossArena.Game;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Pins how hard each beat of the fight is felt: in the hands through the controller, and in the frame.
    /// </summary>
    public sealed class ImpactFeelTests
    {
        private static CombatEvent Landed(CombatantTeam attacker, float damage, DamageType type = DamageType.BossMelee) =>
            new CombatEvent { Kind = CombatEventKind.AttackLanded, Actor = attacker, Magnitude = damage, DamageType = type };

        [Test]
        public void ABlowTaken_ThumpsHarderTheMoreItHurts()
        {
            Assert.IsTrue(RumbleDirector.TryPulseFor(Landed(CombatantTeam.Boss, 6f), out RumblePulse light));
            Assert.IsTrue(RumbleDirector.TryPulseFor(Landed(CombatantTeam.Boss, 24f), out RumblePulse heavy));

            Assert.Greater(heavy.Low, light.Low);
            Assert.Greater(heavy.Seconds, light.Seconds);
        }

        [Test]
        public void ABlowTaken_IsFeltMoreThanABlowLanded()
        {
            RumbleDirector.TryPulseFor(Landed(CombatantTeam.Boss, 12f), out RumblePulse taken);
            RumbleDirector.TryPulseFor(Landed(CombatantTeam.Player, 12f, DamageType.Heavy), out RumblePulse dealt);

            Assert.Greater(taken.Strength, dealt.Strength);
        }

        [Test]
        public void ADeflect_IsAllEdge()
        {
            Assert.IsTrue(RumbleDirector.TryPulseFor(
                new CombatEvent { Kind = CombatEventKind.Deflected, Actor = CombatantTeam.Player }, out RumblePulse pulse));
            Assert.Greater(pulse.High, pulse.Low, "The ring of steel is the high motor, not the low.");
        }

        [Test]
        public void TheExecution_RollsLongerThanAnyBlow()
        {
            RumbleDirector.TryPulseFor(new CombatEvent { Kind = CombatEventKind.Riposte }, out RumblePulse riposte);
            RumbleDirector.TryPulseFor(Landed(CombatantTeam.Boss, 30f), out RumblePulse worst);

            Assert.Greater(riposte.Seconds, worst.Seconds);
        }

        [Test]
        public void QuietBeats_AreNotFelt()
        {
            Assert.IsFalse(RumbleDirector.TryPulseFor(
                new CombatEvent { Kind = CombatEventKind.AttackStarted }, out _));
            Assert.IsFalse(RumbleDirector.TryPulseFor(
                new CombatEvent { Kind = CombatEventKind.GuardRaised }, out _));
        }

        [Test]
        public void TheFrame_IsKnockedHardestWhenTheKnightIsStruck()
        {
            float taken = CombatEffectsDirector.KickFor(CombatantTeam.Boss, ImpactFlavour.Heavy);
            float heavy = CombatEffectsDirector.KickFor(CombatantTeam.Player, ImpactFlavour.Heavy);
            float light = CombatEffectsDirector.KickFor(CombatantTeam.Player, ImpactFlavour.Light);

            Assert.Greater(taken, heavy);
            Assert.Greater(heavy, light);
            Assert.Greater(light, 0f);
        }
    }
}
