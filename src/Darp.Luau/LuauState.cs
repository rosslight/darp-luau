using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;
using static Darp.Luau.Utils.LuauNativeMethods;

namespace Darp.Luau;

internal sealed record UserdataRegistryValue(
    LuauState State,
    UserdataRegistryValue.OnLuaCallback IndexCallback,
    UserdataRegistryValue.OnLuaCallback NewIndexCallback,
    UserdataRegistryValue.OnLuaCallback MethodCallback
)
{
    public unsafe delegate int OnLuaCallback(LuauState lua, object? userdata);
}

internal sealed class LuauCache(LuauState state) : IDisposable
{
    private readonly LuauState _state = state;
    private readonly Dictionary<Type, GCHandle> _registrations = [];
    private bool _userdataCallbacksRegistered;

    public unsafe GCHandle Register<T>()
        where T : ILuauUserData<T>
    {
        if (_registrations.TryGetValue(typeof(T), out GCHandle handle))
            return handle;

        EnsureUserdataCallbacksRegistered();

        var userRegistryValue = new UserdataRegistryValue(
            _state,
            IndexCallbackManaged,
            NewIndexCallbackManaged,
            MethodCallbackManaged
        );
        handle = GCHandle.Alloc(userRegistryValue);
        _registrations.Add(typeof(T), handle);

        return handle;

        static int IndexCallbackManaged(LuauState lua, object? userdata)
        {
            if (userdata is not T target)
                throw new InvalidOperationException();

            lua_State* L = lua.L;
            if (!TryGetString(L, 2, out string key))
                return 0;

            if (!T.OnIndex(target, lua, key, out IntoLuau result))
                return 0;

            result.Push(lua);
            return 1;
        }

        static int NewIndexCallbackManaged(LuauState lua, object? userdata)
        {
            if (userdata is not T target)
                throw new InvalidOperationException();

            lua_State* L = lua.L;
            if (!TryGetString(L, 2, out string key))
                return 0;

            var view = new LuauView(lua, stackIndex: 3);
            _ = T.OnSetIndex(target, view, key);
            return 0;
        }

        static int MethodCallbackManaged(LuauState lua, object? userdata)
        {
            if (userdata is not T target)
                throw new InvalidOperationException();

            lua_State* L = lua.L;
            string methodName;
            int firstParameterStackIndex;
            int numberOfParameters;
            if (TryGetNameCall(L, out methodName))
            {
                numberOfParameters = Math.Max(0, lua_gettop(L) - 1);
                firstParameterStackIndex = 2;
            }
            else if (TryGetString(L, 2, out methodName))
            {
                numberOfParameters = Math.Max(0, lua_gettop(L) - 2);
                firstParameterStackIndex = 3;
            }
            else
            {
                return 0;
            }

            int topBefore = lua_gettop(L);
            var view = new LuauFunctions(lua, numberOfParameters, firstParameterStackIndex);
            bool handled = T.OnMethodCall(target, view, methodName);
            int outputCount = lua_gettop(L) - topBefore;

            if (!handled)
            {
                if (outputCount > 0)
                    lua_pop(L, outputCount);
                return 0;
            }

            return Math.Max(0, outputCount);
        }
    }

    public void Dispose()
    {
        foreach (GCHandle handle in _registrations.Values)
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        _registrations.Clear();
    }

    private unsafe void EnsureUserdataCallbacksRegistered()
    {
        if (_userdataCallbacksRegistered)
            return;

        lua_State* L = _state.L;
        int initialTop = lua_gettop(L);
        try
        {
            lua_newtable(L);

            fixed (byte* pIndexName = "__index\0"u8)
            {
                lua_pushcfunction(L, &IndexCallback, pIndexName);
                lua_setfield(L, -2, pIndexName);
            }

            fixed (byte* pNewIndexName = "__newindex\0"u8)
            {
                lua_pushcfunction(L, &NewIndexCallback, pNewIndexName);
                lua_setfield(L, -2, pNewIndexName);
            }

            fixed (byte* pNameCallName = "__namecall\0"u8)
            {
                lua_pushcfunction(L, &MethodCallback, pNameCallName);
                lua_setfield(L, -2, pNameCallName);
            }

            lua_setuserdatametatable(L, LuauUserdataNative.Tag);
            lua_setuserdatadtor(L, LuauUserdataNative.Tag, &UserdataDestructor);
            _userdataCallbacksRegistered = true;
        }
        finally
        {
            lua_settop(L, initialTop);
        }
    }

    private static unsafe bool TryGetString(lua_State* L, int stackIndex, out string value)
    {
        if ((lua_Type)lua_type(L, stackIndex) is not lua_Type.LUA_TSTRING)
        {
            value = string.Empty;
            return false;
        }

        nuint keyLength;
        byte* keyUtf8 = lua_tolstring(L, stackIndex, &keyLength);
        if (keyUtf8 is null)
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(keyUtf8, checked((int)keyLength)));
        return true;
    }

    private static unsafe bool TryGetNameCall(lua_State* L, out string methodName)
    {
        int atom = 0;
        byte* name = lua_namecallatom(L, &atom);
        if (name is null)
        {
            methodName = string.Empty;
            return false;
        }

        int length = 0;
        while (name[length] != 0)
            length++;

        methodName = Encoding.UTF8.GetString(name, length);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void UserdataDestructor(lua_State* _, void* pUserdata)
    {
        if (pUserdata is null)
            return;

        var native = (LuauUserdataNative*)pUserdata;
        if (native->UserdataHandle.IsAllocated)
            native->UserdataHandle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int IndexCallback(lua_State* L)
    {
        UserdataRegistryValue registryValue = GetRegistryValue(L, out object? userdata);
        return registryValue.IndexCallback(registryValue.State, userdata);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int NewIndexCallback(lua_State* L)
    {
        UserdataRegistryValue registryValue = GetRegistryValue(L, out object? userdata);
        return registryValue.NewIndexCallback(registryValue.State, userdata);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int MethodCallback(lua_State* L)
    {
        UserdataRegistryValue registryValue = GetRegistryValue(L, out object? userdata);
        return registryValue.MethodCallback(registryValue.State, userdata);
    }

    private static unsafe UserdataRegistryValue GetRegistryValue(lua_State* L, out object? userdata)
    {
        var native = (LuauUserdataNative*)lua_touserdatatagged(L, 1, LuauUserdataNative.Tag);
        if (native is null)
            throw new InvalidOperationException();
        if (!native->RegistryValueHandle.IsAllocated)
            throw new InvalidOperationException();
        if (native->RegistryValueHandle.Target is not UserdataRegistryValue registryValue)
            throw new InvalidOperationException();
        if (registryValue.State.L != L)
            throw new InvalidOperationException();

        userdata = native->UserdataHandle.Target;
        return registryValue;
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
    private readonly LuauCache _cache;

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
        _cache = new LuauCache(this);
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

    public LuauUserdata CreateUserdata<T>()
        where T : class, ILuauUserData<T>, new() => CreateUserdata(new T());

    public LuauUserdata CreateUserdata<T>(in T userdata)
        where T : class, ILuauUserData<T>
    {
        this.ThrowIfDisposed();

        GCHandle registrationHandle = _cache.Register<T>();
        return CreateUserdataUnchecked(registrationHandle, userdata);
    }

    private bool TryRegisterUserdata<T>()
        where T : ILuauUserData<T>
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        string tagName = typeof(T).FullName ?? typeof(T).Name;
        Span<byte> tagNameBuffer = stackalloc byte[Encoding.UTF8.GetByteCount(tagName)];
        int numberOfBytes = Encoding.UTF8.GetBytes(tagName, tagNameBuffer);
        fixed (byte* pTagName = tagNameBuffer[..numberOfBytes])
        {
            // Returns 0, if there is already a metatable registered for this type
            if (luaL_newmetatable(L, pTagName) != 1)
                return false;
        }
        lua_newtable(L);
        // Constructor
        // lua_pushcfunction(L, s_Class2_MyFunc, "Class2.MyFunc");
        return true;
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
