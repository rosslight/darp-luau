using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Input view used by <see cref="ILuauUserData{T}.OnSetIndex"/> callbacks.
/// Represents exactly one assigned value.
/// </summary>
public readonly ref struct LuauSetIndexArgs
{
    private readonly LuauArgs _args;

    internal LuauSetIndexArgs(LuauArgs args) => _args = args;

    public ReadOnlySpan<byte> CheckString() => _args.CheckString(1);

    public ReadOnlySpan<byte> CheckStringOrNil(out bool isNull) => _args.CheckStringOrNil(1, out isNull);

    public double CheckNumber() => _args.CheckNumber(1);

    public double? CheckNumberOrNil() => _args.CheckNumberOrNil(1);

    public bool CheckBoolean() => _args.CheckBoolean(1);

    public bool? CheckBooleanOrNil() => _args.CheckBooleanOrNil(1);

    public ReadOnlySpan<byte> CheckBuffer() => _args.CheckBuffer(1);

    public ReadOnlySpan<byte> CheckBufferOrNil(out bool isNull) => _args.CheckBufferOrNil(1, out isNull);

    public LuauValue CheckLuauValue() => _args.CheckLuauValue(1);

    public LuauTable CheckLuauTable() => _args.CheckLuauTable(1);

    public LuauFunction CheckLuauFunction() => _args.CheckLuauFunction(1);

    public LuauString CheckLuauString() => _args.CheckLuauString(1);

    public LuauBuffer CheckLuauBuffer() => _args.CheckLuauBuffer(1);

    public bool TryReadNumber(out double value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    public bool TryReadNumberOrNil(out double? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    public bool TryReadUtf8String(out ReadOnlySpan<byte> value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadUtf8String(1, out value, out error);

    public bool TryReadUtf8StringOrNil(
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    ) => _args.TryReadUtf8StringOrNil(1, out value, out isNil, out error);

    public bool TryReadBoolean(out bool value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBoolean(1, out value, out error);

    public bool TryReadBooleanOrNil(out bool? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBooleanOrNil(1, out value, out error);

    public bool TryReadBuffer(out ReadOnlySpan<byte> value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBuffer(1, out value, out error);

    public bool TryReadBufferOrNil(
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    ) => _args.TryReadBufferOrNil(1, out value, out isNil, out error);

    public bool TryReadLuauValue(out LuauValue value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauValue(1, out value, out error);

    public bool TryReadLuauTable(out LuauTable value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauTable(1, out value, out error);

    public bool TryReadLuauFunction(out LuauFunction value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauFunction(1, out value, out error);

    public bool TryReadLuauString(out LuauString value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauString(1, out value, out error);

    public bool TryReadLuauBuffer(out LuauBuffer value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauBuffer(1, out value, out error);

    public bool TryRead(out int value, [NotNullWhen(false)] out string? error) =>
        _args.TryRead(1, out value, out error);

    public bool TryRead<T>(out T value, [NotNullWhen(false)] out string? error)
        where T : allows ref struct
    {
        return _args.TryRead(1, out value, out error);
    }
}
