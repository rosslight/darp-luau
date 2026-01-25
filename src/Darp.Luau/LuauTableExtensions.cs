using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

public static class LuauTableExtensions
{
    public static void Set<TKey>(this in LuauTable table, TKey key, LuauValue value)
    {
        LuauValue.TryCreate(key, table.State, out LuauValue luauKey);
        if (luauKey.Type is LuauValueType.Nil)
            throw new ArgumentNullException(nameof(key));
        table.Set(luauKey, value);
    }

    public static void Set<TKey>(this in LuauTable table, TKey key, LuauFunction value)
    {
        table.Set(key, (LuauValue)value);
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, out LuauValue value)
    {
        value = default;
        return LuauValue.TryCreate(key, table.State, out LuauValue luauKey) && table.TryGet(luauKey, out value);
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, out bool value)
    {
        value = false;
        return table.TryGet(key, out LuauValue luaValue) && luaValue.TryGet(out value);
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, out double value)
    {
        value = 0;
        return table.TryGet(key, out LuauValue luaValue) && luaValue.TryGet(out value);
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, [NotNullWhen(true)] out string? value)
    {
        if (table.TryGet(key, out LuauValue luaValue) && luaValue.TryGet(out LuauString luaString))
        {
            value = luaString.ToString();
            return true;
        }
        value = null;
        return false;
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, out LuauTable value)
    {
        value = default;
        return table.TryGet(key, out LuauValue luaValue) && luaValue.TryGet(out value);
    }

    public static bool TryGet<TKey>(this in LuauTable table, TKey key, out LuauFunction value)
    {
        value = default;
        return table.TryGet(key, out LuauValue luaValue) && luaValue.TryGet(out value);
    }
}
