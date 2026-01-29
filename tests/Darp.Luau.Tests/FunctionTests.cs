using Shouldly;

namespace Darp.Luau.Tests;

public sealed class FunctionTests
{
    [Fact]
    public void Simple()
    {
        using var state = new LuauState();
        state.DoString(
            """
            function get_value(message: string)
              return message;
            end
            """
        );
        _ = state.Globals.TryGet("get_value", out LuauFunction func);
        string r = func.Call<string>("Message");
        r.ShouldBe("Message");
    }

    [Fact]
    public void CSharpFunction_Adder_ShouldBeCalled()
    {
        using var state = new LuauState();
        LuauFunction func = state.CreateFunction(Add);
        state.Globals.Set("add", func);

        state.DoString(
            """
            result = add(1, 2)
            """
        );
        state.Globals.TryGet("result", out int result).ShouldBeTrue();

        result.ShouldBe(3);
        return;

        int Add(int p1, int p2) => p1 + p2;
    }

    [Fact]
    public void Func_NoArgs_Returns()
    {
        using var lua = new LuauState();

        LuauFunction f1 = lua.CreateFunction(() => true);
        LuauFunction f2 = lua.CreateFunction(() => (sbyte)1);
        LuauFunction f3 = lua.CreateFunction(() => (byte)1);
        LuauFunction f4 = lua.CreateFunction(() => (short)1);
        LuauFunction f5 = lua.CreateFunction(() => (ushort)1);
        LuauFunction f6 = lua.CreateFunction(() => 1);
        LuauFunction f7 = lua.CreateFunction(() => (uint)1);
        LuauFunction f8 = lua.CreateFunction(() => (long)1);
        LuauFunction f9 = lua.CreateFunction(() => (ulong)1);
        LuauFunction f10 = lua.CreateFunction(() => (Int128)1);
        LuauFunction f11 = lua.CreateFunction(() => (UInt128)1);
        LuauFunction f12 = lua.CreateFunction(() => "1");
        LuauFunction f13 = lua.CreateFunction(() => (Half)1);
        LuauFunction f14 = lua.CreateFunction(() => (float)1);
        LuauFunction f15 = lua.CreateFunction(() => (double)1);
        LuauFunction f16 = lua.CreateFunction(() => (decimal)1);

        f1.Call<bool>().ShouldBe(true);
        f2.Call<sbyte>().ShouldBe((sbyte)1);
        f3.Call<byte>().ShouldBe((byte)1);
        f4.Call<short>().ShouldBe((short)1);
        f5.Call<ushort>().ShouldBe((ushort)1);
        f6.Call<int>().ShouldBe(1);
        f7.Call<uint>().ShouldBe((uint)1);
        f8.Call<long>().ShouldBe(1);
        f9.Call<ulong>().ShouldBe((ulong)1);
        f10.Call<Int128>().ShouldBe(1);
        f11.Call<UInt128>().ShouldBe((UInt128)1);
        f12.Call<string>().ShouldBe("1");
        f13.Call<Half>().ShouldBe((Half)1);
        f14.Call<float>().ShouldBe(1);
        f15.Call<double>().ShouldBe(1);
        f16.Call<decimal>().ShouldBe(1);
    }

    [Fact]
    public void Func_OneArg_Returns()
    {
        using var lua = new LuauState();

        LuauFunction f1 = lua.CreateFunction((bool x) => x);
        LuauFunction f2 = lua.CreateFunction((sbyte x) => x);
        LuauFunction f3 = lua.CreateFunction((byte x) => x);
        LuauFunction f4 = lua.CreateFunction((short x) => x);
        LuauFunction f5 = lua.CreateFunction((ushort x) => x);
        LuauFunction f6 = lua.CreateFunction((int x) => x);
        LuauFunction f7 = lua.CreateFunction((uint x) => x);
        LuauFunction f8 = lua.CreateFunction((long x) => x);
        LuauFunction f9 = lua.CreateFunction((ulong x) => x);
        LuauFunction f10 = lua.CreateFunction((Int128 x) => x);
        LuauFunction f11 = lua.CreateFunction((UInt128 x) => x);
        LuauFunction f12 = lua.CreateFunction((string x) => x);
        LuauFunction f13 = lua.CreateFunction((Half x) => x);
        LuauFunction f14 = lua.CreateFunction((float x) => x);
        LuauFunction f15 = lua.CreateFunction((double x) => x);
        LuauFunction f16 = lua.CreateFunction((decimal x) => x);

        f1.Call<bool>(true).ShouldBe(true);
        f2.Call<sbyte>(1).ShouldBe((sbyte)1);
        f3.Call<byte>(1).ShouldBe((byte)1);
        f4.Call<short>(1).ShouldBe((short)1);
        f5.Call<ushort>(1).ShouldBe((ushort)1);
        f6.Call<int>(1).ShouldBe(1);
        f7.Call<uint>(1).ShouldBe((uint)1);
        f8.Call<long>(1).ShouldBe(1);
        f9.Call<ulong>(1).ShouldBe((ulong)1);
        f10.Call<Int128>(1).ShouldBe(1);
        f11.Call<UInt128>(1).ShouldBe((UInt128)1);
        f12.Call<string>("1").ShouldBe("1");
        f13.Call<Half>(1).ShouldBe((Half)1);
        f14.Call<float>(1).ShouldBe(1);
        f15.Call<double>(1).ShouldBe(1);
        f16.Call<decimal>(1).ShouldBe(1);
    }

    [Fact]
    public void Func_OneArgNullable_Returns()
    {
        using var lua = new LuauState();

        LuauFunction f1 = lua.CreateFunction((bool? x) => x);
        LuauFunction f2 = lua.CreateFunction((sbyte? x) => x);
        LuauFunction f3 = lua.CreateFunction((byte? x) => x);
        LuauFunction f4 = lua.CreateFunction((short? x) => x);
        LuauFunction f5 = lua.CreateFunction((ushort? x) => x);
        LuauFunction f6 = lua.CreateFunction((int? x) => x);
        LuauFunction f7 = lua.CreateFunction((uint? x) => x);
        LuauFunction f8 = lua.CreateFunction((long? x) => x);
        LuauFunction f9 = lua.CreateFunction((ulong? x) => x);
        LuauFunction f10 = lua.CreateFunction((Int128? x) => x);
        LuauFunction f11 = lua.CreateFunction((UInt128? x) => x);
        LuauFunction f12 = lua.CreateFunction((string? x) => x);
        LuauFunction f13 = lua.CreateFunction((Half? x) => x);
        LuauFunction f14 = lua.CreateFunction((float? x) => x);
        LuauFunction f15 = lua.CreateFunction((double? x) => x);
        LuauFunction f16 = lua.CreateFunction((decimal? x) => x);

        f1.Call<bool?>((bool?)null).ShouldBe(null);
        f2.Call<sbyte?>((sbyte?)null).ShouldBe(null);
        f3.Call<byte?>((byte?)null).ShouldBe(null);
        f4.Call<short?>((short?)null).ShouldBe(null);
        f5.Call<ushort?>((ushort?)null).ShouldBe(null);
        f6.Call<int?>((int?)null).ShouldBe(null);
        f7.Call<uint?>((uint?)null).ShouldBe(null);
        f8.Call<long?>((long?)null).ShouldBe(null);
        f9.Call<ulong?>((ulong?)null).ShouldBe(null);
        f10.Call<Int128?>((long?)null).ShouldBe(null);
        f11.Call<UInt128?>((ulong?)null).ShouldBe(null);
        f12.Call<string?>((string?)null).ShouldBe(null);
        f13.Call<Half?>((Half?)null).ShouldBe(null);
        f14.Call<float?>((float?)null).ShouldBe(null);
        f15.Call<double?>((double?)null).ShouldBe(null);
        f16.Call<decimal?>((double?)null).ShouldBe(null);
    }

    [Fact]
    public void Func_NoArgs_Returns_ULong()
    {
        const ulong expectedValue = 1;
        using var lua = new LuauState();

        LuauFunction func = lua.CreateFunction(() => expectedValue);
        lua.Globals.Set("f", func);
        lua.DoString("result = f()");
        lua.Globals.TryGet("result", out ulong result).ShouldBeTrue();

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_NoArgs_Returns_Nil_Bool()
    {
        bool? expectedValue = null;
        using var lua = new LuauState();

        LuauFunction func = lua.CreateFunction(() => expectedValue);
        lua.Globals.Set("f", func);
        lua.DoString("result = f()");
        lua.Globals.TryGet("result", out bool? result).ShouldBeFalse();

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_NoArgs_Returns_Int()
    {
        const int expectedValue = 1;
        using var lua = new LuauState();

        LuauFunction func = lua.CreateFunction(() => expectedValue);
        lua.Globals.Set("f", func);
        lua.DoString("result = f()");
        lua.Globals.TryGet("result", out int result).ShouldBeTrue();

        result.ShouldBe(expectedValue);
    }
}
