using System.Collections;
using AdaptiveBossArena.Combat.Feel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AdaptiveBossArena.Tests.PlayMode
{
    /// <summary>
    /// Tests that the corrupted knight's fire breaks out phase by phase and dies back on a retry.
    /// </summary>
    /// <remarks>
    /// Built on a stand-in body with stand-in maps, so it runs in a checkout without the licensed model.
    /// </remarks>
    [TestFixture]
    public sealed class CorruptionDisplayTests
    {
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private GameObject _body;
        private CorruptionDisplay _display;
        private Texture2D[] _levels;

        [SetUp]
        public void BuildAStandIn()
        {
            _levels = new Texture2D[4];

            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = new Texture2D(2, 2) { name = "Level" + i };
            }

            _body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _body.SetActive(false);
            _display = _body.AddComponent<CorruptionDisplay>();
            _display.Bind(_body.GetComponent<Renderer>(), new Renderer[0], _levels, new Color(2f, 0.5f, 0.1f), null);
            _body.SetActive(true);
        }

        [TearDown]
        public void Clear()
        {
            Object.Destroy(_body);

            foreach (Texture2D level in _levels)
            {
                Object.Destroy(level);
            }
        }

        [UnityTest]
        public IEnumerator EachPhaseShowsItsOwnCracksAndBurnsHotter()
        {
            yield return null;
            float opening = Glow();
            Assert.AreSame(_levels[0], Map(), "The opening shows cracks it has not earned yet.");

            _display.SetPhase(2);
            yield return new WaitForSecondsRealtime(1.5f);

            Assert.AreSame(_levels[2], Map(), "The second escalation did not open its cracks.");
            Assert.Greater(Glow(), opening * 1.5f, "The fire did not burn hotter as the fight escalated.");
        }

        [UnityTest]
        public IEnumerator ARetryStartsCold()
        {
            _display.SetPhase(3);
            yield return new WaitForSecondsRealtime(0.5f);

            _display.SetPhase(0);
            yield return null;

            Assert.AreSame(_levels[0], Map(), "A retry kept the Last Stand's cracks.");
            Assert.Less(Glow(), 1.5f * 2f, "A retry kept the Last Stand's fire.");
        }

        private Texture Map()
        {
            var block = new MaterialPropertyBlock();
            _body.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetTexture(EmissionMapId);
        }

        private float Glow()
        {
            var block = new MaterialPropertyBlock();
            _body.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor(EmissionColorId).maxColorComponent;
        }
    }
}
