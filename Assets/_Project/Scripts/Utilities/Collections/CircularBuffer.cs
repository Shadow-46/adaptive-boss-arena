using System;
using System.Collections;
using System.Collections.Generic;

namespace AdaptiveBossArena.Utilities.Collections
{
    /// <summary>
    /// Fixed-capacity ring buffer that silently overwrites the oldest element once full.
    /// </summary>
    /// <remarks>
    /// This is the storage primitive behind the boss's combat memory. It never allocates after
    /// construction and never grows, which bounds both memory and per-sample cost regardless of
    /// how long a fight runs. Index <c>0</c> is always the oldest retained element and index
    /// <see cref="Count"/> minus one is always the newest.
    /// </remarks>
    /// <typeparam name="T">Element type. Value types are stored inline and cause no allocation.</typeparam>
    public sealed class CircularBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;

        /// <summary>Index the next written element will occupy.</summary>
        private int _head;

        private int _count;

        /// <summary>Creates a buffer that retains at most <paramref name="capacity"/> elements.</summary>
        /// <param name="capacity">Maximum retained elements. Must be greater than zero.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is not positive.</exception>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity), capacity, "Capacity must be greater than zero.");
            }

            _items = new T[capacity];
        }

        /// <summary>Maximum number of elements retained before the oldest is discarded.</summary>
        public int Capacity => _items.Length;

        /// <summary>Number of elements currently retained.</summary>
        public int Count => _count;

        /// <summary>True once the buffer has reached <see cref="Capacity"/>.</summary>
        public bool IsFull => _count == _items.Length;

        /// <summary>True while the buffer holds no elements.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>Gets the element at <paramref name="index"/>, where zero is the oldest retained element.</summary>
        /// <param name="index">Zero-based index into the retained window.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index falls outside the retained window.</exception>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index), index, $"Index must be in the range [0, {_count}).");
                }

                return _items[PhysicalIndex(index)];
            }
        }

        /// <summary>The oldest retained element.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is empty.</exception>
        public T Oldest => _count > 0
            ? _items[PhysicalIndex(0)]
            : throw new InvalidOperationException("Buffer is empty.");

        /// <summary>The most recently added element.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is empty.</exception>
        public T Newest => _count > 0
            ? _items[PhysicalIndex(_count - 1)]
            : throw new InvalidOperationException("Buffer is empty.");

        /// <summary>
        /// Appends an element, discarding the oldest one if the buffer is already full.
        /// </summary>
        /// <param name="item">Element to append. Passed by reference to avoid copying large structs.</param>
        public void Add(in T item)
        {
            _items[_head] = item;
            _head = _head + 1 == _items.Length ? 0 : _head + 1;

            if (_count < _items.Length)
            {
                _count++;
            }
        }

        /// <summary>Attempts to read the most recently added element without throwing when empty.</summary>
        /// <param name="item">Receives the newest element, or <c>default</c> when the buffer is empty.</param>
        /// <returns>True when an element was available.</returns>
        public bool TryGetNewest(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _items[PhysicalIndex(_count - 1)];
            return true;
        }

        /// <summary>
        /// Drops all retained elements. Backing storage is reused, so this does not allocate.
        /// </summary>
        public void Clear()
        {
            // Clearing references matters for reference types so the buffer does not pin dead objects.
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>Returns a non-allocating enumerator over the retained window, oldest first.</summary>
        /// <returns>A value-type enumerator suitable for <c>foreach</c> without boxing.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Maps a logical index (zero = oldest) onto the backing array index.</summary>
        private int PhysicalIndex(int logicalIndex)
        {
            // _head - _count is never less than -Capacity, so a single wrap correction is sufficient.
            int start = _head - _count + _items.Length;
            int offset = start + logicalIndex;
            return offset % _items.Length;
        }

        /// <summary>Value-type enumerator that walks the retained window from oldest to newest.</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly CircularBuffer<T> _buffer;
            private int _index;

            internal Enumerator(CircularBuffer<T> buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            /// <summary>The element at the current cursor position.</summary>
            public T Current => _buffer[_index];

            object IEnumerator.Current => Current;

            /// <summary>Advances the cursor toward the newest element.</summary>
            /// <returns>True while an element remains.</returns>
            public bool MoveNext() => ++_index < _buffer.Count;

            /// <summary>Rewinds the cursor to before the oldest element.</summary>
            public void Reset() => _index = -1;

            /// <summary>No unmanaged resources are held; present to satisfy the interface.</summary>
            public void Dispose()
            {
            }
        }
    }
}
