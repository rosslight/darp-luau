using Shouldly;

namespace Darp.Luau.Tests.Table;

public sealed class LuauTableErrorTests : IDisposable
{
    private readonly LuauState _lua = new();

    [Fact]
    public void Set_null_key_throws()
    {
        using LuauTable table = _lua.CreateTable();
        string? nullKey = null;

        Should.Throw<ArgumentNullException>(() => table.Set(nullKey!, 42));
    }

    [Fact]
    public void Table_after_state_disposal_throws()
    {
        LuauTable table = _lua.CreateTable();
        table.Set("key", 42);
        _lua.Dispose();

        Should.Throw<ObjectDisposedException>(() => table.Set("key2", 43));
    }

    [Fact]
    public void Table_after_table_dispose_throws()
    {
        LuauTable table = _lua.CreateTable();
        table.Set("key", 42);
        table.Dispose();

        Should.Throw<ObjectDisposedException>(() => table.Set("key2", 43));
    }

    public void Dispose()
    {
        if (!_lua.IsDisposed)
            _lua.MemoryStatistics.ActiveRegistryReferences.ShouldBe(2UL);
        _lua.Dispose();
    }
}
