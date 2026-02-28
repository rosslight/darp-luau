using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public enum LuauValueType
{
    // Nil has to be 0 to allow default(LuauValue) to not cause problems
    Nil = 0,
    String,
    Number,
    Boolean,
    Table,
    Function,
    Thread,
    Userdata,
    Vector,
    Buffer,
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct LuauValue : IDisposable
{
    public LuauValueType Type { get; }
    private readonly LuauState? _state;
    private readonly LuauValueUnion _union;

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct LuauValueUnion
    {
        [FieldOffset(0)]
        public readonly bool ValueBool;

        [FieldOffset(0)]
        public readonly double ValueDouble;

        [FieldOffset(0)]
        public readonly int ValueReference;

        public LuauValueUnion(bool value) => ValueBool = value;

        public LuauValueUnion(double value) => ValueDouble = value;

        public LuauValueUnion(int value) => ValueReference = value;
    }

    private LuauValue(LuauState? state, LuauValueType type, LuauValueUnion union)
    {
        Type = type;
        _state = state;
        _union = union;
    }

    public static explicit operator LuauValue(bool value) =>
        new(null, LuauValueType.Boolean, new LuauValueUnion(value));

    public static explicit operator LuauValue(double value) =>
        new(null, LuauValueType.Number, new LuauValueUnion(value));

    public static explicit operator LuauValue(LuauString value) =>
        new(value.State, LuauValueType.String, new LuauValueUnion(value.Reference));

    public static explicit operator LuauValue(LuauTable value)
    {
        LuauState? state = value.State;
        state.ThrowIfDisposed();
        int clonedReference = state.ReferenceTracker.CloneTrackedReference(value.Reference, nameof(LuauTable));
        return new LuauValue(state, LuauValueType.Table, new LuauValueUnion(clonedReference));
    }

    internal static LuauValue FromSource(in LuauRefSource source, LuauValueType type)
    {
        return new LuauValue(source.State, type, new LuauValueUnion(source.Reference));
    }

    public static explicit operator LuauValue(LuauBuffer value) =>
        new(value.State, LuauValueType.Buffer, new LuauValueUnion(value.Reference));

    public static explicit operator LuauValue(LuauUserdata value) =>
        new(value.State, LuauValueType.Userdata, new LuauValueUnion(value.Reference));

    public unsafe bool TryGet<T>([NotNullWhen(true)] out T? value, bool acceptNil = false)
        where T : allows ref struct
    {
        if (typeof(T) == typeof(LuauValue))
        {
            LuauValue temp = this;
            if (
                _state is not null
                && Type
                    is LuauValueType.String
                        or LuauValueType.Table
                        or LuauValueType.Function
                        or LuauValueType.Userdata
                        or LuauValueType.Buffer
                && _state.ReferenceTracker.HasRegistryReference(_union.ValueReference)
            )
            {
                int clonedReference = _state.ReferenceTracker.CloneTrackedReference(
                    _state.L,
                    _union.ValueReference,
                    nameof(LuauValue)
                );
                temp = new LuauValue(_state, Type, new LuauValueUnion(clonedReference));
            }
            value = Unsafe.As<LuauValue, T>(ref temp)!;
            return true;
        }
        value = default;
        switch (Type)
        {
            case LuauValueType.Nil:
                if (typeof(T) == typeof(LuauNil))
                {
                    var temp = default(LuauNil);
                    value = Unsafe.As<LuauNil, T>(ref temp)!;
                    return true;
                }
                if (!acceptNil)
                {
                    return false;
                }
                if (typeof(T) == typeof(double?))
                {
                    double? temp = null;
                    value = Unsafe.As<double?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Half?))
                {
                    Half? temp = null;
                    value = Unsafe.As<Half?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(float?))
                {
                    float? temp = null;
                    value = Unsafe.As<float?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(decimal?))
                {
                    decimal? temp = null;
                    value = Unsafe.As<decimal?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(double?))
                {
                    double? temp = null;
                    value = Unsafe.As<double?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(sbyte?))
                {
                    sbyte? temp = null;
                    value = Unsafe.As<sbyte?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(byte?))
                {
                    byte? temp = null;
                    value = Unsafe.As<byte?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(short?))
                {
                    short? temp = null;
                    value = Unsafe.As<short?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ushort?))
                {
                    ushort? temp = null;
                    value = Unsafe.As<ushort?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(int?))
                {
                    int? temp = null;
                    value = Unsafe.As<int?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(uint?))
                {
                    uint? temp = null;
                    value = Unsafe.As<uint?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(long?))
                {
                    long? temp = null;
                    value = Unsafe.As<long?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ulong?))
                {
                    ulong? temp = null;
                    value = Unsafe.As<ulong?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Int128?))
                {
                    Int128? temp = null;
                    value = Unsafe.As<Int128?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(UInt128?))
                {
                    UInt128? temp = null;
                    value = Unsafe.As<UInt128?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(bool?))
                {
                    bool? temp = null;
                    value = Unsafe.As<bool?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(string))
                {
                    string? temp = null;
                    value = Unsafe.As<string?, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.String:
                if (_state is null || !_state.ReferenceTracker.HasRegistryReference(_union.ValueReference))
                    return false;
                if (typeof(T) == typeof(string))
                {
                    using var luauString = new LuauString(_state, _union.ValueReference);
                    string temp = luauString.ToString();
                    value = Unsafe.As<string, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(LuauString))
                {
                    int clonedReference = _state.ReferenceTracker.CloneTrackedReference(
                        _state.L,
                        _union.ValueReference,
                        nameof(LuauValue)
                    );
                    var temp = new LuauString(_state, clonedReference);
                    value = Unsafe.As<LuauString, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Number:
                if (typeof(T) == typeof(double))
                {
                    double temp = _union.ValueDouble;
                    value = Unsafe.As<double, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Half))
                {
                    var temp = (Half)_union.ValueDouble;
                    value = Unsafe.As<Half, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(float))
                {
                    float temp = (float)_union.ValueDouble;
                    value = Unsafe.As<float, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(decimal))
                {
                    decimal temp = (decimal)_union.ValueDouble;
                    value = Unsafe.As<decimal, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(double))
                {
                    double temp = _union.ValueDouble;
                    value = Unsafe.As<double, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(sbyte))
                {
                    sbyte temp = (sbyte)_union.ValueDouble;
                    value = Unsafe.As<sbyte, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(byte))
                {
                    byte temp = (byte)_union.ValueDouble;
                    value = Unsafe.As<byte, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(short))
                {
                    short temp = (short)_union.ValueDouble;
                    value = Unsafe.As<short, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ushort))
                {
                    ushort temp = (ushort)_union.ValueDouble;
                    value = Unsafe.As<ushort, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(int))
                {
                    int temp = (int)_union.ValueDouble;
                    value = Unsafe.As<int, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(uint))
                {
                    uint temp = (uint)_union.ValueDouble;
                    value = Unsafe.As<uint, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(long))
                {
                    long temp = (long)_union.ValueDouble;
                    value = Unsafe.As<long, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ulong))
                {
                    ulong temp = (ulong)_union.ValueDouble;
                    value = Unsafe.As<ulong, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Int128))
                {
                    var temp = (Int128)_union.ValueDouble;
                    value = Unsafe.As<Int128, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(UInt128))
                {
                    UInt128 temp = (UInt128)_union.ValueDouble;
                    value = Unsafe.As<UInt128, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Boolean:
                if (typeof(T) == typeof(bool))
                {
                    bool temp = _union.ValueBool;
                    value = Unsafe.As<bool, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Table:
                if (_state is null || !_state.ReferenceTracker.HasRegistryReference(_union.ValueReference))
                    return false;
                if (typeof(T) == typeof(LuauTable))
                {
                    var temp = new LuauTable(_state, _union.ValueReference);
                    value = Unsafe.As<LuauTable, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Function:
                if (_state is null || !_state.ReferenceTracker.HasRegistryReference(_union.ValueReference))
                    return false;
                if (typeof(T) == typeof(LuauFunction))
                {
                    int clonedReference = _state.ReferenceTracker.CloneTrackedReference(
                        _state.L,
                        _union.ValueReference,
                        nameof(LuauValue)
                    );
                    var temp = new LuauFunction(_state, clonedReference);
                    value = Unsafe.As<LuauFunction, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Userdata:
                if (_state is null || !_state.ReferenceTracker.HasRegistryReference(_union.ValueReference))
                    return false;
                if (typeof(T) == typeof(LuauUserdata))
                {
                    int clonedReference = _state.ReferenceTracker.CloneTrackedReference(
                        _state.L,
                        _union.ValueReference,
                        nameof(LuauValue)
                    );
                    var temp = new LuauUserdata(_state, clonedReference);
                    value = Unsafe.As<LuauUserdata, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Buffer:
                if (_state is null || !_state.ReferenceTracker.HasRegistryReference(_union.ValueReference))
                    return false;
                if (typeof(T) == typeof(ReadOnlySpan<byte>))
                {
                    using var temp = new LuauBuffer(_state, _union.ValueReference);
                    if (!temp.TryGet(out ReadOnlySpan<byte> span))
                        return false;

                    value = Unsafe.As<ReadOnlySpan<byte>, T>(ref span)!;
                    return true;
                }
                if (typeof(T) == typeof(byte[]))
                {
                    using var temp = new LuauBuffer(_state, _union.ValueReference);
                    if (!temp.TryGet(out byte[] bytes))
                        return false;

                    value = Unsafe.As<byte[], T>(ref bytes)!;
                    return true;
                }
                if (typeof(T) == typeof(LuauBuffer))
                {
                    int clonedReference = _state.ReferenceTracker.CloneTrackedReference(
                        _state.L,
                        _union.ValueReference,
                        nameof(LuauValue)
                    );
                    var temp = new LuauBuffer(_state, clonedReference);
                    value = Unsafe.As<LuauBuffer, T>(ref temp)!;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary> Push a single value on the stack </summary>
    /// <param name="L"> The lua state </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the Type is invalid </exception>
    internal unsafe void Push(lua_State* L)
    {
        if (_state is not null)
        {
            _state.ThrowIfDisposed();
            if ((nint)_state.L != (nint)L)
                throw new InvalidOperationException("Cross-state reference usage is not allowed.");
        }

        switch (Type)
        {
            case LuauValueType.Nil:
                lua_pushnil(L);
                break;
            case LuauValueType.Boolean:
                lua_pushboolean(L, _union.ValueBool ? 1 : 0);
                break;
            case LuauValueType.Number:
                lua_pushnumber(L, _union.ValueDouble);
                break;
            case LuauValueType.String
            or LuauValueType.Table
            or LuauValueType.Function
            or LuauValueType.Userdata
            or LuauValueType.Buffer:
                if (_state is null)
                    throw new InvalidOperationException("No LuauState present.");
                LuauState state = _state;
                int reference = state.ReferenceTracker.ResolveLuaRef(_union.ValueReference, nameof(LuauValue));
                lua_getref(L, reference);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static unsafe LuauValue ToValue(LuauState state, int index = -1)
    {
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var type = (lua_Type)lua_type(L, index);
        switch (type)
        {
            case lua_Type.LUA_TNIL:
                return default;
            case lua_Type.LUA_TBOOLEAN:
                bool valueBool = lua_toboolean(L, index) == 1;
                return new LuauValue(state, LuauValueType.Boolean, new LuauValueUnion(valueBool));
            case lua_Type.LUA_TNUMBER:
                double valueNumber = lua_tonumber(L, index);
                return new LuauValue(state, LuauValueType.Number, new LuauValueUnion(valueNumber));
            case lua_Type.LUA_TSTRING:
                int referenceString = state.ReferenceTracker.TrackRef(L, index);
                return new LuauValue(state, LuauValueType.String, new LuauValueUnion(referenceString));
            case lua_Type.LUA_TTABLE:
                int referenceTable = state.ReferenceTracker.TrackRef(L, index);
                return new LuauValue(state, LuauValueType.Table, new LuauValueUnion(referenceTable));
            case lua_Type.LUA_TFUNCTION:
                int referenceFunction = state.ReferenceTracker.TrackRef(L, index);
                return new LuauValue(state, LuauValueType.Function, new LuauValueUnion(referenceFunction));
            case lua_Type.LUA_TUSERDATA:
                int referenceUserdata = state.ReferenceTracker.TrackRef(L, index);
                return new LuauValue(state, LuauValueType.Userdata, new LuauValueUnion(referenceUserdata));
            case lua_Type.LUA_TBUFFER:
                int referenceBuffer = state.ReferenceTracker.TrackRef(L, index);
                return new LuauValue(state, LuauValueType.Buffer, new LuauValueUnion(referenceBuffer));
            default:
                throw new NotSupportedException($"The lua type {type} is not supported!");
        }
    }

    /// <summary>
    /// Releases an owned registry reference when this value represents a reference-backed Lua type.
    /// </summary>
    public void Dispose()
    {
        if (_state is null)
            return;
        if (
            Type
            is not LuauValueType.String
                and not LuauValueType.Table
                and not LuauValueType.Function
                and not LuauValueType.Userdata
                and not LuauValueType.Buffer
        )
            return;

        _state.ReferenceTracker.ReleaseRef(_union.ValueReference);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        switch (Type)
        {
            case LuauValueType.Nil:
                return "nil";
            case LuauValueType.String:
            {
                using var temp = new LuauString(_state, _union.ValueReference);
                return temp.ToString();
            }
            case LuauValueType.Number:
                return _union.ValueDouble.ToString(CultureInfo.InvariantCulture);
            case LuauValueType.Boolean:
                return _union.ValueBool ? "true" : "false";
            case LuauValueType.Table:
                return new LuauTable(_state, _union.ValueReference).ToString();
            case LuauValueType.Function:
                return new LuauFunction(_state, _union.ValueReference).ToString();
            case LuauValueType.Userdata:
            {
                using var temp = new LuauUserdata(_state, _union.ValueReference);
                return temp.ToString();
            }
            case LuauValueType.Buffer:
            {
                using var temp = new LuauBuffer(_state, _union.ValueReference);
                return temp.ToString();
            }
            default:
                return "n/a";
        }
    }
}
