using System.IO;
using AdaptiveBossArena.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts the recorded sounds reach the moments they were chosen for.
    /// </summary>
    /// <remarks>
    /// The player heard the synthesised sounds as random. The downloads replace them cue by cue, matched by file
    /// name, and a pattern that matched nothing would leave the old sound playing with no error anywhere.
    /// </remarks>
    [TestFixture]
    public sealed class AudioMappingTests
    {
        [TestCase(@"^impactWood_heavy_\d+$")]
        [TestCase(@"^impactMetal_heavy_\d+$")]
        [TestCase(@"^impactBell_heavy_\d+$")]
        [TestCase(@"^impactPunch_heavy_\d+$")]
        [TestCase(@"^footstep_concrete_\d+$")]
        public void EachKenneyCueHasSeveralTakes(string pattern)
        {
            Assume.That(Directory.Exists(AudioAssetBuilder.SoundFolder + "/Kenney"), "The Kenney packs are not downloaded here.");

            AudioClip[] clips = AudioAssetBuilder.ClipsMatching(pattern);

            Assert.GreaterOrEqual(clips.Length, 3, pattern + " found too few takes to vary.");
        }

        [TestCase(@"^507470_take\d+$", 5)]
        [TestCase(@"^(317849|471097)_take\d+$", 10)]
        [TestCase(@"^(125405|869056)_take\d+$", 10)]
        public void EachCompilationWasCutIntoSingleSounds(string pattern, int atLeast)
        {
            // A compilation played whole fires a burst of swings for one blow. Each take is one sound, and short.
            Assume.That(Directory.Exists(AudioAssetBuilder.SoundFolder + "/Freesound/Takes"), "The Freesound takes are not here.");

            AudioClip[] takes = AudioAssetBuilder.ClipsMatching(pattern);

            Assert.GreaterOrEqual(takes.Length, atLeast, pattern + " has too few takes.");

            foreach (AudioClip take in takes)
            {
                Assert.Less(take.length, 2.5f, take.name + " is long enough to hold more than one sound.");
            }
        }

        [Test]
        public void TheBlockAndTheParryNeverShareASound()
        {
            // A block is the shield's wood, a parry the bright ring: the player must hear which one happened.
            Assume.That(Directory.Exists(AudioAssetBuilder.SoundFolder + "/Kenney"), "The Kenney packs are not downloaded here.");

            AudioClip[] block = AudioAssetBuilder.ClipsMatching(@"^impactWood_heavy_\d+$");
            AudioClip[] parry = AudioAssetBuilder.ClipsMatching(@"^impactBell_heavy_\d+$");

            CollectionAssert.IsNotEmpty(block);
            CollectionAssert.IsNotEmpty(parry);
            CollectionAssert.AreNotEquivalent(block, parry);
        }
    }
}
