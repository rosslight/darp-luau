using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class LuauTableUserdataTests : IDisposable
{
    private readonly LuauState _state = new();

    [Fact]
    public void Table_GetUserdata_ShouldResolveManagedInstance()
    {
        using LuauTable table = _state.CreateTable();
        var value = new ValueUserdata { Value = 7 };

        table.Set("value", value);

        ValueUserdata resolved = table.GetUserdata<ValueUserdata>("value");
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Table_GetLuauUserdata_ShouldReturnReference()
    {
        using LuauTable table = _state.CreateTable();
        var value = new ValueUserdata();

        table.Set("value", value);

        using LuauUserdata userdata = table.GetLuauUserdata("value");

        userdata.TryGetManaged(out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Table_TryGetUserdata_WhenTypeMismatches_ReturnsFalse()
    {
        using LuauTable table = _state.CreateTable();
        table.Set("value", new OtherValueUserdata());

        table.TryGetUserdata<ValueUserdata>("value", out _).ShouldBeFalse();
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNil_ReturnsFalse()
    {
        using LuauTable table = _state.CreateTable();

        table.TryGetUserdata<ValueUserdata>("missing", out _).ShouldBeFalse();
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNotUserdata_ReturnsFalse()
    {
        using LuauTable table = _state.CreateTable();
        table.Set("value", 42);

        table.TryGetUserdata<ValueUserdata>("value", out _).ShouldBeFalse();
    }

    public void Dispose()
    {
        _state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(2);
        _state.Dispose();
    }
}
