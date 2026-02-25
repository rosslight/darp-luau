using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class LuauTableUserdataTests
{
    [Fact]
    public void Table_GetUserdata_ShouldResolveManagedInstance()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        var value = new ValueUserdata { Value = 7 };

        table.Set("value", value);

        ValueUserdata resolved = table.GetUserdata<ValueUserdata>("value");
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Table_GetLuauUserdata_ShouldReturnReference()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        var value = new ValueUserdata();

        table.Set("value", value);

        using (LuauUserdata userdata = table.GetLuauUserdata("value"))
        {
            string? error;
            userdata.TryGetManaged(out ValueUserdata? resolved, out error).ShouldBeTrue(error);
            ReferenceEquals(value, resolved).ShouldBeTrue();
        }
    }

    [Fact]
    public void Table_TryGetUserdata_WhenTypeMismatches_ReturnsFalse()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        table.Set("value", new OtherValueUserdata());

        table.TryGetUserdata<ValueUserdata>("value", out _).ShouldBeFalse();
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNil_ReturnsFalse()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();

        table.TryGetUserdata<ValueUserdata>("missing", out _).ShouldBeFalse();
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNotUserdata_ReturnsFalse()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        table.Set("value", 42);

        table.TryGetUserdata<ValueUserdata>("value", out _).ShouldBeFalse();
    }
}
