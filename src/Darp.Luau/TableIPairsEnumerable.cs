using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary>
/// Represents an <c>ipairs</c>-style enumerable over a Luau table.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "This enumerable is a lightweight view over Lua state and handle ownership; custom value equality would imply semantics the API does not guarantee."
)]
public readonly struct TableIPairsEnumerable : IEnumerable<KeyValuePair<int, LuauValue>>
{
    private readonly LuauTable _table;
    private readonly LuauState? _state;
    private readonly ulong _handle;

    internal TableIPairsEnumerable(LuauTable table, LuauState? state, ulong handle)
    {
        _table = table;
        _state = state;
        _handle = handle;
    }

    /// <summary>
    /// Gets the raw array length of the underlying Luau table.
    /// </summary>
    /// <remarks>If the table has holes, this value can be misleading and enumeration may end earlier.</remarks>
    public int Count => _table.ListCount;

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
    public Enumerator GetEnumerator()
    {
        _state.ThrowIfDisposed();
        ulong newHandle = _state.ReferenceTracker.CountRefOrThrow(_handle);
        return new Enumerator(_state, newHandle);
    }

    IEnumerator<KeyValuePair<int, LuauValue>> IEnumerable<KeyValuePair<int, LuauValue>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<int, LuauValue>>
    {
        private readonly LuauState? _state;
        private readonly ulong _handle;
        private KeyValuePair<int, LuauValue> _current;
        private int _i;

        /// <inheritdoc />
        public KeyValuePair<int, LuauValue> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>Initializes an enumerator over a tracked table reference.</summary>
        /// <param name="state">The state that owns the tracked table reference.</param>
        /// <param name="handle">The tracked table handle to enumerate.</param>
        internal Enumerator(LuauState? state, ulong handle)
        {
            _state = state;
            _handle = handle;
        }

        /// <inheritdoc />
        public unsafe bool MoveNext()
        {
            _i++;
            RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(_handle);
            lua_State* L = _state.L;
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: 0);
#endif
            using PopDisposable tablePop = trackedReference.PushToTop();
            int t = lua_gettop(L);
            _ = lua_rawgeti(L, t, _i);
            if (lua_isnil(L, -1))
            {
                lua_pop(L, 1);
                return false;
            }

            var value = LuauValue.ToValue(_state);

            lua_pop(L, 1);
            _current = new KeyValuePair<int, LuauValue>(_i, value);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _i = 0;
            _current = default;
        }

        void IDisposable.Dispose()
        {
            Reset();
            _state?.ReferenceTracker.ReleaseRef(_handle);
        }
    }
}
