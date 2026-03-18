using System.Runtime.CompilerServices;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauFunctionInvokeCore
{
    private const int LuaMultRet = -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invoke<T>(scoped in T? source, scoped in RefEnumerable<IntoLuau> args)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        int topBeforeInvoke = lua_gettop(L);
        try
        {
            source.PushToTop();
            int length = args.Length;
            for (int i = 0; i < length; i++)
                args[i].Push(state);

            int status = lua_pcall(L, length, 0, 0);
            LuaException.ThrowIfNotOk(L, status, "lua_pcall");
        }
        finally
        {
            lua_settop(L, topBeforeInvoke);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TR Invoke<T, TR>(scoped in T? source, scoped in RefEnumerable<IntoLuau> args, Func<LuauArgs, TR> func)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        int topBeforeInvoke = lua_gettop(L);
        try
        {
            source.PushToTop();
            int nArgs = args.Length;
            for (int i = 0; i < nArgs; i++)
                args[i].Push(state);

            int status = lua_pcall(L, nArgs, LuaMultRet, 0);
            LuaException.ThrowIfNotOk(L, status, "lua_pcall");
            var result = new LuauArgs(state, lua_gettop(L) - topBeforeInvoke, topBeforeInvoke + 1);
            return func(result);
        }
        finally
        {
            lua_settop(L, topBeforeInvoke);
        }
    }

    public static T Read<T>(this in LuauArgs args, int index)
    {
        if (!args.TryReadLuauValue(index, out LuauValue value, out string? error))
            throw new ArgumentOutOfRangeException(nameof(index), error);
        try
        {
            return value.TryGet(out T? result, acceptNil: default(T) is null)
                ? result
                : throw new InvalidCastException(error);
        }
        finally
        {
            value.Dispose();
        }
    }
}
