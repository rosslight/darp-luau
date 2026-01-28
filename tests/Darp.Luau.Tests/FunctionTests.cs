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
            function get_value()
              return "Hello";
            end
            """
        );
        _ = state.Globals.TryGet("get_value", out LuauFunction func);
        string r = func.Call<string>();
        r.ShouldBe("Hello");
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

        lua.Globals.Set("f1", lua.CreateFunction(() => true));
        lua.Globals.Set("f2", lua.CreateFunction(() => (sbyte)1));
        lua.Globals.Set("f3", lua.CreateFunction(() => (byte)1));
        lua.Globals.Set("f4", lua.CreateFunction(() => (short)1));
        lua.Globals.Set("f5", lua.CreateFunction(() => (ushort)1));
        lua.Globals.Set("f6", lua.CreateFunction(() => (int)1));
        lua.Globals.Set("f7", lua.CreateFunction(() => (uint)1));
        lua.Globals.Set("f8", lua.CreateFunction(() => (long)1));
        lua.Globals.Set("f9", lua.CreateFunction(() => (ulong)1));
        lua.Globals.Set("f10", lua.CreateFunction(() => (Int128)1));
        lua.Globals.Set("f11", lua.CreateFunction(() => (UInt128)1));
        lua.Globals.Set("f12", lua.CreateFunction(() => "1"));
        lua.Globals.Set("f13", lua.CreateFunction(() => (Half)1));
        lua.Globals.Set("f14", lua.CreateFunction(() => (float)1));
        lua.Globals.Set("f15", lua.CreateFunction(() => (double)1));
        lua.Globals.Set("f16", lua.CreateFunction(() => (decimal)1));
        lua.DoString(
            """
            r1 = f1()
            r2 = f2()
            r3 = f3()
            r4 = f4()
            r5 = f5()
            r6 = f6()
            r7 = f7()
            r8 = f8()
            r9 = f9()
            r10 = f10()
            r11 = f11()
            r12 = f12()
            r13 = f13()
            r14 = f14()
            r15 = f15()
            r16 = f16()
            """
        );
        lua.Globals.TryGet("r1", out bool r1).ShouldBeTrue();
        lua.Globals.TryGet("r2", out sbyte r2).ShouldBeTrue();
        lua.Globals.TryGet("r3", out byte r3).ShouldBeTrue();
        lua.Globals.TryGet("r4", out short r4).ShouldBeTrue();
        lua.Globals.TryGet("r5", out ushort r5).ShouldBeTrue();
        lua.Globals.TryGet("r6", out int r6).ShouldBeTrue();
        lua.Globals.TryGet("r7", out uint r7).ShouldBeTrue();
        lua.Globals.TryGet("r8", out long r8).ShouldBeTrue();
        lua.Globals.TryGet("r9", out ulong r9).ShouldBeTrue();
        lua.Globals.TryGet("r10", out Int128 r10).ShouldBeTrue();
        lua.Globals.TryGet("r11", out UInt128 r11).ShouldBeTrue();
        lua.Globals.TryGet("r12", out string? r12).ShouldBeTrue();
        lua.Globals.TryGet("r13", out Half r13).ShouldBeTrue();
        lua.Globals.TryGet("r14", out float r14).ShouldBeTrue();
        lua.Globals.TryGet("r15", out double r15).ShouldBeTrue();
        lua.Globals.TryGet("r16", out decimal r16).ShouldBeTrue();
        r1.ShouldBe(true);
        r2.ShouldBe((sbyte)1);
        r3.ShouldBe((byte)1);
        r4.ShouldBe((short)1);
        r5.ShouldBe((ushort)1);
        r6.ShouldBe((int)1);
        r7.ShouldBe((uint)1);
        r8.ShouldBe((long)1);
        r9.ShouldBe((ulong)1);
        r10.ShouldBe((Int128)1);
        r11.ShouldBe((UInt128)1);
        r12.ShouldBe("1");
        r13.ShouldBe((Half)1);
        r14.ShouldBe((float)1);
        r15.ShouldBe((double)1);
        r16.ShouldBe((decimal)1);
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

    /*
    [Fact]
    public void Func_NoArgs_Returns_Nil_Bool()
    {
        bool? expectedValue = null;
        using var lua = new LuauState();

        LuauFunction func = lua.CreateFunction(() => expectedValue);
        lua.Globals.Set("f", func);
        lua.DoString("result = f()");
        lua.Globals.TryGet("result", out bool? result).ShouldBeTrue();

        result.ShouldBe(expectedValue);
    }
*/
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
