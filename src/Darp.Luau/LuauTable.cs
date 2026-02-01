using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

/// <summary> A reference to a luau table </summary>
/// <remarks> A view of the table  </remarks>
public struct LuauTable : ILuauReference, IEnumerable<KeyValuePair<LuauValue, LuauValue>>, IDisposable
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; private set; }

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauTable() { }

    internal LuauTable(LuauState? state, int reference) => (State, Reference) = (state, reference);

    /// <summary> Set a value </summary>
    /// <param name="key"> The key of the value to set </param>
    /// <param name="value"> The value to set </param>
    /// <exception cref="ObjectDisposedException"> Thrown if the state is disposed </exception>
    public unsafe void Set(IntoLuau key, IntoLuau value)
    {
        ThrowIfDisposed();
        if (key.Type is IntoLuau.Kind.Nil)
            throw new ArgumentNullException(nameof(key), "Cannot set a table value with nil key");
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);
        key.Push(L);
        value.Push(L);
        lua_settable(L, -3);
        lua_pop(L, 1);
    }

    /// <summary> Try to get the value for a given key </summary>
    /// <param name="key"> The key to get from the table </param>
    /// <param name="value"> The value if present </param>
    /// <returns> True, if the value could be retrieved. False, otherwise </returns>
    /// <exception cref="ObjectDisposedException"> Thrown if the state is disposed </exception>
    public readonly unsafe bool TryGet(IntoLuau key, out LuauValue value)
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);
        key.Push(L);
        _ = lua_gettable(L, -2);
        value = LuauValue.ToValue(State);
        lua_pop(L, 2);
        return true;
    }

    /// <summary> Ability for <see cref="LuauTable"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The table </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauTable value) => (LuauValue)value;

    /// <summary> Gets the count of the table if viewed as a list </summary>
    /// <remarks> If a lua table has holes, this property is unreliable! </remarks>
    public readonly unsafe int ListCount
    {
        get
        {
            ThrowIfDisposed();
            lua_State* L = State.L;
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: 0);
#endif
            lua_getref(L, Reference);
            int count = lua_objlen(L, 1);
            lua_pop(L, 1);
            return count;
        }
    }

    /// <inheritdoc />
    public override readonly string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public readonly Enumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return new Enumerator(this, State);
    }

    IEnumerator<KeyValuePair<LuauValue, LuauValue>> IEnumerable<KeyValuePair<LuauValue, LuauValue>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary> Gets the value associated with this key (or <see cref="LuauValueType.Nil"/>) </summary>
    /// <param name="key"> The key to look for </param>
    public LuauValue this[IntoLuau key]
    {
        get
        {
            TryGet(key, out LuauValue value);
            return value;
        }
    }

    /// <summary> Get the values as a list in paris of index and value </summary>
    /// <returns> An enumerable of values </returns>
    /// <remarks> Starts at index 1 and goes as long as there is no nil value </remarks>
    public readonly TableIPairsEnumerable IPairs()
    {
        ThrowIfDisposed();
        return new TableIPairsEnumerable(this, State);
    }

    /// <summary> Remove the reference from the lua state </summary>
    public unsafe void Dispose()
    {
        if (State is null || Reference is 0)
            return;
        lua_unref(State.L, Reference);
        Reference = 0;
    }

    [MemberNotNull(nameof(State))]
    private readonly void ThrowIfDisposed()
    {
        State.ThrowIfDisposed();
        if (Reference is 0)
            throw new ObjectDisposedException(nameof(LuauTable), "The reference to the LuauTable is invalid");
    }

    /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<LuauValue, LuauValue>>
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly LuauTable _table;
        private readonly LuauState _state;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private KeyValuePair<LuauValue, LuauValue> _current;
        private int _lastKeyRef;

        /// <inheritdoc />
        public KeyValuePair<LuauValue, LuauValue> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary> The enumerator of the <see cref="TableIPairsEnumerable"/> </summary>
        /// <param name="table"> The table to enumerate </param>
        /// <param name="state"> The state of the table </param>
        internal Enumerator(LuauTable table, LuauState state)
        {
            this = default;
            _table = table;
            _state = state;
        }

        /// <inheritdoc />
        public unsafe bool MoveNext()
        {
            _table.ThrowIfDisposed();
            lua_State* L = _state.L;
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: 0);
#endif
            lua_getref(L, _table.Reference);
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
            int newKeyRef = LuauHelpers.luaL_ref(L, LUA_REGISTRYINDEX); // pops keyCopy
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
        public unsafe void Reset()
        {
            if (_lastKeyRef != 0 && !_state.IsDisposed)
            {
                lua_unref(_state.L, _lastKeyRef);
            }
            _lastKeyRef = 0;
            _current = default;
        }

        void IDisposable.Dispose() => Reset();
    }
}
