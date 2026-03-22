using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

internal enum LuauChunkSourceKind : byte
{
    Chars,
    Utf8Bytes,
}

/// <summary>
/// Compiler settings used when compiling a Luau chunk from source.
/// </summary>
/// <param name="OptimizationLevel">The Luau compiler optimization level.</param>
/// <param name="DebugLevel">The Luau compiler debug information level.</param>
public readonly record struct LuauCompiler(int OptimizationLevel, int DebugLevel)
{
    /// <summary>
    /// The default compiler configuration used by chunk loading APIs.
    /// </summary>
    public static readonly LuauCompiler Default = new(OptimizationLevel: 1, DebugLevel: 1);
}

/// <summary>
/// Represents a lightweight executable Luau chunk loaded from source text.
/// </summary>
/// <remarks>
/// This type is an ephemeral wrapper over source text. Executing it recompiles the source each time.
/// Use <see cref="ToFunction"/> to compile and load the chunk as a reusable <see cref="LuauFunction"/>.
/// </remarks>
public readonly ref struct LuauChunk
{
    private const int LuaMultRet = -1;

    private readonly LuauState? _state;
    private readonly LuauCompiler _compiler;
    private readonly LuauChunkSourceKind _sourceKind;
    private readonly ReadOnlySpan<char> _charSource;
    private readonly ReadOnlySpan<byte> _utf8Source;
    private readonly ReadOnlySpan<char> _chunkName;

    internal LuauChunk(LuauState? state, LuauCompiler compiler, ReadOnlySpan<char> source)
        : this(state, compiler, LuauChunkSourceKind.Chars, source, default, default) { }

    internal LuauChunk(LuauState? state, LuauCompiler compiler, ReadOnlySpan<byte> source)
        : this(state, compiler, LuauChunkSourceKind.Utf8Bytes, default, source, default) { }

    private LuauChunk(
        LuauState? state,
        LuauCompiler compiler,
        LuauChunkSourceKind sourceKind,
        ReadOnlySpan<char> charSource,
        ReadOnlySpan<byte> utf8Source,
        ReadOnlySpan<char> chunkName
    )
    {
        _state = state;
        _compiler = compiler;
        _sourceKind = sourceKind;
        _charSource = charSource;
        _utf8Source = utf8Source;
        _chunkName = chunkName;
    }

    /// <summary> Gets the environment configured for this chunk. </summary>
    /// <remarks>Chunk environments are not implemented yet.</remarks>
    public LuauTable Environment { get; }

    /// <summary> Returns a copy of this chunk configured to run with the provided environment. </summary>
    /// <param name="environment">The environment table to bind to the chunk.</param>
    /// <returns>A chunk configured with the provided environment.</returns>
    /// <remarks>Chunk environments are not implemented yet.</remarks>
    public LuauChunk WithEnvironment(LuauTable environment) => throw new NotImplementedException();

    /// <summary> Returns a copy of this chunk configured with a specific chunk name. </summary>
    /// <param name="chunkName">The chunk name to use when loading the compiled bytecode.</param>
    /// <returns>A chunk configured with the provided chunk name.</returns>
    public LuauChunk WithName(ReadOnlySpan<char> chunkName) =>
        new(_state, _compiler, _sourceKind, _charSource, _utf8Source, chunkName);

    /// <summary> Compiles and executes the chunk, ignoring any return values. </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    public void Execute(params RefEnumerable<IntoLuau> args) => ExecuteCore(args, nResults: 0);

    /// <summary> Compiles and executes the chunk and converts the first return value. </summary>
    /// <param name="args">The arguments passed to the chunk.</param>
    /// <typeparam name="TR">Managed return type to convert to.</typeparam>
    /// <returns>The first Luau return value converted to <typeparamref name="TR"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    /// <exception cref="InvalidCastException">Thrown when the return value cannot be converted to <typeparamref name="TR"/>.</exception>
    public TR Execute<TR>(params RefEnumerable<IntoLuau> args) =>
        ExecuteCore(args, nResults: 1, LuauFunctionInvokeCore.ResultSelector<TR>);

    /// <summary> Compiles and executes the chunk and converts the first two return values. </summary>
    /// <param name="args">The arguments passed to the chunk.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <returns>The first two Luau return values converted to a tuple.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    /// <exception cref="InvalidCastException">Thrown when a return value cannot be converted to the requested managed type.</exception>
    public (TR1, TR2) Execute<TR1, TR2>(params RefEnumerable<IntoLuau> args) =>
        ExecuteCore(args, nResults: 2, LuauFunctionInvokeCore.ResultSelector<TR1, TR2>);

    /// <summary> Compiles and executes the chunk and converts the first three return values. </summary>
    /// <param name="args">The arguments passed to the chunk.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR3">Managed return type to convert to.</typeparam>
    /// <returns>The first three Luau return values converted to a tuple.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    /// <exception cref="InvalidCastException">Thrown when a return value cannot be converted to the requested managed type.</exception>
    public (TR1, TR2, TR3) Execute<TR1, TR2, TR3>(params RefEnumerable<IntoLuau> args) =>
        ExecuteCore(args, nResults: 3, LuauFunctionInvokeCore.ResultSelector<TR1, TR2, TR3>);

    /// <summary> Compiles and executes the chunk and converts the first four return values. </summary>
    /// <param name="args">The arguments passed to the chunk.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR3">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR4">Managed return type to convert to.</typeparam>
    /// <returns>The first four Luau return values converted to a tuple.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    /// <exception cref="InvalidCastException">Thrown when a return value cannot be converted to the requested managed type.</exception>
    public (TR1, TR2, TR3, TR4) Execute<TR1, TR2, TR3, TR4>(params RefEnumerable<IntoLuau> args) =>
        ExecuteCore(args, nResults: 4, LuauFunctionInvokeCore.ResultSelector<TR1, TR2, TR3, TR4>);

    /// <summary>
    /// Compiles and executes the chunk and returns all Luau return values as raw <see cref="LuauValue"/> instances.
    /// </summary>
    /// <param name="args">The arguments passed to the chunk.</param>
    /// <returns>All Luau return values as an array.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load or runtime error.</exception>
    public LuauValue[] ExecuteMulti(params RefEnumerable<IntoLuau> args) =>
        ExecuteCore(args, nResults: LuaMultRet, LuauFunctionInvokeCore.ResultSelectorMulti);

    /// <summary> Compiles and loads the chunk as a reusable <see cref="LuauFunction"/>. </summary>
    /// <returns>The loaded chunk represented as a function.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the owning state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a load error.</exception>
    public unsafe LuauFunction ToFunction()
    {
        LuauState state = GetState();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        LoadCompiledChunk(L);
        ulong reference = state.ReferenceTracker.TrackAndPopRef(L, -1);
        return new LuauFunction(state, reference);
    }

    private unsafe TResult ExecuteCore<TResult>(
        RefEnumerable<IntoLuau> args,
        int nResults,
        Func<LuauArgs, TResult> resultSelector
    )
    {
        LuauState state = GetState();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        int topBeforeInvoke = lua_gettop(L);
        try
        {
            LoadCompiledChunk(L);
            int nArgs = args.Length;
            for (int i = 0; i < nArgs; i++)
                args[i].Push(state);

            int status = lua_pcall(L, nArgs, nResults, 0);
            LuaException.ThrowIfNotOk(L, status, "lua_pcall");
            var result = new LuauArgs(state, lua_gettop(L) - topBeforeInvoke, topBeforeInvoke + 1);
            return resultSelector(result);
        }
        finally
        {
            lua_settop(L, topBeforeInvoke);
        }
    }

    private unsafe void ExecuteCore(RefEnumerable<IntoLuau> args, int nResults)
    {
        LuauState state = GetState();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        int topBeforeInvoke = lua_gettop(L);
        try
        {
            LoadCompiledChunk(L);
            int nArgs = args.Length;
            for (int i = 0; i < nArgs; i++)
                args[i].Push(state);

            int status = lua_pcall(L, nArgs, nResults, 0);
            LuaException.ThrowIfNotOk(L, status, "lua_pcall");
        }
        finally
        {
            lua_settop(L, topBeforeInvoke);
        }
    }

    private unsafe void LoadCompiledChunk(lua_State* L)
    {
        lua_CompileOptions options = new()
        {
            optimizationLevel = _compiler.OptimizationLevel,
            debugLevel = _compiler.DebugLevel,
        };

        if (_sourceKind == LuauChunkSourceKind.Utf8Bytes)
        {
            fixed (byte* pSource = _utf8Source)
            {
                nuint nSizeByteCode = 0;
                byte* pByteCode = luau_compile(pSource, (nuint)_utf8Source.Length, &options, &nSizeByteCode);
                try
                {
                    LoadBytecode(L, pByteCode, nSizeByteCode);
                }
                finally
                {
                    luau_free(pByteCode);
                }
            }
            return;
        }

        int nBytes = Encoding.UTF8.GetByteCount(_charSource);
        byte[] bytesSource = ArrayPool<byte>.Shared.Rent(nBytes);
        try
        {
            Span<byte> bytesSpan = bytesSource.AsSpan(0, nBytes);
            int actualNBytes = Encoding.UTF8.GetBytes(_charSource, bytesSpan);
            bytesSpan = bytesSpan[..actualNBytes];
            fixed (byte* pSource = bytesSpan)
            {
                nuint nSizeByteCode = 0;
                byte* pByteCode = luau_compile(pSource, (nuint)actualNBytes, &options, &nSizeByteCode);
                try
                {
                    LoadBytecode(L, pByteCode, nSizeByteCode);
                }
                finally
                {
                    luau_free(pByteCode);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytesSource);
        }
    }

    private unsafe void LoadBytecode(lua_State* L, byte* pByteCode, nuint nSizeByteCode)
    {
        ReadOnlySpan<char> chunkName = _chunkName.IsEmpty ? "main" : _chunkName;

        int nChunkNameBytes = Encoding.UTF8.GetByteCount(chunkName);
        Span<byte> chunkNameBuffer = stackalloc byte[nChunkNameBytes + 1];

        int actualChunkNameBytes = Encoding.UTF8.GetBytes(chunkName, chunkNameBuffer);
        chunkNameBuffer[actualChunkNameBytes] = 0;
        fixed (byte* pChunkName = chunkNameBuffer)
        {
            int loadStatus = luau_load(L, pChunkName, pByteCode, nSizeByteCode, 0);
            LuaException.ThrowIfNotOk(L, loadStatus, "luau_load");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LuauState GetState()
    {
        ArgumentNullException.ThrowIfNull(_state);
        _state.ThrowIfDisposed();
        return _state;
    }
}
