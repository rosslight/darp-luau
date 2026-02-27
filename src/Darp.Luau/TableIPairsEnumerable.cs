using System.Collections;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> An IPairs enumerable over a table </summary>
public readonly struct TableIPairsEnumerable : IEnumerable<KeyValuePair<int, LuauValue>>
{
    private readonly LuauTable _table;
    private readonly LuauState _state;

    internal TableIPairsEnumerable(LuauTable table, LuauState state)
    {
        _table = table;
        _state = state;
    }

    /// <summary> The raw length of the underlying lua table </summary>
    /// <remarks> If a lua table has holes, this property is unreliable! Enumeration might end earlier! </remarks>
    public int Count => _table.ListCount;

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
    public Enumerator GetEnumerator() => new(_table, _state);

    IEnumerator<KeyValuePair<int, LuauValue>> IEnumerable<KeyValuePair<int, LuauValue>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<int, LuauValue>>
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly LuauTable _table;
        private readonly LuauState _state;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private KeyValuePair<int, LuauValue> _current;
        private int _i;

        /// <inheritdoc />
        public KeyValuePair<int, LuauValue> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
        /// <param name="table"> The table to enumerate </param>
        /// <param name="state"> The state of the table </param>
        internal Enumerator(LuauTable table, LuauState state)
        {
            _table = table;
            _state = state;
        }

        /// <inheritdoc />
        public unsafe bool MoveNext()
        {
            _i++;
            _state.ThrowIfDisposed();
            lua_State* L = _state.L;
            int tableReference = _state.ReferenceTracker.ResolveLuaRef(_table.Reference, nameof(LuauTable));
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: 0);
#endif
            lua_getref(L, tableReference);
            int t = lua_gettop(L);
            _ = lua_rawgeti(L, t, _i);
            if (lua_isnil(L, -1))
            {
                lua_pop(L, 2);
                return false;
            }

            var value = LuauValue.ToValue(_state);

            lua_pop(L, 2);
            _current = new KeyValuePair<int, LuauValue>(_i, value);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _i = 0;
            _current = default;
        }

        void IDisposable.Dispose() => Reset();
    }
}
