using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Asserts the imported character art arrives in the shape the game depends on.
    /// </summary>
    /// <remarks>
    /// Import settings fail silently. A rig that falls back to Generic still imports, still plays in
    /// the preview, and simply cannot drive the game's Humanoid body or have its hand bone found -
    /// and nothing but running the game would show it. These pin the settings the code relies on.
    /// </remarks>
    [TestFixture]
    public sealed class ThirdPartyCharacterImportTests
    {
        private const string Library1 =
            "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";

        private const string Library2 =
            "Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";

        private static IEnumerable<AnimationClip> ClipsIn(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"));

        [TestCase(Library1)]
        [TestCase(Library2)]
        public void EachLibraryImportsAValidHumanoidAvatar(string path)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

            Assert.IsNotNull(avatar, $"{path} produced no avatar.");
            Assert.IsTrue(avatar.isHuman, $"{path} imported as a non-humanoid rig.");
            Assert.IsTrue(avatar.isValid, $"{path} imported an invalid avatar; its bones did not map.");
        }

        [Test]
        public void TheClipsTheGameUsesArePresent()
        {
            var names = new HashSet<string>(ClipsIn(Library1).Concat(ClipsIn(Library2)).Select(c => c.name));

            string[] required =
            {
                "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Roll", "Sword_Idle",
                "Sword_Attack", "Hit_Chest", "Death01", "Sword_Regular_A", "Sword_Regular_B",
                "Sword_Regular_C", "Sword_Block", "Hit_Knockback", "LayToIdle"
            };

            foreach (string clip in required)
            {
                Assert.IsTrue(
                    names.Contains(clip),
                    "Required clip " + clip + " is missing. Present: " + string.Join(", ", names.OrderBy(n => n)));
            }
        }

        [Test]
        public void LocomotionLoopsAndAttacksPlayOnce()
        {
            Dictionary<string, AnimationClip> clips = ClipsIn(Library1)
                .Concat(ClipsIn(Library2))
                .GroupBy(c => c.name)
                .ToDictionary(g => g.Key, g => g.First());

            Assert.IsTrue(clips["Walk_Loop"].isLooping, "Walking does not loop.");
            Assert.IsTrue(clips["Sword_Idle"].isLooping, "The sword idle does not loop.");
            Assert.IsFalse(clips["Sword_Regular_A"].isLooping, "An attack loops.");
            Assert.IsFalse(clips["Roll"].isLooping, "The roll loops.");
        }
    }
}
