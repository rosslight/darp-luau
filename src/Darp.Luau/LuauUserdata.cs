using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

internal struct LuauUserdataNative
{
    public const int Tag = 1;

    public GCHandle UserdataHandle { get; internal set; }
    public GCHandle RegistryValueHandle { get; internal set; }
}

public readonly ref struct LuauUserdata : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; }

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauUserdata() { }

    internal LuauUserdata(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public void Dispose() => State?.ReferenceTracker.ReleaseRef(Reference);

    [MemberNotNull(nameof(State))]
    private void ThrowIfDisposed()
    {
        State.ThrowIfDisposed();
        if (Reference is 0 || !State.ReferenceTracker.HasRegistryReference(Reference))
            throw new ObjectDisposedException(nameof(LuauUserdata), "The reference to the LuauUserdata is invalid");
    }

    /// <summary> Ability for <see cref="LuauUserdata"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The userdata </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauUserdata value) => (LuauValue)value;

    /// <summary>
    /// Attempts to resolve this userdata reference back to the managed userdata instance.
    /// </summary>
    /// <typeparam name="T">Managed userdata type.</typeparam>
    /// <param name="value">Receives the managed instance when successful.</param>
    /// <param name="error">Receives a descriptive error when resolution fails.</param>
    /// <returns>
    /// <c>true</c> when this reference points to managed userdata of type <typeparamref name="T"/>;
    /// otherwise <c>false</c>.
    /// </returns>
    public unsafe bool TryGetManaged<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out string? error)
        where T : class, ILuauUserData<T>
    {
        value = null;
        ThrowIfDisposed();
        lua_State* L = State.L;
        int reference = State.ReferenceTracker.ResolveLuaRef(Reference, nameof(LuauUserdata));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, reference);
        if ((lua_Type)lua_type(L, -1) is not lua_Type.LUA_TUSERDATA)
        {
            lua_pop(L, 1);
            error = "userdata reference does not currently point to a userdata value.";
            return false;
        }

        bool ok = ManagedUserdataResolver.TryResolve(L, -1, out value, out error, valueLabel: "userdata reference");
        lua_pop(L, 1);
        return ok;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (State?.ReferenceTracker.TryResolveLuaRef(Reference, out int reference) is not true)
            return "<nil>";
        return Helpers.RefToString(State, reference);
    }
}
