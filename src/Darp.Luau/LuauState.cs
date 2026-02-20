using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;
using static Darp.Luau.Utils.LuauNativeMethods;

namespace Darp.Luau;

/// <summary> The LuauState </summary>
/// <remarks> Not threadsafe </remarks>
public sealed unsafe class LuauState : IDisposable
{
    // ReSharper disable once ReplaceWithFieldKeyword
    internal readonly lua_State* L;
    private int _disposing; // 0 = false, 1 = true
    private readonly int _globalsReference;
    private readonly int _callbackWrapperReference;
    private readonly UserdataRegistrationCache _cache;

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
        _callbackWrapperReference = RegisterCallbackWrapper();
        _cache = new UserdataRegistrationCache(this);
    }

    private int RegisterCallbackWrapper()
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var source =
            """
            local function wrap(f)
              return function(...)
                -- packet = isPCallOk, okFlag, errorMessage, ...
                local packed = table.pack(pcall(f, ...))
                if not packed[1] then
                  -- This should not happen
                  error("UNWRAPPED LUA ERROR: raw lua_error/longjmp crossed boundary", 2)
                end

                local okFlag = packed[2]
                if not okFlag then
                  error(packed[3], 2)
                end
                return table.unpack(packed, 3, packed.n)
              end
            end
            return wrap
            """u8;

        var chunkName = "managed_callback_wrapper"u8;
        fixed (byte* pSource = source)
        fixed (byte* pChunkName = chunkName)
        {
            nuint resultSize = 0;
            byte* pByteCode = luau_compile(pSource, (nuint)source.Length, null, &resultSize);
            int loadStatus = luau_load(L, pChunkName, pByteCode, resultSize, 0);
            LuaException.ThrowIfNotOk(L, loadStatus, "luau_load");

            int callStatus = lua_pcall(L, 0, 1, 0);
            LuaException.ThrowIfNotOk(L, callStatus, "lua_pcall");
        }
        if ((lua_Type)lua_type(L, -1) is not lua_Type.LUA_TFUNCTION)
        {
            lua_pop(L, 1);
            throw new InvalidOperationException("Managed callback wrapper chunk must return a function.");
        }
        return luaL_ref(L, LUA_REGISTRYINDEX);
    }

    internal void PushWrappedCallback(delegate* unmanaged[Cdecl]<lua_State*, int> callback, byte* debugName = null)
    {
        this.ThrowIfDisposed();
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 1);
#endif
        lua_getref(L, _callbackWrapperReference);
        lua_pushcfunction(L, callback, debugName);
        int callStatus = lua_pcall(L, 1, 1, 0);
        LuaException.ThrowIfNotOk(L, callStatus, "lua_pcall");
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

    /// <summary> The delegate type that maps Lua arguments to a single return or error </summary>
    public delegate LuauReturn LuauFunctionBuilder(LuauArgs args);

    /// <summary> Create a new LuaFunction and get the reference to it </summary>
    /// <param name="onCalled">The callback returning either a value or an error</param>
    /// <returns> The LuaFunction with the reference to the lua memory </returns>
    public LuauFunction CreateFunctionBuilder(LuauFunctionBuilder onCalled)
    {
        this.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(onCalled);
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var f = F;
#pragma warning disable CS0618 // This is the only place we want to save the delegates
        _delegateSave.Add(f);
        IntPtr intPtr = Marshal.GetFunctionPointerForDelegate(f);
#pragma warning restore CS0618 // Type or member is obsolete

        PushWrappedCallback((delegate* unmanaged[Cdecl]<lua_State*, int>)intPtr);
        int refs = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauFunction(this, refs);

        int F(lua_State* luaState)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual((nint)luaState, (nint)L);
            int numberOfParameters = lua_gettop(luaState);
#if DEBUG
            using var nestedGuard = new StackGuard(L, expectedDelta: 0);
#endif
            int topBeforeInvoke = lua_gettop(luaState);
            var args = new LuauArgs(this, numberOfParameters);
            try
            {
                LuauReturn result = onCalled(args);
                lua_settop(luaState, topBeforeInvoke);

                if (!result.TryPushValues(this, out int outputCount, out string? error))
                {
                    int errorReturnCount = LuauStateMarshal.ReturnError(luaState, error);
#if DEBUG
                    nestedGuard.OverwriteExpectedDelta(errorReturnCount);
#endif
                    return errorReturnCount;
                }

                int returnCount = LuauStateMarshal.ReturnSuccess(luaState, outputCount);
#if DEBUG
                nestedGuard.OverwriteExpectedDelta(returnCount);
#endif
                return returnCount;
            }
            catch (Exception exception)
            {
                lua_settop(luaState, topBeforeInvoke);
                int returnCount = LuauStateMarshal.ReturnCallbackException(luaState, "managed function", exception);
#if DEBUG
                nestedGuard.OverwriteExpectedDelta(returnCount);
#endif
                return returnCount;
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

    /// <summary> Register userdata at lua </summary>
    /// <typeparam name="T"> The type of the userdata to create and register </typeparam>
    /// <returns> The lua object that references userdata </returns>
    public LuauUserdata CreateUserdata<T>()
        where T : class, ILuauUserData<T>, new() => CreateUserdata(new T());

    /// <summary> Register userdata at lua </summary>
    /// <param name="userdata"> The userdata to register </param>
    /// <typeparam name="T"> The type of the userdata to register </typeparam>
    /// <returns> The lua object that references userdata </returns>
    public LuauUserdata CreateUserdata<T>(in T userdata)
        where T : class, ILuauUserData<T>
    {
        this.ThrowIfDisposed();

        GCHandle registrationHandle = _cache.Register<T>();
        return CreateUserdataUnchecked(registrationHandle, userdata);
    }

    private LuauUserdata CreateUserdataUnchecked<T>(GCHandle registrationHandle, T userdata)
        where T : class
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var ptr = (LuauUserdataNative*)lua_newuserdatataggedwithmetatable(
            L,
            (nuint)sizeof(LuauUserdataNative),
            LuauUserdataNative.Tag
        );
        ptr->UserdataHandle = GCHandle.Alloc(userdata, GCHandleType.Normal);
        ptr->RegistryValueHandle = registrationHandle;
        int reference = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauUserdata(this, reference);
    }

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
        _cache.Dispose();
        if (_callbackWrapperReference is not 0)
            lua_unref(L, _callbackWrapperReference);
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
            LuaException.ThrowIfNotOk(L, loadStatus, "luau_load");
            int callStatus = lua_pcall(L, 0, 0, 0);
            LuaException.ThrowIfNotOk(L, callStatus, "lua_pcall");
        }
    }
}
