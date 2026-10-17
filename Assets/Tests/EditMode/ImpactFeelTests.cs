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

        [Test]
        public void APhaseTitle_IsItsNumeralAndItsName()
        {
            Assert.AreEqual("II · PRESSING", CombatEffectsDirector.PhaseTitle(1, "Pressing"));
            Assert.AreEqual("IV · FRENZIED", CombatEffectsDirector.PhaseTitle(3, "Frenzied"));
            Assert.AreEqual("III", CombatEffectsDirector.PhaseTitle(2, string.Empty));
        }

        [Test]
        public void TheCameraIsBack_BeforeTheBossCanStrikeAgain()
        {
            // The cut must end inside the boss's own transformation, during which he is invulnerable and cannot
            // swing, so the phase's shockwave is always seen winding up from behind the knight.
            Assert.Less(CombatEffectsDirector.PhaseShotSeconds, 1.3f);
        }

        [Test]
        public void Lightning_FlashesTwice_ThenIsGone()
        {
            Assert.AreEqual(0f, ArenaAtmosphere.LightningFlash(float.PositiveInfinity));
            Assert.Greater(ArenaAtmosphere.LightningFlash(0f), 0.9f);

            float trough = ArenaAtmosphere.LightningFlash(0.12f);
            float restrike = ArenaAtmosphere.LightningFlash(0.135f);

            Assert.Greater(restrike, trough, "Lightning flickers: a second flash follows the first.");
            Assert.Less(ArenaAtmosphere.LightningFlash(0.6f), 0.01f);
        }

        [Test]
        public void Thunder_CracksThenRollsAway()
        {
            float[] thunder = AdaptiveBossArena.Utilities.Audio.ScoreGenerator.RenderThunder();
            int quarter = thunder.Length / 4;
            float early = 0f;
            float late = 0f;

            for (int i = 0; i < quarter; i++)
            {
                early = UnityEngine.Mathf.Max(early, UnityEngine.Mathf.Abs(thunder[i]));
                late = UnityEngine.Mathf.Max(late, UnityEngine.Mathf.Abs(thunder[thunder.Length - 1 - i]));
            }

            Assert.Greater(early, late * 2f);
        }
    }
}
