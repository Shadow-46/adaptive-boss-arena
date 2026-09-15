using AdaptiveBossArena.Combat;
using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests the ground warning's shape: bright at its boundary, faint inside, and never past the hitbox.
    /// </summary>
    /// <remarks>
    /// It was a shape filled at one opacity, which read as a coloured disc lying on the floor.
    /// </remarks>
    [TestFixture]
    public sealed class AttackOverlayShapeTests
    {
        [TestCase("BossSlam")]
        [TestCase("BossPerilousOverhead")]
        public void TheRimIsBrightAndTheInsideFaint(string attackName)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/ScriptableObjects/Attacks/" + attackName + ".asset");
            Assume.That(attack != null, "The attack assets have not been generated.");

            Mesh mesh = AttackShapeMesh.Build(attack);

            try
            {
                Color[] colours = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, colours.Length, "The overlay carries no falloff.");

                float faintest = 1f, brightest = 0f;

                foreach (Color c in colours)
                {
                    faintest = Mathf.Min(faintest, c.a);
                    brightest = Mathf.Max(brightest, c.a);
                }

                Assert.Less(faintest, 0.2f, "The inside of the warning is not faint.");
                Assert.Greater(brightest, 0.9f, "The rim of the warning is not bright.");

                float reach = attack.Shape == AttackShape.Box
                    ? attack.BoxHalfExtents.magnitude + attack.Offset.magnitude
                    : attack.Range;

                foreach (Vector3 v in mesh.vertices)
                {
                    Assert.LessOrEqual(new Vector2(v.x, v.z).magnitude, reach + 0.001f, "The warning reaches past the hitbox.");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
