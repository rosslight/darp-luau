using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

/// <summary> A reference to a luau table </summary>
/// <remarks> A view of the table  </remarks>
public readonly ref struct LuauTable : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; }

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauTable() { }

    internal LuauTable(LuauState? state, int reference) => (State, Reference) = (state, reference);

    /// <summary> Set a value </summary>
    /// <param name="key"> The key of the value to set </param>
    /// <param name="value"> The value to set </param>
    /// <exception cref="ObjectDisposedException"> Thrown if the state is disposed </exception>
    public unsafe void Set(IntoLuau key, IntoLuau value)
    {
        State.ThrowIfDisposed();
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
    public unsafe bool TryGet(IntoLuau key, out LuauValue value)
    {
        State.ThrowIfDisposed();
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

    public static implicit operator IntoLuau(LuauTable value) => (LuauValue)value;

    /// <inheritdoc />
    public override string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);
}
