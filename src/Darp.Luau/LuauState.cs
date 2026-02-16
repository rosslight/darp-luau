using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.LuauHelpers;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

#if DEBUG
internal unsafe ref struct StackGuard(lua_State* l, int expectedDelta = 0, [CallerMemberName] string? callerName = null)
    : IDisposable
{
    private static Exception? s_exceptionInFlight;

    private readonly lua_State* _L = l;
    private readonly int _startTop = lua_gettop(l);
    private int _expectedDelta = expectedDelta;
    private readonly string? _callerName = callerName;

    public void OverwriteExpectedDelta(int newExpectedDelta) => _expectedDelta = newExpectedDelta;

    public void Dispose()
    {
        int now = lua_gettop(_L);
        int expected = _startTop + _expectedDelta;

        if (now == expected)
            return;

        string direction = now > _startTop ? "grown" : "decreased";
        s_exceptionInFlight = _expectedDelta switch
        {
            0 => new InvalidOperationException(
                $"Lua stack mismatch at {_callerName}: Stack has {direction} from {_startTop} to {now} but should be the same\n{StackDump()}",
                s_exceptionInFlight
            ),
            _ => new InvalidOperationException(
                $"Lua stack mismatch at {_callerName}: Stack has {direction} by {now - _startTop} from {_startTop} to {now} but should have by {_expectedDelta} to {_startTop + _expectedDelta}\n{StackDump()}",
                s_exceptionInFlight
            ),
        };
        throw s_exceptionInFlight;
    }

    private string StackDump()
    {
        using var writer = new StringWriter();
        writer.WriteLine("Lua stack:");
        int top = lua_gettop(_L);
        for (int i = 1; i <= top; i++)
        {
            var t = (lua_Type)lua_type(_L, i);
            switch (t)
            {
                case lua_Type.LUA_TBOOLEAN:
                    writer.WriteLine($"  [{i}]: {lua_toboolean(_L, i)}");
                    break;
                case lua_Type.LUA_TSTRING:
                    nuint lenStr = 0;
                    byte* pStr = lua_tolstring(_L, i, &lenStr);
                    writer.WriteLine($"  [{i}]: {Encoding.UTF8.GetString(pStr, (int)lenStr)}");
                    break;
                case lua_Type.LUA_TNUMBER:
                    writer.WriteLine($"  [{i}]: {lua_tonumber(_L, i)}");
                    break;
                default:
                    byte* pType = lua_typename(_L, (int)t);
                    int lenType = 0;
                    for (; ; )
                    {
                        if (pType is null || pType[lenType] == 0)
                            break;
                        lenType++;
                    }
                    writer.WriteLine($"  [{i}]: {Encoding.UTF8.GetString(pType, lenType)}");
                    break;
            }
        }
        return writer.ToString();
    }
}
#endif

internal static class LuauHelpers
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
}

/// <summary> The LuauState </summary>
/// <remarks> Not threadsafe </remarks>
public sealed unsafe class LuauState : IDisposable
{
    // ReSharper disable once ReplaceWithFieldKeyword
    internal readonly lua_State* L;
    private int _disposing; // 0 = false, 1 = true
    private readonly int _globalsReference;

    [Obsolete("Used for saving delegates used in unmanaged memory to prevent them going out of scope")]
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly List<Delegate> _delegateSave = [];

    /// <summary> The global table. Used as a entry point </summary>
    public LuauTable Globals => new(this, _globalsReference);

    /// <summary> If true, the LuauState is disposed and any method will throw </summary>
    public bool IsDisposed => _disposing > 0;

    /// <summary> Initializes a new LuauState, and opens default libs. </summary>
    /// <exception cref="InvalidOperationException"> Thrown if the luau state could not be created </exception>
    public LuauState()
    {
        L = luaL_newstate();
        if (L is null)
            throw new InvalidOperationException("Could not create Lua state.");
        luaL_openlibs(L);
        // Get the reference to the globals table
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        _globalsReference = luaL_ref(L, LUA_REGISTRYINDEX);
    }

    /// <summary> Create a new table </summary>
    /// <returns> The resulting table </returns>
    public LuauTable CreateTable()
    {
        this.ThrowIfDisposed();
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_newtable(L);
        int refPtr = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauTable(this, refPtr);
    }

    /// <summary> The delegate type that provides a save view to work with a function </summary>
    public delegate void LuauFunctionBuilder(ref LuauFunctions builder);

    /// <summary> Create a new LuaFunction and get the reference to it </summary>
    /// <param name="onCalled">The callback providing a </param>
    /// <returns> The LuaFunction with the reference to the lua memory </returns>
    public LuauFunction CreateFunctionBuilder(LuauFunctionBuilder onCalled)
    {
        this.ThrowIfDisposed();
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var f = F;
#pragma warning disable CS0618 // This is the only place we want to save the delegates
        _delegateSave.Add(f);
        IntPtr intPtr = Marshal.GetFunctionPointerForDelegate(f);
#pragma warning restore CS0618 // Type or member is obsolete

        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State*, int>)intPtr, null);
        int refs = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauFunction(this, refs);

        int F(lua_State* luaState)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual((nint)luaState, (nint)L);
            int numberOfParameters = lua_gettop(luaState);
#if DEBUG
            using var guard = new StackGuard(L, expectedDelta: -numberOfParameters);
#endif
            var builder = new LuauFunctions(this, numberOfParameters);
            try
            {
                onCalled(ref builder);
#if DEBUG
                guard.OverwriteExpectedDelta(builder.NumberOfOutputParameters);
#endif
                return builder.NumberOfOutputParameters;
            }
            catch (Exception e)
            {
                // TODO: Handle the error (do i have to call luaL_error?)
                throw;
                return 0;
            }
        }
    }

    /// <summary> Creates a new luau function and returns the reference to it </summary>
    /// <param name="value"> The delegate to register </param>
    /// <typeparam name="T"> The type of the delegate. This will be used by the interceptor to retrieve the parameters </typeparam>
    /// <returns> The <see cref="LuauFunction"/> with the reference </returns>
    /// <remarks> THIS METHOD IS SUPPOSED TO BE INTERCEPTED AND WILL NOT WORK OTHERWISE </remarks>
    public LuauFunction CreateFunction<T>(T value)
        where T : Delegate => throw new InvalidOperationException("This method should be intercepted!");

    /// <summary> Creates a new luau string </summary>
    /// <param name="value"> The string </param>
    /// <returns> The reference to the LuauString </returns>
    public LuauString CreateString(scoped ReadOnlySpan<char> value)
    {
        Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
        int numberOfBytes = Encoding.UTF8.GetBytes(value, buffer);
        return CreateString(buffer[..numberOfBytes]);
    }

    /// <summary> Creates a new luau string </summary>
    /// <param name="utf8Value"> The utf8 string </param>
    /// <returns> The reference to the LuauString </returns>
    public LuauString CreateString(scoped ReadOnlySpan<byte> utf8Value)
    {
        this.ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(_disposing > 0, this);
        if (utf8Value.IsEmpty)
            throw new ArgumentNullException(nameof(utf8Value));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        fixed (byte* pValue = utf8Value)
        {
            lua_pushlstring(L, pValue, (nuint)utf8Value.Length);
        }

        int reference = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauString(this, reference);
    }

    /// <summary> Creates a new luau buffer </summary>
    /// <param name="span"> The bytes span </param>
    /// <returns> The reference to the LuauBuffer </returns>
    public LuauBuffer CreateBuffer(scoped ReadOnlySpan<byte> span)
    {
        this.ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(_disposing > 0, this);
        if (span.IsEmpty)
            throw new ArgumentNullException(nameof(span));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        void* pDest = lua_newbuffer(L, (nuint)span.Length);

        fixed (byte* pSrc = span)
        {
            Unsafe.CopyBlock(pDest, pSrc, (uint)span.Length);
        }

        int reference = lua_ref(L, -1);
        lua_pop(L, 1);
        return new LuauBuffer(this, reference);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0)
            return;
        lua_close(L);
#pragma warning disable CS0618 // Type or member is obsolete -> This is the place we want to clear the delegates
        _delegateSave.Clear();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary> Do the string </summary>
    /// <param name="source"> The source to compile and run </param>
    /// <param name="chunkName"> The name of the chunk to load </param>
    public void DoString(ReadOnlySpan<char> source, ReadOnlySpan<char> chunkName = default)
    {
        byte[] sourceBuffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(source));
        int numberOfSourceBytes = Encoding.UTF8.GetBytes(source, sourceBuffer);
        try
        {
            Span<byte> chunkNameBuffer = stackalloc byte[Encoding.UTF8.GetByteCount(chunkName)];
            int numberOfChunkNameBytes = Encoding.UTF8.GetBytes(chunkName, chunkNameBuffer);
            DoString(sourceBuffer.AsSpan(0, numberOfSourceBytes), chunkNameBuffer[..numberOfChunkNameBytes]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBuffer);
        }
    }

    /// <summary> Do the string </summary>
    /// <param name="source"> The source to compile and run </param>
    /// <param name="chunkName"> The name of the chunk to load </param>
    public void DoString(ReadOnlySpan<byte> source, ReadOnlySpan<byte> chunkName = default)
    {
        this.ThrowIfDisposed();
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        if (chunkName.IsEmpty)
            chunkName = "main"u8;
        fixed (byte* pSource = source)
        fixed (byte* pChunkName = chunkName)
        {
            nuint resultSize = 0;
            byte* pByteCode = luau_compile(pSource, (nuint)source.Length, null, &resultSize);
            int loadStatus = luau_load(L, pChunkName, pByteCode, resultSize, 0);
            LuaException.ThrowIfNotOk(L, loadStatus);
            int callStatus = lua_pcall(L, 0, 0, 0);
            LuaException.ThrowIfNotOk(L, callStatus);
        }
    }
}

/// <summary> A lua exception </summary>
public sealed class LuaException : Exception
{
    private LuaException(string message)
        : base(message) { }

    /// <summary> Throws if not ok </summary>
    /// <param name="state"> The state that holds the error </param>
    /// <param name="status"> The status </param>
    /// <exception cref="LuaException"> The exception if the status is not ok </exception>
    public static unsafe void ThrowIfNotOk(lua_State* state, int status)
    {
        if (status == 0)
            return;

        nuint outLength = 0;
        byte* err = lua_tolstring(state, -1, &outLength);
        lua_pop(state, 1);
        string error = Encoding.UTF8.GetString(err, (int)outLength);
        string message = $"Lua invocation failed with status {status}: {error}";
        throw new LuaException(message);
    }
}
