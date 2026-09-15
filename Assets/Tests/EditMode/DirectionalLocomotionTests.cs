using System.Linq;
using AdaptiveBossArena.Combat.Feel;
using AdaptiveBossArena.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests that every clip in a fighter's locomotion blend steps the way the blend places it.
    /// </summary>
    /// <remarks>
    /// The brute's run clip stepped backwards, so it ran forwards on legs running away. File names said
    /// "run"; only measuring the feet showed it.
    /// </remarks>
    [TestFixture]
    public sealed class DirectionalLocomotionTests
    {
        [TestCase("KnightController")]
        [TestCase("BruteController")]
        public void EveryMovingClipTravelsTheWayItIsPlaced(string controllerName)
        {
            CharacterClipTable table = controllerName == "KnightController" ? CharacterClipTable.Knight : CharacterClipTable.Brute;
            Assume.That(AnimatorControllerBuilder.IsAvailable(table), "The licensed art is not in the project.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/_Project/ScriptableObjects/Animation/" + controllerName + ".controller");
            Assume.That(controller != null, "Setup has not generated the controller.");

            AnimatorState locomotion = controller.layers[0].stateMachine.states
                .Select(s => s.state)
                .First(s => s.name == CharacterAnimatorParameters.LocomotionState);

            var tree = locomotion.motion as BlendTree;
            Assert.IsNotNull(tree, "Locomotion is not a blend.");
            Assert.AreEqual(BlendTreeType.FreeformDirectional2D, tree.blendType, "Locomotion does not blend by direction.");

            int moving = 0;

            foreach (ChildMotion child in tree.children)
            {
                if (child.position.magnitude < 0.01f)
                {
                    continue;
                }

                moving++;
                Vector2 travel = ClipTravelMeasure.TravelOf(table, child.motion.name);

                Assert.Greater(Vector2.Dot(travel.normalized, child.position.normalized), 0.8f,
                    $"{child.motion.name} is placed at {child.position} but steps {travel}.");
            }

            Assert.GreaterOrEqual(moving, 5, "The blend is missing its back-steps or strafes.");
        }

        [Test]
        public void TheBrutesRunStepsForwards()
        {
            CharacterClipTable table = CharacterClipTable.Brute;
            Assume.That(AnimatorControllerBuilder.IsAvailable(table), "The licensed art is not in the project.");

            string run = table.Locomotion.Last().Clip;
            Assert.Greater(ClipTravelMeasure.TravelOf(table, run).y, 1f, run + " does not run forwards.");
        }
    }
}
