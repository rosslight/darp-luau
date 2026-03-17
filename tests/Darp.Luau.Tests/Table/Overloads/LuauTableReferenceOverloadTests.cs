using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests.Table.Overloads;

public sealed class LuauTableReferenceOverloadTests
{
    [Fact]
    public void TryGetLuauTable_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        using LuauTable nested = lua.CreateTable();
        nested.Set("value", 7);
        table.Set("nested", nested);

        table.TryGetLuauTable("nested", out LuauTable found).ShouldBeTrue();
        using (found)
        {
            found.TryGetNumber("value", out double value).ShouldBeTrue();
            value.ShouldBe(7);
        }
    }

    [Fact]
    public void TryGetLuauTable_FailureCases_ReturnsFalseAndDefault()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetLuauTable("missing", out LuauTable missingValue).ShouldBeFalse();
        missingValue.IsDisposed.ShouldBe(true);

        table.Set("wrong", 1);
        table.TryGetLuauTable("wrong", out LuauTable wrongTypeValue).ShouldBeFalse();
        wrongTypeValue.IsDisposed.ShouldBe(true);
    }

    [Fact]
    public void GetLuauTable_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        using LuauTable nested = lua.CreateTable();
        nested.Set("value", 7);
        table.Set("nested", nested);

        using LuauTable found = table.GetLuauTable("nested");
        found.GetNumber("value").ShouldBe(7);
    }

    [Fact]
    public void GetLuauTable_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetLuauTable("wrong"));
    }

    [Fact]
    public void TryGetLuauFunction_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        using LuauFunction function = lua.CreateFunction(() => 7);
        table.Set("function", function);

        table.TryGetLuauFunction("function", out LuauFunction found).ShouldBeTrue();
        using (found)
        {
            found.Invoke<int>().ShouldBe(7);
        }
    }

    [Fact]
    public void TryGetLuauFunction_FailureCases_ReturnsFalseAndDefault()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetLuauFunction("missing", out LuauFunction missingValue).ShouldBeFalse();
        missingValue.ShouldBe(default);

        table.Set("wrong", 1);
        table.TryGetLuauFunction("wrong", out LuauFunction wrongTypeValue).ShouldBeFalse();
        wrongTypeValue.ShouldBe(default);
    }

    [Fact]
    public void GetLuauFunction_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        using LuauFunction function = lua.CreateFunction(() => 7);
        table.Set("function", function);

        using LuauFunction found = table.GetLuauFunction("function");
        found.Invoke<int>().ShouldBe(7);
    }

    [Fact]
    public void GetLuauFunction_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetLuauFunction("wrong"));
    }

    [Fact]
    public void TryGetLuauString_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("value", "text");

        table.TryGetLuauString("value", out LuauString value).ShouldBeTrue();
        value.ToString().ShouldBe("text");
    }

    [Fact]
    public void TryGetLuauString_FailureCases_ReturnsFalseAndDefault()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetLuauString("missing", out LuauString missingValue).ShouldBeFalse();
        missingValue.ToString().ShouldBe("<nil>");

        table.Set("wrong", 1);
        table.TryGetLuauString("wrong", out LuauString wrongTypeValue).ShouldBeFalse();
        wrongTypeValue.ToString().ShouldBe("<nil>");
    }

    [Fact]
    public void GetLuauString_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("value", "text");

        table.GetLuauString("value").ToString().ShouldBe("text");
    }

    [Fact]
    public void GetLuauString_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetLuauString("wrong"));
    }

    [Fact]
    public void TryGetLuauBuffer_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        byte[] expected = [0x01, 0x02, 0x03];
        table.Set("buffer", expected);

        table.TryGetLuauBuffer("buffer", out LuauBuffer found).ShouldBeTrue();
        using (found)
        {
            found.TryGet(out byte[] bytes).ShouldBeTrue();
            bytes.ShouldBe(expected);
        }
    }

    [Fact]
    public void TryGetLuauBuffer_FailureCases_ReturnsFalseAndDefault()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetLuauBuffer("missing", out LuauBuffer missingValue).ShouldBeFalse();
        missingValue.ShouldBe(default);

        table.Set("wrong", 1);
        table.TryGetLuauBuffer("wrong", out LuauBuffer wrongTypeValue).ShouldBeFalse();
        wrongTypeValue.ShouldBe(default);
    }

    [Fact]
    public void GetLuauBuffer_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        byte[] expected = [0x01, 0x02, 0x03];
        table.Set("buffer", expected);

        using LuauBuffer found = table.GetLuauBuffer("buffer");
        found.TryGet(out byte[] bytes).ShouldBeTrue();
        bytes.ShouldBe(expected);
    }

    [Fact]
    public void GetLuauBuffer_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetLuauBuffer("wrong"));
    }

    [Fact]
    public void TryGetLuauUserdata_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        var value = new ValueUserdata();
        table.Set("value", value);

        table.TryGetLuauUserdata("value", out LuauUserdata found).ShouldBeTrue();
        using (found)
        {
            found.TryGetManaged(out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
            ReferenceEquals(value, resolved).ShouldBeTrue();
        }
    }

    [Fact]
    public void GetLuauUserdata_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        var value = new ValueUserdata();
        table.Set("value", value);

        using LuauUserdata found = table.GetLuauUserdata("value");
        found.TryGetManaged(out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void GetLuauUserdata_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetLuauUserdata("wrong"));
    }

    [Fact]
    public void TryGetUserdataOrNil_SuccessWithValue()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        var value = new ValueUserdata();
        table.Set("value", value);

        table.TryGetUserdataOrNil("value", out ValueUserdata? found).ShouldBeTrue();
        ReferenceEquals(value, found).ShouldBeTrue();
    }

    [Fact]
    public void TryGetUserdataOrNil_SuccessWithNil()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetUserdataOrNil("missing", out ValueUserdata? found).ShouldBeTrue();
        found.ShouldBeNull();
    }

    [Fact]
    public void TryGetUserdataOrNil_ErrorCase_ReturnsFalseAndNull()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        table.TryGetUserdataOrNil("wrong", out ValueUserdata? found).ShouldBeFalse();
        found.ShouldBeNull();
    }

    [Fact]
    public void GetUserdata_Success()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        var value = new ValueUserdata();
        table.Set("value", value);

        ValueUserdata found = table.GetUserdata<ValueUserdata>("value");
        ReferenceEquals(value, found).ShouldBeTrue();
    }

    [Fact]
    public void GetUserdata_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetUserdata<ValueUserdata>("wrong"));
    }

    [Fact]
    public void GetUserdataOrNil_SuccessWithValue()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        var value = new ValueUserdata();
        table.Set("value", value);

        ValueUserdata? found = table.GetUserdataOrNil<ValueUserdata>("value");
        ReferenceEquals(value, found).ShouldBeTrue();
    }

    [Fact]
    public void GetUserdataOrNil_SuccessWithNil()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.GetUserdataOrNil<ValueUserdata>("missing").ShouldBeNull();
    }

    [Fact]
    public void GetUserdataOrNil_ErrorCase_ThrowsException()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("wrong", 1);

        Should.Throw<Exception>(() => table.GetUserdataOrNil<ValueUserdata>("wrong"));
    }
}
