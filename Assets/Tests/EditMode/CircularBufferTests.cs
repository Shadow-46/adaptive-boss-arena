using System;
using AdaptiveBossArena.Utilities.Collections;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the ring buffer backing the boss's combat memory.
    /// </summary>
    /// <remarks>
    /// Wraparound indexing is the failure most worth guarding: if index zero stops meaning "oldest"
    /// once the buffer fills, every derived behaviour feature silently reads the wrong window of
    /// history, and the boss adapts to events in the wrong order without anything appearing broken.
    /// </remarks>
    [TestFixture]
    public sealed class CircularBufferTests
    {
        [Test]
        public void Constructor_WithNonPositiveCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(-1));
        }

        [Test]
        public void Add_BelowCapacity_GrowsCountAndPreservesOrder()
        {
            var buffer = new CircularBuffer<int>(4);

            buffer.Add(10);
            buffer.Add(20);
            buffer.Add(30);

            Assert.AreEqual(3, buffer.Count);
            Assert.IsFalse(buffer.IsFull);
            Assert.AreEqual(10, buffer[0], "Index zero must be the oldest element.");
            Assert.AreEqual(30, buffer[2]);
            Assert.AreEqual(10, buffer.Oldest);
            Assert.AreEqual(30, buffer.Newest);
        }

        [Test]
        public void Add_BeyondCapacity_DiscardsOldestAndKeepsWindowOrdered()
        {
            var buffer = new CircularBuffer<int>(3);

            for (int i = 1; i <= 5; i++)
            {
                buffer.Add(i);
            }

            Assert.AreEqual(3, buffer.Count);
            Assert.IsTrue(buffer.IsFull);
            Assert.AreEqual(3, buffer[0], "After wrapping, index zero must still be the oldest retained element.");
            Assert.AreEqual(4, buffer[1]);
            Assert.AreEqual(5, buffer[2]);
        }

        [Test]
        public void Add_ManyTimesAroundTheRing_KeepsIndexingCorrect()
        {
            const int capacity = 8;
            const int writes = 1000;

            var buffer = new CircularBuffer<int>(capacity);

            for (int i = 0; i < writes; i++)
            {
                buffer.Add(i);
            }

            // After many laps the newest value is writes-1 and the window is the preceding capacity
            // values, in order. This is the invariant the perception history depends on.
            for (int i = 0; i < capacity; i++)
            {
                Assert.AreEqual(writes - capacity + i, buffer[i]);
            }
        }

        [Test]
        public void Indexer_OutsideRetainedWindow_Throws()
        {
            var buffer = new CircularBuffer<int>(3);
            buffer.Add(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }

        [Test]
        public void OldestAndNewest_WhenEmpty_Throw()
        {
            var buffer = new CircularBuffer<int>(3);

            Assert.Throws<InvalidOperationException>(() => _ = buffer.Oldest);
            Assert.Throws<InvalidOperationException>(() => _ = buffer.Newest);
        }

        [Test]
        public void TryGetNewest_WhenEmpty_ReturnsFalse()
        {
            var buffer = new CircularBuffer<int>(3);

            Assert.IsFalse(buffer.TryGetNewest(out int value));
            Assert.AreEqual(0, value);
        }

        [Test]
        public void Clear_ResetsToEmptyAndAllowsReuse()
        {
            var buffer = new CircularBuffer<int>(3);
            buffer.Add(1);
            buffer.Add(2);

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.IsTrue(buffer.IsEmpty);

            buffer.Add(9);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(9, buffer[0]);
        }

        [Test]
        public void Enumeration_AfterWrapping_YieldsOldestToNewest()
        {
            var buffer = new CircularBuffer<int>(3);

            for (int i = 1; i <= 5; i++)
            {
                buffer.Add(i);
            }

            var collected = new System.Collections.Generic.List<int>();
            foreach (int value in buffer)
            {
                collected.Add(value);
            }

            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, collected);
        }
    }
}
