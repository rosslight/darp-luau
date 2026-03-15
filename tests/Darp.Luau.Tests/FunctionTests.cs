using Shouldly;

namespace Darp.Luau.Tests;

public sealed class FunctionTests : IDisposable
{
    private readonly LuauState _state = new();

    [Fact]
    public void Simple()
    {
        _state.DoString(
            """
            function get_value(message: string)
              return message;
            end
            """
        );
        _ = _state.Globals.TryGet("get_value", out LuauFunction func);
        using (func)
        {
            string r = func.Invoke<string>("Message");
            r.ShouldBe("Message");
        }
    }

    [Fact]
    public void CSharpFunction_Adder_ShouldBeCalled()
    {
        using LuauFunction func = _state.CreateFunction(Add);
        _state.Globals.Set("add", func);

        _state.DoString("result = add(1, 2)");
        _state.Globals.TryGet("result", out int result).ShouldBeTrue();

        result.ShouldBe(3);
        return;

        int Add(int p1, int p2) => p1 + p2;
    }

    [Fact]
    public void CSharpFunction_Exception_ShouldBeCatchableByPCall()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ =>
            throw new InvalidOperationException("Boom from managed function")
        );
        _state.Globals.Set("explode", func);

        _state.DoString("ok, err = pcall(explode)");

        _state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        _state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("managed function callback failed");
        err.ShouldContain("Boom from managed function");
    }

    [Fact]
    public void CSharpFunction_Exception_ShouldBeCatchable()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ =>
            throw new InvalidOperationException("Boom from managed function")
        );
        _state.Globals.Set("explode", func);

        LuaException exception = Should.Throw<LuaException>(() => _state.DoString("explode();"));
        exception.Message.ShouldContain("managed function callback failed");
        exception.Message.ShouldContain("Boom from managed function");
    }

    [Fact]
    public void CSharpFunction_ResultObject_ShouldReturnValue()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadNumber(1, out int a, out string? error) || !args.TryReadNumber(2, out int b, out error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(a + b);
        });
        _state.Globals.Set("add", func);

        _state.DoString("result = add(1, 2)");
        _state.Globals.TryGet("result", out int result).ShouldBeTrue();
        result.ShouldBe(3);
    }

    [Fact]
    public void CSharpFunction_ResultObject_ShouldReturnErrorString()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ => LuauReturn.Error("user facing error"));
        _state.Globals.Set("fail", func);

        _state.DoString("ok, err = pcall(fail)");

        _state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        _state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("user facing error");
    }

    [Fact]
    public void CSharpFunction_ResultObject_ShouldReturnStringValueViaOk()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ => LuauReturn.Ok("hello from csharp"));
        _state.Globals.Set("greet", func);

        _state.DoString("result = greet()");

        _state.Globals.TryGet("result", out string? result).ShouldBeTrue();
        result.ShouldBe("hello from csharp");
    }

    [Fact]
    public void CSharpFunction_ResultObject_ImplicitString_ShouldBeAnError()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ => LuauReturn.Error("error from callback"));
        _state.Globals.Set("fail", func);

        _state.DoString("ok, err = pcall(fail)");

        _state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        _state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("error from callback");
    }

    [Fact]
    public void CSharpFunction_ResultObject_ShouldSupportNoReturnValues()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ => LuauReturn.Ok());
        _state.Globals.Set("touch", func);

        _state.DoString("returnCount = select('#', touch())");

        _state.Globals.TryGet("returnCount", out int returnCount).ShouldBeTrue();
        returnCount.ShouldBe(0);
    }

    [Fact]
    public void CSharpFunction_ResultObject_ShouldSupportMultipleReturnValues()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static _ => LuauReturn.Ok(10, 11));
        _state.Globals.Set("pair", func);

        _state.DoString(
            """
            first, second = pair()
            returnCount = select('#', pair())
            """
        );

        _state.Globals.TryGet("first", out int first).ShouldBeTrue();
        first.ShouldBe(10);

        _state.Globals.TryGet("second", out int second).ShouldBeTrue();
        second.ShouldBe(11);

        _state.Globals.TryGet("returnCount", out int returnCount).ShouldBeTrue();
        returnCount.ShouldBe(2);
    }

    [Fact]
    public void Func_NoArgs_Returns()
    {
        using LuauFunction f1 = _state.CreateFunction(() => true);
        using LuauFunction f2 = _state.CreateFunction(() => (sbyte)1);
        using LuauFunction f3 = _state.CreateFunction(() => (byte)1);
        using LuauFunction f4 = _state.CreateFunction(() => (short)1);
        using LuauFunction f5 = _state.CreateFunction(() => (ushort)1);
        using LuauFunction f6 = _state.CreateFunction(() => 1);
        using LuauFunction f7 = _state.CreateFunction(() => (uint)1);
        using LuauFunction f8 = _state.CreateFunction(() => (long)1);
        using LuauFunction f9 = _state.CreateFunction(() => (ulong)1);
        using LuauFunction f10 = _state.CreateFunction(() => (Int128)1);
        using LuauFunction f11 = _state.CreateFunction(() => (UInt128)1);
        using LuauFunction f12 = _state.CreateFunction(() => "1");
        using LuauFunction f13 = _state.CreateFunction(() => (Half)1);
        using LuauFunction f14 = _state.CreateFunction(() => (float)1);
        using LuauFunction f15 = _state.CreateFunction(() => (double)1);
        using LuauFunction f16 = _state.CreateFunction(() => (decimal)1);

        f1.Invoke<bool>().ShouldBe(true);
        f2.Invoke<sbyte>().ShouldBe((sbyte)1);
        f3.Invoke<byte>().ShouldBe((byte)1);
        f4.Invoke<short>().ShouldBe((short)1);
        f5.Invoke<ushort>().ShouldBe((ushort)1);
        f6.Invoke<int>().ShouldBe(1);
        f7.Invoke<uint>().ShouldBe((uint)1);
        f8.Invoke<long>().ShouldBe(1);
        f9.Invoke<ulong>().ShouldBe((ulong)1);
        f10.Invoke<Int128>().ShouldBe(1);
        f11.Invoke<UInt128>().ShouldBe((UInt128)1);
        f12.Invoke<string>().ShouldBe("1");
        f13.Invoke<Half>().ShouldBe((Half)1);
        f14.Invoke<float>().ShouldBe(1);
        f15.Invoke<double>().ShouldBe(1);
        f16.Invoke<decimal>().ShouldBe(1);
    }

    [Fact]
    public void Func_OneArg_Returns()
    {
        using LuauFunction f1 = _state.CreateFunction((bool x) => x);
        using LuauFunction f2 = _state.CreateFunction((sbyte x) => x);
        using LuauFunction f3 = _state.CreateFunction((byte x) => x);
        using LuauFunction f4 = _state.CreateFunction((short x) => x);
        using LuauFunction f5 = _state.CreateFunction((ushort x) => x);
        using LuauFunction f6 = _state.CreateFunction((int x) => x);
        using LuauFunction f7 = _state.CreateFunction((uint x) => x);
        using LuauFunction f8 = _state.CreateFunction((long x) => x);
        using LuauFunction f9 = _state.CreateFunction((ulong x) => x);
        using LuauFunction f10 = _state.CreateFunction((Int128 x) => x);
        using LuauFunction f11 = _state.CreateFunction((UInt128 x) => x);
        using LuauFunction f12 = _state.CreateFunction((string x) => x);
        using LuauFunction f13 = _state.CreateFunction((Half x) => x);
        using LuauFunction f14 = _state.CreateFunction((float x) => x);
        using LuauFunction f15 = _state.CreateFunction((double x) => x);
        using LuauFunction f16 = _state.CreateFunction((decimal x) => x);

        f1.Invoke<bool>(true).ShouldBe(true);
        f2.Invoke<sbyte>(1).ShouldBe((sbyte)1);
        f3.Invoke<byte>(1).ShouldBe((byte)1);
        f4.Invoke<short>(1).ShouldBe((short)1);
        f5.Invoke<ushort>(1).ShouldBe((ushort)1);
        f6.Invoke<int>(1).ShouldBe(1);
        f7.Invoke<uint>(1).ShouldBe((uint)1);
        f8.Invoke<long>(1).ShouldBe(1);
        f9.Invoke<ulong>(1).ShouldBe((ulong)1);
        f10.Invoke<Int128>(1).ShouldBe(1);
        f11.Invoke<UInt128>(1).ShouldBe((UInt128)1);
        f12.Invoke<string>("1").ShouldBe("1");
        f13.Invoke<Half>(1).ShouldBe((Half)1);
        f14.Invoke<float>(1).ShouldBe(1);
        f15.Invoke<double>(1).ShouldBe(1);
        f16.Invoke<decimal>(1).ShouldBe(1);
    }

    [Fact]
    public void Func_OneArgNullable_Returns()
    {
        using LuauFunction f1 = _state.CreateFunction((bool? x) => x);
        using LuauFunction f2 = _state.CreateFunction((sbyte? x) => x);
        using LuauFunction f3 = _state.CreateFunction((byte? x) => x);
        using LuauFunction f4 = _state.CreateFunction((short? x) => x);
        using LuauFunction f5 = _state.CreateFunction((ushort? x) => x);
        using LuauFunction f6 = _state.CreateFunction((int? x) => x);
        using LuauFunction f7 = _state.CreateFunction((uint? x) => x);
        using LuauFunction f8 = _state.CreateFunction((long? x) => x);
        using LuauFunction f9 = _state.CreateFunction((ulong? x) => x);
        using LuauFunction f10 = _state.CreateFunction((Int128? x) => x);
        using LuauFunction f11 = _state.CreateFunction((UInt128? x) => x);
        using LuauFunction f12 = _state.CreateFunction((string? x) => x);
        using LuauFunction f13 = _state.CreateFunction((Half? x) => x);
        using LuauFunction f14 = _state.CreateFunction((float? x) => x);
        using LuauFunction f15 = _state.CreateFunction((double? x) => x);
        using LuauFunction f16 = _state.CreateFunction((decimal? x) => x);

        f1.Invoke<bool?>((bool?)null).ShouldBe(null);
        f2.Invoke<sbyte?>((sbyte?)null).ShouldBe(null);
        f3.Invoke<byte?>((byte?)null).ShouldBe(null);
        f4.Invoke<short?>((short?)null).ShouldBe(null);
        f5.Invoke<ushort?>((ushort?)null).ShouldBe(null);
        f6.Invoke<int?>((int?)null).ShouldBe(null);
        f7.Invoke<uint?>((uint?)null).ShouldBe(null);
        f8.Invoke<long?>((long?)null).ShouldBe(null);
        f9.Invoke<ulong?>((ulong?)null).ShouldBe(null);
        f10.Invoke<Int128?>((long?)null).ShouldBe(null);
        f11.Invoke<UInt128?>((ulong?)null).ShouldBe(null);
        f12.Invoke<string?>((string?)null).ShouldBe(null);
        f13.Invoke<Half?>((Half?)null).ShouldBe(null);
        f14.Invoke<float?>((float?)null).ShouldBe(null);
        f15.Invoke<double?>((double?)null).ShouldBe(null);
        f16.Invoke<decimal?>((double?)null).ShouldBe(null);
    }

    [Fact]
    public void Func_NoArgs_Returns_ULong()
    {
        const ulong expectedValue = 1;

        using LuauFunction func = _state.CreateFunction(() => expectedValue);
        _state.Globals.Set("f", func);
        _state.DoString("result = f()");
        _state.Globals.TryGet("result", out ulong result).ShouldBeTrue();

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_NoArgs_Returns_Nil_Bool()
    {
        bool? expectedValue = null;

        using LuauFunction func = _state.CreateFunction(() => expectedValue);
        _state.Globals.Set("f", func);
        _state.DoString("result = f()");
        _state.Globals.TryGet("result", out bool? result).ShouldBeFalse();

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_NoArgs_Returns_Int()
    {
        const int expectedValue = 1;

        using LuauFunction func = _state.CreateFunction(() => expectedValue);
        _state.Globals.Set("f", func);
        _state.DoString("result = f()");
        _state.Globals.TryGet("result", out int result).ShouldBeTrue();

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_BufferArg_ReturnsBuffer()
    {
        const string expectedValue = "010203";

        using LuauBuffer buffer = _state.CreateBuffer(Convert.FromHexString(expectedValue));
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadLuauBuffer(1, out LuauBufferView b, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(b);
        });

        _state.Globals.Set("input", buffer);
        _state.Globals.Set("f", func);
        _state.DoString("result = f(input)");
        _state.Globals.TryGet("result", out LuauBuffer bufferResult).ShouldBeTrue();

        using (bufferResult)
        {
            bufferResult.TryGet(out byte[] bufferBytes);
            Convert.ToHexString(bufferBytes).ShouldBe(expectedValue);
        }
    }

    [Fact]
    public void Func_StringArg_ReturnsBuffer()
    {
        byte[] expected = [0x01, 0x02, 0x03];
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadUtf8String(1, out string? hex, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(Convert.FromHexString(hex));
        });

        _state.Globals.Set("input", Convert.ToHexString(expected));
        _state.Globals.Set("f", func);
        _state.DoString("result = f(input)");
        _state.Globals.TryGet("result", out ReadOnlySpan<byte> result).ShouldBeTrue();
        result.ToArray().ShouldBe<byte>(expected);
    }

    [Fact]
    public void Func_StringArg_ShouldReadLuauStringViewAndReturnSameValue()
    {
        const string expectedValue = "hello from luau";

        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadLuauString(1, out LuauStringView value, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(value);
        });
        _state.Globals.Set("input", expectedValue);
        _state.Globals.Set("f", func);

        _state.DoString("result = f(input)");
        _state.Globals.TryGet("result", out string? result).ShouldBeTrue();
        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void Func_UserdataArg_ShouldReadManagedUserdata()
    {
        var input = new ArgsUserdataA { Value = 42 };
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadUserdata(1, out ArgsUserdataA? value, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(value.Value);
        });
        _state.Globals.Set("input", input);
        _state.Globals.Set("f", func);

        _state.DoString("result = f(input)");
        _state.Globals.TryGet("result", out int result).ShouldBeTrue();
        result.ShouldBe(42);
    }

    [Fact]
    public void Func_UserdataArg_WrongType_ShouldReturnError()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadUserdata<ArgsUserdataA>(1, out _, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok();
        });
        _state.Globals.Set("input", new ArgsUserdataB());
        _state.Globals.Set("f", func);

        _state.DoString(
            """
            ok, err = pcall(function()
              f(input)
            end)
            """
        );

        _state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();
        _state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("must be userdata of type");
    }

    [Fact]
    public void Func_UserdataArgOrNil_ShouldHandleNil()
    {
        using var func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadUserdataOrNil(1, out ArgsUserdataA? value, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(value is null ? "nil" : "value");
        });
        _state.Globals.Set("f", func);

        _state.DoString("fromNil = f(nil)");

        _state.Globals.Set("input", new ArgsUserdataA());
        _state.DoString("fromUserdata = f(input)");

        _state.Globals.TryGet("fromNil", out string? fromNil).ShouldBeTrue();
        fromNil.ShouldBe("nil");
        _state.Globals.TryGet("fromUserdata", out string? fromUserdata).ShouldBeTrue();
        fromUserdata.ShouldBe("value");
    }

    [Fact]
    public void Func_UserdataArg_ShouldReadLuauUserdataReference()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadLuauUserdata(1, out LuauUserdataView value, out string? error))
                return LuauReturn.Error(error);

            return LuauReturn.Ok(value);
        });
        _state.Globals.Set("input", new ArgsUserdataA());
        _state.Globals.Set("f", func);

        _state.DoString("isSame = f(input) == input");
        _state.Globals.TryGet("isSame", out bool isSame).ShouldBeTrue();
        isSame.ShouldBeTrue();
    }

    [Fact]
    public void LuauFunction_Invoke_ReturningTable_ShouldReturnUsableTable()
    {
        _state.DoString(
            """
            function mk_table()
              return { value = 7 }
            end
            """
        );

        _state.Globals.TryGet("mk_table", out LuauFunction mkTable).ShouldBeTrue();
        using (mkTable)
        {
            using LuauTable table = mkTable.Invoke<LuauTable>();
            table.GetNumber("value").ShouldBe(7);
        }
    }

    [Fact]
    public void LuauFunction_Invoke_ReturningTable_ShouldKeepReferenceTrackedUntilCallerDisposes()
    {
        _state.DoString(
            """
            function mk_table()
              return { value = 11 }
            end
            """
        );

        _state.Globals.TryGet("mk_table", out LuauFunction mkTable).ShouldBeTrue();
        using (mkTable)
        {
            ulong baselineActiveReferences = _state.MemoryStatistics.ActiveRegistryReferences;

            using (LuauTable table = mkTable.Invoke<LuauTable>())
            {
                _state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences + 1);
                table.GetNumber("value").ShouldBe(11);
            }

            _state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);
        }
    }

    [Fact]
    public void LuauFunctionView_Invoke_ReturningTable_ShouldWorkInsideManagedCallback()
    {
        _state.DoString(
            """
            function make_table()
              return { value = 99 }
            end
            """
        );

        using LuauFunction callAndRead = _state.CreateFunctionBuilder(static args =>
        {
            if (!args.TryReadLuauFunction(1, out LuauFunctionView function, out string? error))
                return LuauReturn.Error(error);

            using LuauTable table = function.Invoke<LuauTable>();
            if (!table.TryGet("value", out int value))
                return LuauReturn.Error("Expected returned table to contain number key 'value'.");

            return LuauReturn.Ok(value);
        });

        _state.Globals.Set("call_and_read", callAndRead);
        _state.DoString("result = call_and_read(make_table)");

        _state.Globals.GetNumber("result").ShouldBe(99);
    }

    [Fact]
    public void ReturningTable_ShouldWorkInsideManagedCallback()
    {
        using LuauFunction func = _state.CreateFunctionBuilder(args =>
        {
            if (!args.TryValidateArgumentCount(0, out string? error))
                return LuauReturn.Error(error);
            using LuauTable x = _state.CreateTable();
            x.Set("value", 99);
            return LuauReturn.Ok(x);
        });

        _state.Globals.Set("make_table", func);
        _state.DoString("result = make_table().value");

        _state.Globals.GetNumber("result").ShouldBe(99);
    }

    [Fact]
    public void ReturningTable_ShouldWorkInsideManagedCallback2()
    {
        using LuauTable x = _state.CreateTable();
        x.Set("value", 99);

        using LuauFunction func = _state.CreateFunctionBuilder(args =>
        {
            if (!args.TryValidateArgumentCount(0, out var error))
                return LuauReturn.Error(error);
            return LuauReturn.Ok(x);
        });

        _state.Globals.Set("make_table", func);
        _state.DoString("result = make_table().value");

        _state.Globals.GetNumber("result").ShouldBe(99);
        x.GetNumber("value").ShouldBe(99);
    }

    public void Dispose()
    {
        _state.MemoryStatistics.ActiveRegistryReferences.ShouldBe((ulong)2);
        _state.Dispose();
    }
}

internal sealed class ArgsUserdataA : ILuauUserData<ArgsUserdataA>
{
    public int Value { get; set; }

    public static LuauReturnSingle OnIndex(ArgsUserdataA self, in LuauState state, in ReadOnlySpan<char> fieldName) =>
        LuauReturnSingle.NotHandled;

    public static LuauOutcome OnSetIndex(ArgsUserdataA self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName) =>
        LuauOutcome.NotHandledError;

    public static LuauReturn OnMethodCall(
        ArgsUserdataA self,
        LuauArgs functionArgs,
        in ReadOnlySpan<char> methodName
    ) => LuauReturn.NotHandledError;

    public static implicit operator IntoLuau(ArgsUserdataA value) => IntoLuau.FromUserdata(value);
}

internal sealed class ArgsUserdataB : ILuauUserData<ArgsUserdataB>
{
    public static LuauReturnSingle OnIndex(ArgsUserdataB self, in LuauState state, in ReadOnlySpan<char> fieldName) =>
        LuauReturnSingle.NotHandled;

    public static LuauOutcome OnSetIndex(ArgsUserdataB self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName) =>
        LuauOutcome.NotHandledError;

    public static LuauReturn OnMethodCall(
        ArgsUserdataB self,
        LuauArgs functionArgs,
        in ReadOnlySpan<char> methodName
    ) => LuauReturn.NotHandledError;

    public static implicit operator IntoLuau(ArgsUserdataB value) => IntoLuau.FromUserdata(value);
}
