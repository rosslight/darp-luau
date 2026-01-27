using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

public static class LuauTableExtensions
{
    public static bool TryGet<TValue>(this in LuauTable table, IntoLuau key, [NotNullWhen(true)] out TValue? value)
        where TValue : allows ref struct
    {
        value = default;
        table.State.ThrowIfDisposed();
        return table.TryGet(key, out LuauValue luauValue) && luauValue.TryGet(out value);
    }
}
