using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

public static class LuauTableExtensions
{
    public static void Set<TKey>(this in LuauTable table, TKey key, LuauValue value)
    {
        table.State.ThrowIfDisposed();
        LuauValue.TryCreate(key, table.State, out LuauValue luauKey);
        if (luauKey.Type is LuauValueType.Nil)
            throw new ArgumentNullException(nameof(key));
        table.Set(luauKey, value);
    }

    public static void Set<TKey>(this in LuauTable table, TKey key, LuauFunction value)
    {
        table.Set(key, (LuauValue)value);
    }

    public static bool TryGet<TKey, TValue>(this in LuauTable table, TKey key, [NotNullWhen(true)] out TValue? value)
        where TKey : allows ref struct
        where TValue : allows ref struct
    {
        value = default;
        table.State.ThrowIfDisposed();
        return LuauValue.TryCreate(key, table.State, out LuauValue luauKey)
            && table.TryGet(luauKey, out LuauValue luauValue)
            && luauValue.TryGet(out value);
    }
}
