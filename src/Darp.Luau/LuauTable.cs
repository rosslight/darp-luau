using System.Collections;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A reference to a luau table </summary>
/// <remarks> A view of the table  </remarks>
public readonly unsafe partial struct LuauTable : IDisposable, IEnumerable<KeyValuePair<LuauValue, LuauValue>>
{
    private readonly LuauState? _state;
    private readonly ulong _handle;

    /// <summary> Do (not) initialize a new LuauTable </summary>
    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauTable() { }

    internal LuauTable(LuauState? state, ulong handle)
    {
        _state = state;
        _handle = handle;
    }

    /// <summary> Gets the count of the table if viewed as a list </summary>
    /// <remarks> If a lua table has holes, this property is unreliable! </remarks>
    public int ListCount
    {
        get
        {
            RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(_handle);
            return LuauTableAccessCore.ListCount(trackedReference);
        }
    }

    /// <summary> Set a value </summary>
    /// <param name="key"> The key of the value to set </param>
    /// <param name="value"> The value to set </param>
    /// <exception cref="ObjectDisposedException"> Thrown if the state is disposed </exception>
    /// <exception cref="ArgumentNullException"> Thrown if a <c>Nil</c> <paramref name="key"/> is provided </exception>
    public void Set(IntoLuau key, IntoLuau value)
    {
        RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(_handle);
        LuauTableAccessCore.Set(trackedReference, key, value);
    }

    /// <summary> Determines whether <paramref name="key"/> resolves to a non-<c>nil</c> value. </summary>
    /// <param name="key">The key to resolve.</param>
    /// <remarks>Uses regular table lookup and therefore honors table metamethods such as <c>__index</c>.</remarks>
    public bool ContainsKey(IntoLuau key)
    {
        RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(_handle);
        return LuauTableAccessCore.ContainsKey(trackedReference, key);
    }

    /// <summary> Ability for <see cref="LuauTable"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The table </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauTable value) => IntoLuau.Borrow(value._state, value._handle);

    /// <summary> Converts this string reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks> Calling this method releases the reference of the current <see cref="LuauTable"/> </remarks>
    /// <returns> The reference as a luauValue </returns>
    public LuauValue DisposeAndToLuauValue() => LuauValue.FromSource(_state, _handle, LuauValueType.Table);

    /// <inheritdoc />
    public override string ToString() => Helpers.HandleToString(_state, _handle);

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public Enumerator GetEnumerator()
    {
        _state.ThrowIfDisposed();
        ulong newHandle = _state.ReferenceTracker.CountRefOrThrow(_handle);
        return new Enumerator(_state, newHandle);
    }

    IEnumerator<KeyValuePair<LuauValue, LuauValue>> IEnumerable<KeyValuePair<LuauValue, LuauValue>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary> Gets the value associated with this key (or <see cref="LuauValueType.Nil"/>) </summary>
    /// <param name="key"> The key to look for </param>
    public LuauValue this[IntoLuau key] => TryGetLuauValue(key, out LuauValue value) ? value : default;

    /// <summary> Get the values as a list in paris of index and value </summary>
    /// <returns> An enumerable of values </returns>
    /// <remarks> Starts at index 1 and goes as long as there is no nil value </remarks>
    public TableIPairsEnumerable IPairs() => new(this, _state, _handle);

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => _state?.ReferenceTracker.ReleaseRef(_handle);

    /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<LuauValue, LuauValue>>
    {
        private readonly LuauState? _state;
        private readonly ulong _handle;
        private KeyValuePair<LuauValue, LuauValue> _current;
        private int _lastKeyRef;

        /// <inheritdoc />
        public KeyValuePair<LuauValue, LuauValue> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
        /// <param name="source"> The source of the table </param>
        internal Enumerator(LuauState? state, ulong handle)
        {
            _state = state;
            _handle = handle;
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(_handle);
            lua_State* L = _state.L;
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: 0);
#endif
            _ = trackedReference.PushToTop();
            int t = lua_gettop(L); // table index

            if (_lastKeyRef == 0)
                lua_pushnil(L); // initial key
            else
                lua_getref(L, _lastKeyRef); // last key

            // lua_next pops the key and pushes (key, value) when it returns true.
            int hasNext = lua_next(L, t);
            if (hasNext == 0)
            {
                // key already popped by lua_next; only table remains
                lua_pop(L, 1);
                return false;
            }

            // stack: [table, key, value]
            lua_pushvalue(L, -2); // [table, key, value, keyCopy]
            int newKeyRef = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX); // pops keyCopy
            if (_lastKeyRef != 0)
            {
                lua_unref(L, _lastKeyRef);
            }
            _lastKeyRef = newKeyRef;

            var value = LuauValue.ToValue(_state);
            lua_pop(L, 1); // pop value
            var key = LuauValue.ToValue(_state);
            lua_pop(L, 2); // pop key + table

            _current = new KeyValuePair<LuauValue, LuauValue>(key, value);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (_lastKeyRef != 0 && _state is not null && !_state.IsDisposed)
            {
                lua_unref(_state.L, _lastKeyRef);
            }
            _lastKeyRef = 0;
            _current = default;
        }

        void IDisposable.Dispose()
        {
            Reset();
            _state?.ReferenceTracker.ReleaseRef(_handle);
        }
    }
}
