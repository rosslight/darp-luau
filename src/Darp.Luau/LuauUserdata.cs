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

public struct LuauUserdata : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; private set; }

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauUserdata() { }

    internal LuauUserdata(LuauState? state, int reference) => (State, Reference) = (state, reference);

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
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);
        if ((lua_Type)lua_type(L, -1) is not lua_Type.LUA_TUSERDATA)
        {
            lua_pop(L, 1);
            error = "userdata reference does not currently point to a userdata value.";
            return false;
        }

        bool ok = ManagedUserdataResolver.TryResolve(
            L,
            -1,
            out value,
            out error,
            valueLabel: "userdata reference"
        );
        lua_pop(L, 1);
        return ok;
    }

    /// <inheritdoc />
    public override readonly string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);
}
