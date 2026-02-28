using System.Diagnostics;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal readonly unsafe struct LuauRefSource
{
    private enum SourceKind
    {
        RegistryHandle,
        CallbackStack,
    }

    private readonly SourceKind _kind;
    private readonly int _stackIndex;
    private readonly int _callbackFrameToken;
    private readonly lua_Type _expectedType;

    public LuauState? State { get; }
    public int Reference { get; }

    private LuauRefSource(
        SourceKind kind,
        LuauState? state,
        int reference,
        int stackIndex,
        int callbackFrameToken,
        lua_Type expectedType
    )
    {
        if (kind is not SourceKind.RegistryHandle and not SourceKind.CallbackStack)
            throw new ArgumentOutOfRangeException(nameof(kind));
        _kind = kind;
        State = state;
        Reference = reference;
        _stackIndex = stackIndex;
        _callbackFrameToken = callbackFrameToken;
        _expectedType = expectedType;
    }

    internal static LuauRefSource FromReference(LuauState? state, int reference, lua_Type expectedType) =>
        new(SourceKind.RegistryHandle, state, reference, stackIndex: 0, callbackFrameToken: 0, expectedType);

    internal static LuauRefSource FromCallbackStack(
        LuauState? state,
        int stackIndex,
        int callbackFrameToken,
        lua_Type expectedType
    ) => new(SourceKind.CallbackStack, state, reference: 0, stackIndex, callbackFrameToken, expectedType);

    internal LuauState Validate(ReadOnlySpan<char> ownerTypeName)
    {
        if (State is null)
            throw new ObjectDisposedException(
                ownerTypeName.ToString(),
                $"The reference to the {ownerTypeName} is invalid"
            );

        State.ThrowIfDisposed();

        switch (_kind)
        {
            case SourceKind.RegistryHandle:
                if (Reference is 0 || !State.ReferenceTracker.HasRegistryReference(Reference))
                    throw new ObjectDisposedException(
                        ownerTypeName.ToString(),
                        $"The reference to the {ownerTypeName} is invalid"
                    );
                break;
            case SourceKind.CallbackStack:
                if (!State.IsCallbackFrameActive(_callbackFrameToken))
                    throw new ObjectDisposedException(
                        ownerTypeName.ToString(),
                        "The callback frame has already ended."
                    );
                if ((lua_Type)lua_type(State.L, _stackIndex) != _expectedType)
                    throw new ObjectDisposedException(
                        ownerTypeName.ToString(),
                        $"The reference to the {ownerTypeName} is invalid"
                    );
                break;
            default:
                throw new UnreachableException($"Unknown function source kind {_kind}");
        }

        return State;
    }

    internal void Push(lua_State* L, ReadOnlySpan<char> ownerTypeName)
    {
        LuauState state = Validate(ownerTypeName);
        if ((nint)state.L != (nint)L)
            throw new InvalidOperationException("Cross-state reference usage is not allowed.");

        if (_kind == SourceKind.RegistryHandle)
        {
            int luaReference = state.ReferenceTracker.ResolveLuaRef(Reference, ownerTypeName);
            lua_getref(L, luaReference);
            return;
        }

        lua_pushvalue(L, _stackIndex);
    }

    public override string ToString()
    {
        switch (_kind)
        {
            case SourceKind.RegistryHandle:
                if (State?.ReferenceTracker.TryResolveLuaRef(Reference, out int reference) is not true)
                    return "<nil>";
                return Helpers.RefToString(State, reference);
            case SourceKind.CallbackStack:
                if (State is null)
                    return "<nil>";
                if (!State.IsCallbackFrameActive(_callbackFrameToken))
                    return "<nil>";
                if ((lua_Type)lua_type(State.L, _stackIndex) != _expectedType)
                    return "<nil>";
                return Helpers.StackString(State, _stackIndex);
            default:
                throw new UnreachableException($"Unknown function source kind {_kind}");
        }
    }

    public void Dispose()
    {
        switch (_kind)
        {
            case SourceKind.RegistryHandle:
                State?.ReferenceTracker.ReleaseRef(Reference);
                break;
            case SourceKind.CallbackStack:
            default:
                break;
        }
    }
}
