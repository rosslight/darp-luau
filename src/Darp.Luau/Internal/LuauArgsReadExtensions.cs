namespace Darp.Luau.Internal;

internal static class LuauArgsReadExtensions
{
    public static T Read<T>(this in LuauArgs args, int index)
    {
        if (!args.TryReadLuauValue(index, out LuauValue value, out string? error))
            throw new ArgumentOutOfRangeException(nameof(index), error);
        try
        {
            return value.TryGet(out T? result, acceptNil: default(T) is null)
                ? result
                : throw new InvalidCastException($"Could not cast {typeof(T).Name} to {value.Type}.");
        }
        finally
        {
            value.Dispose();
        }
    }
}
