using Shouldly;

namespace Darp.Luau.Tests;

public sealed class UserdataTests
{
    [Fact]
    public void Userdata_IndexSetterAndMethodCall_ShouldWork()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();
        CounterUserdata.LastMethodName = null;
        CounterUserdata.LastParameterCount = -1;

        state.Globals.Set("counter", counter);
        state.DoString(
            """
            before = counter.value
            counter.value = 41
            after = counter.value
            methodResult = getmetatable(counter).__namecall(counter, "add", 1)
            """
        );

        state.Globals.TryGet("before", out int before).ShouldBeTrue();
        before.ShouldBe(0);

        state.Globals.TryGet("after", out int after).ShouldBeTrue();
        after.ShouldBe(41);

        CounterUserdata.LastMethodName.ShouldBe("add");
        CounterUserdata.LastParameterCount.ShouldBe(1);

        state.Globals.TryGet("methodResult", out LuauValue methodResultValue).ShouldBeTrue();
        methodResultValue.Type.ShouldBe(LuauValueType.Number);
        methodResultValue.TryGet(out int methodResult).ShouldBeTrue();
        methodResult.ShouldBe(42);
    }

    [Fact]
    public void Userdata_ShouldRoundtripThroughLuauValue()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();

        using LuauUserdata userdata = state.CreateUserdata(counter);
        state.Globals.Set("userdata", userdata);

        state.Globals.TryGet("userdata", out LuauValue value).ShouldBeTrue();
        value.Type.ShouldBe(LuauValueType.Userdata);
        value.TryGet(out LuauUserdata roundtrip).ShouldBeTrue();

        using (roundtrip)
        {
            state.Globals.Set("userdataFromValue", value);
            state.DoString("isUserdataPresent = userdataFromValue ~= nil");
            state.Globals.TryGet("isUserdataPresent", out bool isUserdataPresent).ShouldBeTrue();
            isUserdataPresent.ShouldBeTrue();
        }
    }

    [Fact]
    public void Userdata_UnknownIndex_ShouldReturnNil()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString("missingValue = counter.missing");

        state.Globals.TryGet("missingValue", out LuauValue missingValue).ShouldBeTrue();
        missingValue.Type.ShouldBe(LuauValueType.Nil);
    }

    [Fact]
    public void Userdata_UnknownSet_ShouldRaiseLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        LuaException exception = Should.Throw<LuaException>(() => state.DoString("counter.missing = 5"));

        exception.Message.ShouldContain("unknown userdata member 'missing'");
    }

    [Fact]
    public void Userdata_UnknownSet_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              counter.missing = 5
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("unknown userdata member 'missing'");
    }

    [Fact]
    public void Userdata_UnknownMethod_ShouldRaiseLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        LuaException exception = Should.Throw<LuaException>(() =>
            state.DoString("result = getmetatable(counter).__namecall(counter, \"missingMethod\", 1)")
        );

        exception.Message.ShouldContain("unknown userdata method 'missingMethod'");
    }

    [Fact]
    public void Userdata_UnknownMethod_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              return getmetatable(counter).__namecall(counter, "missingMethod", 1)
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("unknown userdata method 'missingMethod'");
    }

    [Fact]
    public void Userdata_CallbackException_ShouldTranslateToLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        LuaException exception = Should.Throw<LuaException>(() => state.DoString("x = failing.explode"));

        exception.Message.ShouldContain("__index callback failed");
        exception.Message.ShouldContain("Boom from OnIndex");
    }

    [Fact]
    public void Userdata_CallbackException_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              return failing.explode
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("__index callback failed");
        err.ShouldContain("Boom from OnIndex");
    }

    private sealed class CounterUserdata : ILuauUserData<CounterUserdata>
    {
        public static string? LastMethodName { get; set; }
        public static int LastParameterCount { get; set; }

        public int Value { get; private set; }

        public static bool OnIndex(
            CounterUserdata self,
            in LuauState state,
            in ReadOnlySpan<char> fieldName,
            out IntoLuau value
        )
        {
            if (fieldName is "value")
            {
                value = self.Value;
                return true;
            }
            value = default;
            return false;
        }

        public static bool OnSetIndex(CounterUserdata self, in LuauView valueView, in ReadOnlySpan<char> fieldName)
        {
            if (fieldName is "value")
            {
                self.Value = (int)valueView.CheckNumber();
                return true;
            }
            return false;
        }

        public static bool OnMethodCall(
            CounterUserdata self,
            LuauFunctions functionArgs,
            in ReadOnlySpan<char> methodName
        )
        {
            LastMethodName = methodName.ToString();
            LastParameterCount = functionArgs.NumberOfParameters;

            if (methodName is "add")
            {
                ArgumentOutOfRangeException.ThrowIfNotEqual(functionArgs.NumberOfParameters, 1);
                int offset = (int)functionArgs.CheckNumber(1);
                functionArgs.ReturnParameter(self.Value + offset);
                return true;
            }

            return false;
        }

        public static implicit operator IntoLuau(CounterUserdata value) => IntoLuau.FromUserdata(value);
    }

    private sealed class FailingUserdata : ILuauUserData<FailingUserdata>
    {
        public static bool OnIndex(
            FailingUserdata self,
            in LuauState state,
            in ReadOnlySpan<char> fieldName,
            out IntoLuau value
        )
        {
            if (fieldName is "explode")
                throw new InvalidOperationException("Boom from OnIndex");

            value = default;
            return false;
        }

        public static bool OnSetIndex(FailingUserdata self, in LuauView valueView, in ReadOnlySpan<char> fieldName) =>
            false;

        public static bool OnMethodCall(
            FailingUserdata self,
            LuauFunctions functionArgs,
            in ReadOnlySpan<char> methodName
        ) => false;

        public static implicit operator IntoLuau(FailingUserdata value) => IntoLuau.FromUserdata(value);
    }
}
