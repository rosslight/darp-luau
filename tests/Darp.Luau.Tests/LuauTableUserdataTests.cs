using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class LuauTableUserdataTests
{
    [Fact]
    public void Table_TryGetUserdata_ShouldResolveManagedInstance()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        var value = new ValueUserdata { Value = 7 };

        table.Set("value", value);

        table.TryGetUserdata("value", out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Table_TryGetLuauUserdata_ShouldReturnReference()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        var value = new ValueUserdata();

        table.Set("value", value);

        table.TryGetLuauUserdata("value", out LuauUserdata userdata, out string? error).ShouldBeTrue(error);
        using (userdata)
        {
            userdata.TryGetManaged(out ValueUserdata? resolved, out error).ShouldBeTrue(error);
            ReferenceEquals(value, resolved).ShouldBeTrue();
        }
    }

    [Fact]
    public void Table_TryGetUserdata_WhenTypeMismatches_ShouldFail()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        table.Set("value", new OtherValueUserdata());

        table.TryGetUserdata<ValueUserdata>("value", out _, out string? error).ShouldBeFalse();
        error.ShouldContain("must be userdata of type");
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNil_ShouldFail()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();

        table.TryGetUserdata<ValueUserdata>("missing", out _, out string? error).ShouldBeFalse();
        error.ShouldContain("nil");
    }

    [Fact]
    public void Table_TryGetUserdata_WhenValueIsNotUserdata_ShouldFail()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        table.Set("value", 42);

        table.TryGetUserdata<ValueUserdata>("value", out _, out string? error).ShouldBeFalse();
        error.ShouldContain("LUA_TUSERDATA");
    }
}
