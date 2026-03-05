using System.Buffers;
using System.Diagnostics;
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
    private readonly Stack<int> _activeCallbackFrameTokens = [];
    private int _nextCallbackFrameToken = 1;

    internal RegistryReferenceTracker ReferenceTracker { get; }

    [Obsolete("Used for saving delegates used in unmanaged memory to prevent them going out of scope")]
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly List<Delegate> _delegateSave = [];

    /// <summary> The global table. Used as a entry point </summary>
    public LuauTable Globals => new(this, _globalsReference);

    /// <summary> If true, the LuauState is disposed and any method will throw </summary>
    public bool IsDisposed => _disposing > 0;

    /// <summary>The effective set of built-in libraries loaded into this state.</summary>
    public LuauLibraries EnabledLibraries { get; }

    /// <summary>
    /// Gets current memory-related tracking counters for this state.
    /// </summary>
    internal LuauMemoryStatistics MemoryStatistics =>
        ReferenceTracker.GetStatistics(
#pragma warning disable CS0618 // This tracks callback roots held by this state.
            activeManagedCallbacks: _delegateSave.Count
#pragma warning restore CS0618
        );

    /// <summary> Initializes a new LuauState, and opens all default libs. </summary>
    /// <exception cref="InvalidOperationException"> Thrown if the luau state could not be created </exception>
    public LuauState()
        : this(LuauLibraries.All) { }

    /// <summary>Initializes a new LuauState with explicit library loading options.</summary>
    /// <param name="builtinLibraries">Configuration for built-in and custom libraries. Automatically adds <see cref="LuauLibraries.Minimal"/> libraries</param>
    /// <exception cref="InvalidOperationException">Thrown if the Luau state could not be created.</exception>
    public LuauState(LuauLibraries builtinLibraries)
    {
        builtinLibraries |= LuauLibraries.Minimal;

        L = luaL_newstate();
        if (L is null)
            throw new InvalidOperationException("Could not create Lua state.");
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        _cache = new UserdataRegistrationCache(this);
        ReferenceTracker = new RegistryReferenceTracker(this);

        EnabledLibraries = builtinLibraries;
        OpenBuiltinLibraries(EnabledLibraries);

        // Push table to stack, get the reference and pop
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        _globalsReference = ReferenceTracker.TrackAndPopRef(L, -1, pinned: true);

        _callbackWrapperReference = RegisterCallbackWrapper();
    }

    private void OpenBuiltinLibraries(LuauLibraries libraries)
    {
        if (libraries.HasFlag(LuauLibraries.Base))
            openlib(L, ""u8, luaopen_base);
        if (libraries.HasFlag(LuauLibraries.Coroutine))
            openlib(L, LUA_COLIBNAME, luaopen_coroutine);
        if (libraries.HasFlag(LuauLibraries.Table))
            openlib(L, LUA_TABLIBNAME, luaopen_table);
        if (libraries.HasFlag(LuauLibraries.Os))
            openlib(L, LUA_OSLIBNAME, luaopen_os);
        if (libraries.HasFlag(LuauLibraries.String))
            openlib(L, LUA_STRLIBNAME, luaopen_string);
        if (libraries.HasFlag(LuauLibraries.Math))
            openlib(L, LUA_MATHLIBNAME, luaopen_math);
        if (libraries.HasFlag(LuauLibraries.Debug))
            openlib(L, LUA_DBLIBNAME, luaopen_debug);
        if (libraries.HasFlag(LuauLibraries.Utf8))
            openlib(L, LUA_UTF8LIBNAME, luaopen_utf8);
        if (libraries.HasFlag(LuauLibraries.Bit32))
            openlib(L, LUA_BITLIBNAME, luaopen_bit32);
        if (libraries.HasFlag(LuauLibraries.Buffer))
            openlib(L, LUA_BUFFERLIBNAME, luaopen_buffer);
        if (libraries.HasFlag(LuauLibraries.Vector))
            openlib(L, LUA_VECLIBNAME, luaopen_vector);
    }

    private delegate int OpenLibFunc(lua_State* L);

    private static void openlib(lua_State* L, ReadOnlySpan<byte> name, OpenLibFunc openf)
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        IntPtr intPtr = Marshal.GetFunctionPointerForDelegate(openf);
        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State*, int>)intPtr, null);
        fixed (byte* pName = name)
            lua_pushstring(L, pName);
        lua_call(L, 1, 0);
    }

    /// <summary> The delegate type used to build a custom library table. </summary>
    /// <param name="state"> The <see cref="LuauState"/> the library is registered for </param>
    /// <param name="lib"> The lib table </param>
    public delegate void OpenLibraryFunc(LuauState state, in LuauTable lib);

    /// <summary>Registers a custom library table in globals.</summary>
    /// <param name="name">Global name of the library table.</param>
    /// <param name="build">Callback used to populate the created table.</param>
    public void OpenLibrary(ReadOnlySpan<char> name, OpenLibraryFunc build)
    {
        ArgumentNullException.ThrowIfNull(build);
        this.ThrowIfDisposed();
        if (Globals.ContainsKey(name))
            throw new InvalidOperationException($"Global '{name}' already exists.");

        using LuauTable table = CreateTable();
        try
        {
            build(this, table);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to register custom library '{name}'.", exception);
        }

        Globals.Set(name, table);
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
        CompileLoadAndCall(L, source, chunkName, nResults: 1);
        if ((lua_Type)lua_type(L, -1) is not lua_Type.LUA_TFUNCTION)
        {
            lua_pop(L, 1);
            throw new InvalidOperationException("Managed callback wrapper chunk must return a function.");
        }
        return ReferenceTracker.TrackAndPopRef(L, -1, pinned: true);
    }

    internal void PushWrappedCallback(delegate* unmanaged[Cdecl]<lua_State*, int> callback, byte* debugName = null)
    {
        this.ThrowIfDisposed();
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 1);
#endif
        lua_getref(L, ReferenceTracker.ResolveLuaRef(_callbackWrapperReference, nameof(LuauState)));
        lua_pushcfunction(L, callback, debugName);
        int callStatus = lua_pcall(L, 1, 1, 0);
        LuaException.ThrowIfNotOk(L, callStatus, "lua_pcall");
    }

    internal int EnterCallbackFrame()
    {
        this.ThrowIfDisposed();
        if (_nextCallbackFrameToken == int.MaxValue)
            throw new InvalidOperationException("Too many callback frames were created for this state.");

        int callbackFrameToken = _nextCallbackFrameToken++;
        _activeCallbackFrameTokens.Push(callbackFrameToken);
        return callbackFrameToken;
    }

    internal int GetCurrentCallbackFrameToken() =>
        _activeCallbackFrameTokens.Count == 0 ? 0 : _activeCallbackFrameTokens.Peek();

    internal void ExitCallbackFrame(int callbackFrameToken)
    {
        if (_activeCallbackFrameTokens.Count == 0 || _activeCallbackFrameTokens.Peek() != callbackFrameToken)
            throw new InvalidOperationException("Callback frame stack was corrupted.");

        ReferenceTracker.ReleaseCallbackFrameReferences(callbackFrameToken);

        _activeCallbackFrameTokens.Pop();
    }

    internal bool IsCallbackFrameActive(int callbackFrameToken)
    {
        if (callbackFrameToken == 0)
            return false;

        foreach (int activeToken in _activeCallbackFrameTokens)
        {
            if (activeToken == callbackFrameToken)
                return true;
        }

        return false;
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
        int reference = ReferenceTracker.TrackAndPopRef(L, -1);
        return new LuauTable(this, reference);
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
        int reference = ReferenceTracker.TrackAndPopRef(L, -1);
        return new LuauFunction(this, reference);

        int F(lua_State* luaState)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual((nint)luaState, (nint)L);
            int numberOfParameters = lua_gettop(luaState);
#if DEBUG
            using var nestedGuard = new StackGuard(L, expectedDelta: 0);
#endif
            int topBeforeInvoke = lua_gettop(luaState);
            int callbackFrameToken = EnterCallbackFrame();
            var args = new LuauArgs(this, numberOfParameters, firstParameterStackIndex: 1, callbackFrameToken);
            try
            {
                LuauReturn result = onCalled(args);
                Debug.Assert(lua_gettop(luaState) == topBeforeInvoke);

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
            finally
            {
                ExitCallbackFrame(callbackFrameToken);
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

    /// <summary>
    /// Gets an existing userdata for this managed instance or creates a new one.
    /// </summary>
    /// <param name="userdata">Managed userdata instance.</param>
    /// <typeparam name="T">Managed userdata type.</typeparam>
    /// <returns>A Lua userdata reference associated with <paramref name="userdata"/>.</returns>
    public LuauUserdata GetOrCreateUserdata<T>(T userdata)
        where T : class, ILuauUserData<T>
    {
        this.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(userdata);
        return _cache.GetOrCreate(userdata);
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

        int reference = ReferenceTracker.TrackAndPopRef(L, -1);
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

        int reference = ReferenceTracker.TrackAndPopRef(L, -1);
        return new LuauBuffer(this, reference);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0)
            return;
        _cache.Dispose();
        ReferenceTracker.ReleaseAll();
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
        CompileLoadAndCall(L, source, chunkName, nResults: 0);
    }
}
