namespace Darp.Luau;

public static class LuauStateExtensions
{
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
