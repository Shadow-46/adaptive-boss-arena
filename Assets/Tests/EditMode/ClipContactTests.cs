using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEditor;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests how an attack clip's moment of contact is found from its weapon hand's motion.
    /// </summary>
    /// <remarks>
    /// Every clip used to be assumed to connect 45 per cent of the way through. The Mixamo attacks range from a
    /// one-second cut to a two-second leap, so a single guess put the blade's arc visibly before or after the
    /// hit on most of them. A swing is fastest where it strikes; that is what is measured.
    /// </remarks>
    [TestFixture]
    public sealed class ClipContactTests
    {
        [Test]
        public void ContactIsWhereTheHandMovesFastest()
        {
            float[] speeds = { 0.1f, 0.2f, 0.5f, 2.4f, 1.1f, 0.3f, 0.2f, 0.1f, 0.1f };

            Assert.AreEqual(3f / 8f, ClipContact.PeakFraction(speeds), 0.0001f);
        }

        [Test]
        public void AFlourishAtTheVeryStartOrEndIsNotTakenForTheStrike()
        {
            // A clip that whips the hand back to guard in its last frames must not be timed as striking at the end.
            float[] speeds = { 3f, 0.4f, 0.9f, 1.2f, 0.8f, 0.3f, 0.2f, 0.4f, 5f };

            float contact = ClipContact.PeakFraction(speeds);

            Assert.GreaterOrEqual(contact, ClipContact.EarliestContact);
            Assert.LessOrEqual(contact, ClipContact.LatestContact);
            Assert.AreEqual(3f / 8f, contact, 0.0001f);
        }

        [Test]
        public void TooFewSamplesFallBackToTheDefault()
        {
            Assert.AreEqual(ClipContact.DefaultContact, ClipContact.PeakFraction(new[] { 1f, 2f }), 0.0001f);
            Assert.AreEqual(ClipContact.DefaultContact, ClipContact.PeakFraction(null), 0.0001f);
        }

        [TestCase("DefaultPlayerAnimation")]
        [TestCase("DefaultBossAnimation")]
        public void EveryBoundAttackStrikesAtItsOwnMeasuredMoment(string configName)
        {
            var config = AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(
                "Assets/_Project/ScriptableObjects/Config/" + configName + ".asset");
            Assume.That(config != null && config.RigPrefab != null, "The character is not rigged.");

            var serialized = new SerializedObject(config);
            SerializedProperty bindings = serialized.FindProperty("_attackClips");
            Assert.Greater(bindings.arraySize, 0, configName + " binds no attacks.");

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                float contact = binding.FindPropertyRelative("_contactFraction").floatValue;
                string state = binding.FindPropertyRelative("_state").stringValue;

                Assert.That(contact, Is.InRange(ClipContact.EarliestContact, ClipContact.LatestContact),
                    $"{configName}: the {state} clip has no measured contact ({contact:F2}).");
            }
        }
    }
}
