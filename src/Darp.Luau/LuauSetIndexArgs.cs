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
}
