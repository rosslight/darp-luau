using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Input view used by <see cref="ILuauUserData{T}.OnSetIndex"/> callbacks.
/// Represents exactly one assigned value.
/// </summary>
public readonly ref struct LuauArgsSingle
{
    private readonly LuauArgs _args;

    /// <summary>
    /// Initializes a single-argument wrapper over <see cref="LuauArgs"/>.
    /// </summary>
    /// <param name="args">Underlying argument view.</param>
    internal LuauArgsSingle(LuauArgs args) => _args = args;

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out double, out string)"/>
    public bool TryReadNumber(out double value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out double?, out string)"/>
    public bool TryReadNumberOrNil(out double? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out sbyte, out string)"/>
    public bool TryReadNumber(out sbyte value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out sbyte?, out string)"/>
    public bool TryReadNumberOrNil(out sbyte? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out byte, out string)"/>
    public bool TryReadNumber(out byte value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out byte?, out string)"/>
    public bool TryReadNumberOrNil(out byte? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out short, out string)"/>
    public bool TryReadNumber(out short value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out short?, out string)"/>
    public bool TryReadNumberOrNil(out short? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out ushort, out string)"/>
    public bool TryReadNumber(out ushort value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out ushort?, out string)"/>
    public bool TryReadNumberOrNil(out ushort? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out int, out string)"/>
    public bool TryReadNumber(out int value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out int?, out string)"/>
    public bool TryReadNumberOrNil(out int? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out uint, out string)"/>
    public bool TryReadNumber(out uint value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out uint?, out string)"/>
    public bool TryReadNumberOrNil(out uint? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out long, out string)"/>
    public bool TryReadNumber(out long value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out long?, out string)"/>
    public bool TryReadNumberOrNil(out long? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out ulong, out string)"/>
    public bool TryReadNumber(out ulong value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out ulong?, out string)"/>
    public bool TryReadNumberOrNil(out ulong? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out float, out string)"/>
    public bool TryReadNumber(out float value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out float?, out string)"/>
    public bool TryReadNumberOrNil(out float? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumber(int, out decimal, out string)"/>
    public bool TryReadNumber(out decimal value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumber(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadNumberOrNil(int, out decimal?, out string)"/>
    public bool TryReadNumberOrNil(out decimal? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadNumberOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadUtf8String(int, out ReadOnlySpan{byte}, out string)"/>
    public bool TryReadUtf8String(out ReadOnlySpan<byte> value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadUtf8String(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadUtf8String(int, out string, out string)"/>
    public bool TryReadUtf8String([NotNullWhen(true)] out string? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadUtf8String(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadUtf8StringOrNil(int, out ReadOnlySpan{byte}, out bool, out string)"/>
    public bool TryReadUtf8StringOrNil(
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    ) => _args.TryReadUtf8StringOrNil(1, out value, out isNil, out error);

    /// <inheritdoc cref="LuauArgs.TryReadUtf8StringOrNil(int, out string, out string)"/>
    public bool TryReadUtf8StringOrNil(out string? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadUtf8StringOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBoolean(int, out bool, out string)"/>
    public bool TryReadBoolean(out bool value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBoolean(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBooleanOrNil(int, out bool?, out string)"/>
    public bool TryReadBooleanOrNil(out bool? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBooleanOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBufferOrNil(int, out byte[], out string)"/>
    public bool TryReadBufferOrNil(out byte[]? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBufferOrNil(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadLuauValue(int, out LuauValue, out string)"/>
    public bool TryReadLuauValue(out LuauValue value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauValue(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadLuauTable(int, out LuauTable, out string)"/>
    public bool TryReadLuauTable(out LuauTable value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauTable(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadLuauFunction(int, out LuauFunction, out string)"/>
    public bool TryReadLuauFunction(out LuauFunction value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauFunction(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadLuauString(int, out LuauString, out string)"/>
    public bool TryReadLuauString(out LuauString value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauString(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadLuauBuffer(int, out LuauBuffer, out string)"/>
    public bool TryReadLuauBuffer(out LuauBuffer value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadLuauBuffer(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBuffer(int, out ReadOnlySpan{byte}, out string)"/>
    public bool TryReadBuffer(out ReadOnlySpan<byte> value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBuffer(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBuffer(int, out byte[], out string)"/>
    public bool TryReadBuffer([NotNullWhen(true)] out byte[]? value, [NotNullWhen(false)] out string? error) =>
        _args.TryReadBuffer(1, out value, out error);

    /// <inheritdoc cref="LuauArgs.TryReadBufferOrNil(int, out ReadOnlySpan{byte}, out bool, out string)"/>
    public bool TryReadBufferOrNil(
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    ) => _args.TryReadBufferOrNil(1, out value, out isNil, out error);
}
