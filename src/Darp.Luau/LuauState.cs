using System.Runtime.CompilerServices;
using System.Text;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public sealed unsafe class LuauState : IDisposable
{
    // ReSharper disable once ReplaceWithFieldKeyword
    internal readonly lua_State* L;
    private int _disposing; // 0 = false, 1 = true
    private readonly int _globalsReference;

    public LuauTable Globals => new(this, _globalsReference);

    public bool IsDisposed => _disposing > 0;

    public LuauState()
    {
        L = luaL_newstate();
        if (L is null)
            throw new InvalidOperationException("Could not create Lua state.");
        luaL_openlibs(L);
        // Get the reference to the globals table
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        _globalsReference = lua_ref(L, -1);
    }

    public LuauTable CreateTable()
    {
        lua_newtable(L);
        int refPtr = lua_ref(L, -1);
        return new LuauTable(this, refPtr);
    }

    public LuauFunction<LuauNil, LuauNil> CreateFunction(Delegate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var x = value.Method.GetParameters();
        return new LuauFunction<LuauNil, LuauNil>();
    }

    public LuauString CreateString(ReadOnlySpan<char> value)
    {
        Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
        int numberOfBytes = Encoding.UTF8.GetBytes(value, buffer);
        return CreateString(buffer[..numberOfBytes]);
    }

    public LuauString CreateString(ReadOnlySpan<byte> utf8Value)
    {
        ObjectDisposedException.ThrowIf(_disposing > 0, this);
        fixed (byte* pValue = utf8Value)
        {
            lua_pushlstring(L, pValue, (nuint)utf8Value.Length);
        }
        int reference = lua_ref(L, -1);
        return new LuauString(this, reference);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0)
            return;
        lua_close(L);
    }

    public void DoString(ReadOnlySpan<char> source)
    {
        ObjectDisposedException.ThrowIf(_disposing > 0, this);
    }
}

public static class LuauStateExtensions
{
    public static LuauFunction<T1, LuauNil> CreateFunction<T1>(this LuauState state, Action<T1> value)
        where T1 : allows ref struct => new();

    public static LuauFunction<T1, LuauNil> CreateFunction<T1>(this LuauState state, Action<LuauState, T1> value)
        where T1 : allows ref struct => new();

    public static LuauFunction<T1, T2, LuauNil> CreateFunction<T1, T2>(this LuauState state, Action<T1, T2> value)
        where T1 : allows ref struct
        where T2 : allows ref struct => new();

    public static LuauFunction<T1, T2, T> CreateFunction<T1, T2, T>(this LuauState state, Func<T1, T2, T> value)
        where T1 : allows ref struct
        where T2 : allows ref struct => new();

    public static LuauTable CreateTable(this LuauState state, ReadOnlySpan<double> values)
    {
        ArgumentNullException.ThrowIfNull(state);
        LuauTable table = state.CreateTable();
        table.Set("0", values[0]);
        return table;
    }
}
