using System.Buffers;
using System.Text;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

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
        _globalsReference = lua_ref(L, -1);
    }

    /// <summary> Create a new table </summary>
    /// <returns> The resulting table </returns>
    public LuauTable CreateTable()
    {
        this.ThrowIfDisposed();
        lua_newtable(L);
        int refPtr = lua_ref(L, -1);
        return new LuauTable(this, refPtr);
    }

    /// <summary> The delegate type that provides a save view to work with a function </summary>
    public delegate void LuauFunctionBuilder(LuauFunctions builder);

    /// <summary> Create a new LuaFunction and get the reference to it </summary>
    /// <param name="onCalled">The callback providing a </param>
    /// <returns> The LuaFunction with the reference to the lua memory </returns>
    public LuauFunction CreateFunction(LuauFunctionBuilder onCalled)
    {
        this.ThrowIfDisposed();

        lua_CFunction f = F;
#pragma warning disable CS0618 // This is the only place we want to save the delegates
        _delegateSave.Add(f);
#pragma warning restore CS0618 // Type or member is obsolete

        lua_pushcfunction(L, f, null);
        int refs = lua_ref(L, -1);
        return new LuauFunction(this, refs);

        int F(lua_State* luaState)
        {
            int numberOfParameters = lua_gettop(luaState);
            var builder = new LuauFunctions(luaState, numberOfParameters);
            try
            {
                onCalled(builder);
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

    public LuauFunction CreateFunction(Delegate value) =>
        throw new InvalidOperationException("This method should be intercepted!");

    /// <summary> Creates a new luau string </summary>
    /// <param name="value"> The string </param>
    /// <returns> The reference to the LuauString </returns>
    public LuauString CreateString(ReadOnlySpan<char> value)
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
        fixed (byte* pValue = utf8Value)
        {
            lua_pushlstring(L, pValue, (nuint)utf8Value.Length);
        }
        int reference = lua_ref(L, -1);
        return new LuauString(this, reference);
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
        if (chunkName.IsEmpty)
            chunkName = "main"u8;
        fixed (byte* pSource = source)
        fixed (byte* pChunkName = chunkName)
        {
            nuint resultSize = 0;
            byte* pByteCode = luau_compile(pSource, (nuint)source.Length, null, &resultSize);
            int loadStatus = luau_load(L, pChunkName, pByteCode, resultSize, 0);
            LuaException.ThrowIfNotOk(L, loadStatus);
            int callStatus = lua_pcall(L, 0, LUA_MULTRET, 0);
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
