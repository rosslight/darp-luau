namespace Darp.Luau;

/// <summary>
/// Provides convenience factory helpers for <see cref="LuauState"/>.
/// </summary>
public static class LuauStateExtensions
{
    /// <summary>
    /// Creates a table populated with the supplied numeric values at one-based indices.
    /// </summary>
    /// <param name="state">The state that owns the new table.</param>
    /// <param name="values">The numeric values to store starting at index <c>1</c>.</param>
    /// <returns>A table containing the provided values in order.</returns>
    public static LuauTable CreateTable(this LuauState state, ReadOnlySpan<double> values)
    {
        ArgumentNullException.ThrowIfNull(state);
        LuauTable table = state.CreateTable();
        for (int i = 0; i < values.Length; i++)
        {
            table.Set(i + 1, values[i]);
        }
        return table;
    }
}
