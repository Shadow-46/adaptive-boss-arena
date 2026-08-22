using System.Collections.Generic;
using System.Reflection;
using AdaptiveBossArena.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts that copying a hit description does not quietly lose any of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DamageInfo"/> is init-only and C# 9 has no <c>with</c> for structs, so every copy
    /// is written out by hand in one private helper. Its own documentation says a field added later
    /// must not be forgotten there — and nothing enforced that. Every hit in the game passes through
    /// <c>AtPoint</c> on its way to a hurtbox, so a field missing from the copy is a field that
    /// silently does nothing, everywhere, while still compiling and still reading correctly at the
    /// declaration site.
    /// </para>
    /// <para>
    /// Written against the property list by reflection rather than field by field, so it covers
    /// fields that do not exist yet. That is the whole point: the next person to add one should not
    /// have to remember this file exists.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class DamageInfoCopyTests
    {
        /// <summary>The three fields callers are allowed to change, which are excluded from checks.</summary>
        private static readonly HashSet<string> DeliberatelyVaried = new HashSet<string>
        {
            nameof(DamageInfo.Amount),
            nameof(DamageInfo.HitPoint),
            nameof(DamageInfo.HitDirection)
        };

        /// <summary>A hit with every field set away from its default, so a dropped one shows up.</summary>
        private static DamageInfo Distinctive() => new DamageInfo
        {
            Amount = 37f,
            Type = DamageType.BossProjectile,
            SourceTeam = CombatantTeam.Boss,
            SourceInstanceId = 4242,
            HitPoint = new Vector3(1f, 2f, 3f),
            HitDirection = Vector3.left,
            KnockbackSpeed = 9f,
            PoiseDamage = 55f,
            Stagger = StaggerStrength.Break,
            HitStopSeconds = 0.12f,
            IgnoresInvulnerability = true,
            Unblockable = true,
            Unparryable = true
        };

        private static void AssertCarriedOver(in DamageInfo source, in DamageInfo copy)
        {
            foreach (PropertyInfo property in typeof(DamageInfo).GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                if (DeliberatelyVaried.Contains(property.Name) || !property.CanRead)
                {
                    continue;
                }

                Assert.AreEqual(
                    property.GetValue(BoxOf(source)),
                    property.GetValue(BoxOf(copy)),
                    $"Copying a hit dropped '{property.Name}'. Add it to DamageInfo.Rebuild.");
            }
        }

        /// <summary>Boxes the struct once so reflection reads the same instance for every property.</summary>
        private static object BoxOf(in DamageInfo damage) => damage;

        [Test]
        public void AtPointKeepsEveryFieldItWasNotAskedToChange()
        {
            DamageInfo source = Distinctive();
            DamageInfo copy = source.AtPoint(new Vector3(9f, 9f, 9f), Vector3.up);

            AssertCarriedOver(source, copy);

            Assert.AreEqual(new Vector3(9f, 9f, 9f), copy.HitPoint);
            Assert.AreEqual(Vector3.up, copy.HitDirection);
            Assert.AreEqual(source.Amount, copy.Amount, 0.001f);
        }

        [Test]
        public void WithAmountKeepsEveryFieldItWasNotAskedToChange()
        {
            DamageInfo source = Distinctive();
            DamageInfo copy = source.WithAmount(96f);

            AssertCarriedOver(source, copy);

            Assert.AreEqual(96f, copy.Amount, 0.001f);
            Assert.AreEqual(source.HitPoint, copy.HitPoint);
            Assert.AreEqual(source.HitDirection, copy.HitDirection);
        }

        [Test]
        public void UnparryableSurvivesTheCopyEveryHitMakes()
        {
            // Named separately because it is the field this test was written for: the executor calls
            // AtPoint on the way to every hurtbox, so an unparryable execution that lost the flag
            // here would be deflectable in play while looking correct in the asset.
            Assert.IsTrue(Distinctive().AtPoint(Vector3.zero, Vector3.forward).Unparryable);
        }
    }
}
