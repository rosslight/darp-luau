using System.Collections;
using System.Runtime.CompilerServices;

namespace Darp.Luau.Internal;

/// <summary> Enumerable over a span of ref structs. </summary>
/// <typeparam name="T"> The type of each element </typeparam>
public ref struct RefEnumerable<T> : IEnumerable<T>
    where T : allows ref struct
{
    /// <summary> The maximum number of elements that can be stored in a <see cref="RefEnumerable{T}"/>. </summary>
    public const int MaxLength = 4;

    /// <summary> Gets the number of elements in the <see cref="RefEnumerable{T}"/>. </summary>
    public int Length { get; private set; }

    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;

    /// <summary>Gets the element at the index.</summary>
    public readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return index switch
            {
                0 => _item0,
                1 => _item1,
                2 => _item2,
                3 => _item3,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
        }
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();

    /// <summary> Add a new item to the <see cref="RefEnumerable{T}"/>. </summary>
    /// <param name="item"> The item to add </param>
    /// <exception cref="InvalidOperationException"> Thrown if there is no more space for a new item </exception>
    public void Add(T item)
    {
        switch (Length)
        {
            case 0:
                _item0 = item;
                break;
            case 1:
                _item1 = item;
                break;
            case 2:
                _item2 = item;
                break;
            case 3:
                _item3 = item;
                break;
            default:
                throw new InvalidOperationException("Cannot add new item");
        }
        Length++;
    }

    /// <summary>Enumerates the elements of a <see cref="RefEnumerable{T}"/>.</summary>
    public ref struct Enumerator : IEnumerator<T>
    {
        /// <summary>The span being enumerated.</summary>
        private readonly RefEnumerable<T> _span;

        /// <summary>The next index to yield.</summary>
        private int _index;

        /// <summary>Initialize the enumerator.</summary>
        /// <param name="span">The span to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(RefEnumerable<T> span)
        {
            _span = span;
            _index = -1;
        }

        /// <summary>Advances the enumerator to the next element of the span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int index = _index + 1;
            if (index >= _span.Length)
            {
                return false;
            }

            _index = index;
            return true;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current =>
            (uint)_index < (uint)_span.Length ? _span[_index] : throw new InvalidOperationException();

        T IEnumerator<T>.Current => Current;

        object IEnumerator.Current => throw new NotSupportedException();

        void IEnumerator.Reset() => _index = -1;

        void IDisposable.Dispose() { }
    }
}
