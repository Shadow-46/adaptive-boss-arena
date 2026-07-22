using System;
using System.Collections.Generic;
using AdaptiveBossArena.Core.Services;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>Tests for the interface-keyed service container.</summary>
    [TestFixture]
    public sealed class ServiceRegistryTests
    {
        private interface IExampleService
        {
            int Value { get; }
        }

        private sealed class ExampleService : IExampleService
        {
            public ExampleService(int value) => Value = value;

            public int Value { get; }
        }

        [Test]
        public void Register_ThenGet_ReturnsTheRegisteredInstance()
        {
            var registry = new ServiceRegistry();
            var service = new ExampleService(42);

            registry.Register<IExampleService>(service);

            Assert.AreSame(service, registry.Get<IExampleService>());
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void Register_Twice_Throws()
        {
            var registry = new ServiceRegistry();
            registry.Register<IExampleService>(new ExampleService(1));

            // Silent replacement would let a duplicated manager in a scene mask itself, producing a
            // bug that only surfaces after a scene reload.
            Assert.Throws<InvalidOperationException>(
                () => registry.Register<IExampleService>(new ExampleService(2)));
        }

        [Test]
        public void Register_WithNull_Throws()
        {
            var registry = new ServiceRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register<IExampleService>(null));
        }

        [Test]
        public void RegisterOrReplace_OverwritesTheExistingRegistration()
        {
            var registry = new ServiceRegistry();
            registry.Register<IExampleService>(new ExampleService(1));

            registry.RegisterOrReplace<IExampleService>(new ExampleService(2));

            Assert.AreEqual(2, registry.Get<IExampleService>().Value);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void Get_WhenUnregistered_ThrowsWithAnActionableMessage()
        {
            var registry = new ServiceRegistry();

            var exception = Assert.Throws<InvalidOperationException>(() => registry.Get<IExampleService>());
            StringAssert.Contains("composition root", exception.Message);
        }

        [Test]
        public void TryGet_WhenUnregistered_ReturnsFalseWithoutThrowing()
        {
            var registry = new ServiceRegistry();

            Assert.IsFalse(registry.TryGet(out IExampleService service));
            Assert.IsNull(service);
        }

        [Test]
        public void Unregister_RemovesTheRegistration()
        {
            var registry = new ServiceRegistry();
            registry.Register<IExampleService>(new ExampleService(1));

            Assert.IsTrue(registry.Unregister<IExampleService>());
            Assert.IsFalse(registry.TryGet(out IExampleService _));
            Assert.IsFalse(registry.Unregister<IExampleService>());
        }
    }

    /// <summary>
    /// Tests for the deterministic random source.
    /// </summary>
    /// <remarks>
    /// Reproducibility is the entire reason this type exists in place of Unity's own generator. The
    /// boss's counter-strategy selection is probabilistic, and without a pinnable seed neither its
    /// behaviour nor a player's bug report could be reproduced.
    /// </remarks>
    [TestFixture]
    public sealed class XorShiftRandomProviderTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequences()
        {
            var first = new XorShiftRandomProvider(12345u);
            var second = new XorShiftRandomProvider(12345u);

            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual(first.NextFloat01(), second.NextFloat01());
            }
        }

        [Test]
        public void DifferentSeeds_DivergeImmediately()
        {
            var first = new XorShiftRandomProvider(1u);
            var second = new XorShiftRandomProvider(2u);

            bool diverged = false;
            for (int i = 0; i < 10 && !diverged; i++)
            {
                diverged = !Mathf.Approximately(first.NextFloat01(), second.NextFloat01());
            }

            Assert.IsTrue(diverged, "Neighbouring seeds must not produce similar opening sequences.");
        }

        [Test]
        public void Reseed_RestartsTheSequence()
        {
            var random = new XorShiftRandomProvider(777u);
            var expected = new List<float>();

            for (int i = 0; i < 20; i++)
            {
                expected.Add(random.NextFloat01());
            }

            random.Reseed(777u);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(expected[i], random.NextFloat01());
            }
        }

        [Test]
        public void ZeroSeed_IsRemappedToAUsableState()
        {
            // Xorshift cannot escape an all-zero state, so a zero seed must be substituted.
            var random = new XorShiftRandomProvider(0u);

            Assert.AreNotEqual(0u, random.Seed);

            bool sawNonZero = false;
            for (int i = 0; i < 20 && !sawNonZero; i++)
            {
                sawNonZero = random.NextFloat01() > 0f;
            }

            Assert.IsTrue(sawNonZero);
        }

        [Test]
        public void NextFloat01_StaysWithinTheUnitRange()
        {
            var random = new XorShiftRandomProvider(99u);

            for (int i = 0; i < 20000; i++)
            {
                float value = random.NextFloat01();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void NextInt_StaysWithinTheRequestedRange()
        {
            var random = new XorShiftRandomProvider(31u);

            for (int i = 0; i < 20000; i++)
            {
                int value = random.NextInt(-5, 5);
                Assert.GreaterOrEqual(value, -5);
                Assert.Less(value, 5);
            }
        }

        [Test]
        public void NextInt_WithDegenerateRange_ReturnsTheLowerBound()
        {
            var random = new XorShiftRandomProvider(5u);

            Assert.AreEqual(3, random.NextInt(3, 3));
            Assert.AreEqual(3, random.NextInt(3, 1));
        }

        [Test]
        public void NextBool_WithCertainProbabilities_IsDeterministic()
        {
            var random = new XorShiftRandomProvider(17u);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(random.NextBool(0f));
                Assert.IsTrue(random.NextBool(1f));
            }
        }

        [Test]
        public void NextBool_ApproximatesTheRequestedProbability()
        {
            var random = new XorShiftRandomProvider(2024u);
            const int trials = 40000;
            const float probability = 0.25f;

            int hits = 0;
            for (int i = 0; i < trials; i++)
            {
                if (random.NextBool(probability))
                {
                    hits++;
                }
            }

            float observed = (float)hits / trials;
            Assert.AreEqual(probability, observed, 0.02f);
        }

        [Test]
        public void NextDirectionOnPlane_ReturnsUnitVectors()
        {
            var random = new XorShiftRandomProvider(404u);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(1f, random.NextDirectionOnPlane().magnitude, 0.0001f);
            }
        }
    }
}
