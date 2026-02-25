using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class LuauArgsUserdataTests
{
    [Fact]
    public void Args_TryReadUserdata_ShouldResolveManagedInstance()
    {
        using var state = new LuauState();
        state.Globals.Set("input", new ValueUserdata { Value = 42 });
        state.Globals.Set(
            "f",
            state.CreateFunctionBuilder(static args =>
            {
                if (!args.TryReadUserdata(1, out ValueUserdata? value, out string? error))
                    return LuauReturn.Error(error);
                return LuauReturn.Ok(value.Value);
            })
        );

        state.DoString("result = f(input)");
        state.Globals.TryGet("result", out int result).ShouldBeTrue();
        result.ShouldBe(42);
    }

    [Fact]
    public void Args_TryReadUserdata_WhenTypeMismatches_ShouldFail()
    {
        using var state = new LuauState();
        state.Globals.Set("input", new OtherValueUserdata());
        state.Globals.Set(
            "f",
            state.CreateFunctionBuilder(static args =>
            {
                if (!args.TryReadUserdata<ValueUserdata>(1, out _, out string? error))
                    return LuauReturn.Error(error);
                return LuauReturn.Ok();
            })
        );

        state.DoString(
            """
            ok, err = pcall(function()
              f(input)
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();
        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("must be userdata of type");
    }

    [Fact]
    public void Args_TryReadUserdata_WhenValueIsNotUserdata_ShouldFail()
    {
        using var state = new LuauState();
        state.Globals.Set("input", 12);
        state.Globals.Set(
            "f",
            state.CreateFunctionBuilder(static args =>
            {
                if (!args.TryReadUserdata<ValueUserdata>(1, out _, out string? error))
                    return LuauReturn.Error(error);
                return LuauReturn.Ok();
            })
        );

        state.DoString(
            """
            ok, err = pcall(function()
              f(input)
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();
        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("LUA_TUSERDATA");
    }

    [Fact]
    public void Args_TryReadUserdataOrNil_ShouldAcceptNil()
    {
        using var state = new LuauState();
        state.Globals.Set(
            "f",
            state.CreateFunctionBuilder(static args =>
            {
                if (!args.TryReadUserdataOrNil(1, out ValueUserdata? value, out string? error))
                    return LuauReturn.Error(error);
                return LuauReturn.Ok(value is null ? "nil" : "value");
            })
        );

        state.DoString("result = f(nil)");
        state.Globals.TryGet("result", out string? result).ShouldBeTrue();
        result.ShouldBe("nil");
    }

    [Fact]
    public void Args_TryReadUserdataOrNil_ShouldAcceptUserdata()
    {
        using var state = new LuauState();
        state.Globals.Set("input", new ValueUserdata());
        state.Globals.Set(
            "f",
            state.CreateFunctionBuilder(static args =>
            {
                if (!args.TryReadUserdataOrNil(1, out ValueUserdata? value, out string? error))
                    return LuauReturn.Error(error);
                return LuauReturn.Ok(value is null ? "nil" : "value");
            })
        );

        state.DoString("result = f(input)");
        state.Globals.TryGet("result", out string? result).ShouldBeTrue();
        result.ShouldBe("value");
    }
}
