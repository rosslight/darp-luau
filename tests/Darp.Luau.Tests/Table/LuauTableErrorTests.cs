using Shouldly;

namespace Darp.Luau.Tests.Table;

public sealed class LuauTableErrorTests
{
    [Fact]
    public void Set_null_key_throws()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        string? nullKey = null;

        Should.Throw<ArgumentNullException>(() => table.Set(nullKey!, 42));
    }

    [Fact]
    public void Table_after_state_disposal_throws()
    {
        var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("key", 42);
        lua.Dispose();

        Should.Throw<ObjectDisposedException>(() => table.Set("key2", 43));
    }

    [Fact]
    public void Table_after_table_dispose_throws()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("key", 42);
        table.Dispose();

        Should.Throw<ObjectDisposedException>(() => table.Set("key2", 43));
    }
}
