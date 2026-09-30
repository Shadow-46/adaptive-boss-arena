using System.Collections;
using AdaptiveBossArena.Core.Constants;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the fighters' own light reaches them, and them alone.
    /// </summary>
    /// <remarks>
    /// The light exists to keep the side of a fighter the camera sees readable in the dark nave. Reaching the floor
    /// too, it would undo the moonlit room; missing a fighter or a drawn weapon, it would leave that one thing black.
    /// </remarks>
    [TestFixture]
    public sealed class CharacterLightTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCharacterLightReachesOnlyTheCharacterLayer()
        {
            yield return null;

            CharacterLight follower = Object.FindAnyObjectByType<CharacterLight>();
            Assert.IsNotNull(follower, "The arena has no character light.");

            Light light = follower.GetComponent<Light>();
            Assert.AreEqual(RenderingLayers.CharacterMask, (uint)light.GetUniversalAdditionalLightData().renderingLayers,
                "The character light reaches more than the fighters.");
            Assert.AreEqual(LightShadows.None, light.shadows, "The character light pays for shadows it does not need.");
        }

        [UnityTest]
        public IEnumerator EveryFighterAndWeaponIsOnTheCharacterLayer()
        {
            yield return null;

            foreach (string name in new[] { "Player", "Boss" })
            {
                GameObject fighter = GameObject.Find(name);
                Assert.IsNotNull(fighter, name + " is missing.");

                // The body and whatever is held. Not the ground warning and effects a fighter carries, which are
                // drawn on the floor or in the air and must not catch the fighters' light.
                foreach (Renderer renderer in fighter.GetComponentsInChildren<Renderer>(true))
                {
                    bool body = renderer is SkinnedMeshRenderer;
                    bool held = renderer.GetComponentInParent<AdaptiveBossArena.Combat.Feel.WeaponSocket>() != null;

                    if (!body && !held)
                    {
                        continue;
                    }

                    Assert.AreNotEqual(0u, renderer.renderingLayerMask & RenderingLayers.CharacterMask,
                        $"{renderer.name} on {name} is not on the character layer, so it stays dark.");
                    Assert.AreNotEqual(0u, renderer.renderingLayerMask & RenderingLayers.DefaultMask,
                        $"{renderer.name} on {name} left the default layer, so the moon no longer reaches it.");
                }
            }
        }

        [UnityTest]
        public IEnumerator NothingOfTheRoomIsOnTheCharacterLayer()
        {
            yield return null;

            foreach (Renderer renderer in GameObject.Find("Cathedral").GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreEqual(0u, renderer.renderingLayerMask & RenderingLayers.CharacterMask,
                    renderer.name + " is lit by the fighters' light.");
            }
        }

        [Test]
        public void TheLightComesFromAboveTheCamerasShoulderIntoTheScene()
        {
            Vector3 cameraForward = new Vector3(0.3f, -0.4f, 1f);
            Vector3 travel = CharacterLight.OrientationFor(cameraForward) * Vector3.forward;

            Assert.Less(travel.y, -0.3f, "The light does not come from above.");
            Assert.Greater(Vector3.Dot(new Vector3(travel.x, 0f, travel.z).normalized, new Vector3(0.3f, 0f, 1f).normalized), 0.7f,
                "The light does not travel with the camera's view, so it lights the far side of the fighters.");
        }
    }
}
