using System.Diagnostics;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Utils;

internal static class LuauNativeMethods
{
    public static unsafe int luaL_ref(lua_State* L, int t)
    {
        // Luau lua_ref behaves differently from normal lua!
        // See https://github.com/luau-lang/luau/issues/247#issuecomment-983043114
        Debug.Assert(t == LUA_REGISTRYINDEX);
        int r = lua_ref(L, -1);
        lua_pop(L, 1);
        return r;
    }

    public static unsafe void CompileLoadAndCall(
        lua_State* L,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> chunkName,
        int nResults
    )
    {
        fixed (byte* pSource = source)
        fixed (byte* pChunkName = chunkName)
        {
            nuint resultSize = 0;
            byte* pByteCode = luau_compile(pSource, (nuint)source.Length, null, &resultSize);
            try
            {
                int loadStatus = luau_load(L, pChunkName, pByteCode, resultSize, 0);
                LuaException.ThrowIfNotOk(L, loadStatus, "luau_load");

                int callStatus = lua_pcall(L, 0, nResults, 0);
                LuaException.ThrowIfNotOk(L, callStatus, "lua_pcall");
            }
            finally
            {
                luau_free(pByteCode);
            }
        }
    }
}
