using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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

    /// <inheritdoc />
    public override readonly string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);
}
