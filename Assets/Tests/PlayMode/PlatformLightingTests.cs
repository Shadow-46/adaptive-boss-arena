using System.Collections;
using System.Linq;
using AdaptiveBossArena.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that each platform gets its own lighting: the roof's shadow on desktop, fainter shafts on the web.
    /// </summary>
    [TestFixture]
    public sealed class PlatformLightingTests
    {
        private PlatformLighting _lighting;
        private Light _sun;

        [UnitySetUp]
        public IEnumerator LoadTheArena()
        {
            SceneManager.LoadScene("Arena", LoadSceneMode.Single);

            yield return null;
            yield return null;

            _lighting = Object.FindAnyObjectByType<PlatformLighting>();
            _sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);

            Assert.IsNotNull(_lighting, "The arena has no platform lighting.");
            Assert.IsNotNull(_sun, "The arena has no sun.");
        }

        [UnityTest]
        public IEnumerator OnlyTheWebPlayerTakesTheWebLighting()
        {
            yield return null;

            Assert.IsTrue(PlatformLighting.UsesWebLighting(RuntimePlatform.WebGLPlayer));
            Assert.IsFalse(PlatformLighting.UsesWebLighting(RuntimePlatform.WindowsPlayer));
            Assert.IsFalse(PlatformLighting.UsesWebLighting(RuntimePlatform.WindowsEditor));
        }

        [UnityTest]
        public IEnumerator TheDesktopSunCastsTheRoofsShadow()
        {
            _lighting.Apply(web: false);

            yield return null;

            Assert.IsNotNull(_sun.cookie, "The desktop sun has no cookie.");
        }

        [UnityTest]
        public IEnumerator TheWebDrawsFainterShaftsAndNoCookie()
        {
            // Found under its own parent: the columns have a Shaft_0 of their own.
            Renderer shaft = GameObject.Find("LightShafts").transform.GetChild(0).GetComponent<Renderer>();
            float desktopAlpha = shaft.sharedMaterial.GetColor("_BaseColor").a;

            _lighting.Apply(web: true);

            yield return null;

            Assert.IsNull(_sun.cookie, "The web sun still carries the cookie.");
            Assert.Less(shaft.sharedMaterial.GetColor("_BaseColor").a, desktopAlpha * 0.6f,
                "The web shafts are not meaningfully fainter than the desktop ones.");
        }
    }
}
