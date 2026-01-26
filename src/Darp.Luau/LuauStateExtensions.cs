namespace Darp.Luau;

public static class LuauStateExtensions
{
    public static LuauTable CreateTable(this LuauState state, ReadOnlySpan<double> values)
    {
        ArgumentNullException.ThrowIfNull(state);
        LuauTable table = state.CreateTable();
        table.Set("0", values[0]);
        return table;
    }
}
