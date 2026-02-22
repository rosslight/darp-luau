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
    public void Userdata_MethodCallViaColonSyntax_ShouldWork()
    {
        using var state = new LuauState();
        CounterUserdata.LastMethodName = null;
        CounterUserdata.LastParameterCount = -1;
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            counter.value = 41
            methodResult = counter:add(1)
            """
        );

        state.Globals.TryGet("methodResult", out int methodResult).ShouldBeTrue();
        methodResult.ShouldBe(42);
        CounterUserdata.LastMethodName.ShouldBe("add");
        CounterUserdata.LastParameterCount.ShouldBe(1);
    }

    [Fact]
    public void Userdata_ShouldRoundtripThroughLuauValue()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();

        using LuauUserdata userdata = state.GetOrCreateUserdata(counter);
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
    public void Userdata_GetOrCreate_WithSameManagedInstance_ShouldReuseLuaIdentity()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();

        using LuauUserdata first = state.GetOrCreateUserdata(counter);
        using LuauUserdata second = state.GetOrCreateUserdata(counter);

        state.Globals.Set("first", first);
        state.Globals.Set("second", second);
        state.DoString("sameIdentity = first == second");

        state.Globals.TryGet("sameIdentity", out bool sameIdentity).ShouldBeTrue();
        sameIdentity.ShouldBeTrue();
    }

    [Fact]
    public void Userdata_LuauUserdataTryGetManaged_ShouldResolveManagedInstance()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();

        using LuauUserdata userdata = state.GetOrCreateUserdata(counter);
        userdata.TryGetManaged(out CounterUserdata? resolved, out string? error).ShouldBeTrue(error);

        ReferenceEquals(counter, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Userdata_TableTryGetUserdata_ShouldResolveManagedInstance()
    {
        using var state = new LuauState();
        var counter = new CounterUserdata();
        LuauTable table = state.CreateTable();

        table.Set("counter", counter);

        table.TryGetUserdata("counter", out CounterUserdata? resolved, out string? error).ShouldBeTrue(error);
        ReferenceEquals(counter, resolved).ShouldBeTrue();

        table.TryGetLuauUserdata("counter", out LuauUserdata reference, out error).ShouldBeTrue(error);
        using (reference)
        {
            reference.TryGetManaged(out CounterUserdata? resolvedFromReference, out error).ShouldBeTrue(error);
            ReferenceEquals(counter, resolvedFromReference).ShouldBeTrue();
        }
    }

    [Fact]
    public void Userdata_TableTryGetUserdata_WithWrongManagedType_ShouldFail()
    {
        using var state = new LuauState();
        LuauTable table = state.CreateTable();
        table.Set("failing", new FailingUserdata());

        table.TryGetUserdata<CounterUserdata>("failing", out _, out string? error).ShouldBeFalse();
        error.ShouldContain("must be userdata of type");
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
    public void Userdata_NonStringIndexKey_ShouldRaiseLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        LuaException exception = Should.Throw<LuaException>(() => state.DoString("x = counter[1]"));

        exception.Message.ShouldContain("userdata index access requires a string member name");
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
    public void Userdata_NonStringSetKey_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              counter[1] = 5
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("userdata assignment requires a string member name");
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
    public void Userdata_NonStringMethodName_ShouldRaiseLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        LuaException exception = Should.Throw<LuaException>(() =>
            state.DoString("x = getmetatable(counter).__namecall(counter, 1, 1)")
        );

        exception.Message.ShouldContain("userdata method call requires a string method name");
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

    [Fact]
    public void Userdata_IndexErrorResult_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              return failing.errorIndex
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("error from OnIndex");
        err.ShouldNotContain("__index callback failed");
    }

    [Fact]
    public void Userdata_SetterCallbackException_ShouldTranslateToLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        LuaException exception = Should.Throw<LuaException>(() => state.DoString("failing.explodeSet = 1"));

        exception.Message.ShouldContain("__newindex callback failed");
        exception.Message.ShouldContain("Boom from OnSetIndex");
    }

    [Fact]
    public void Userdata_SetterCallbackException_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              failing.explodeSet = 1
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("__newindex callback failed");
        err.ShouldContain("Boom from OnSetIndex");
    }

    [Fact]
    public void Userdata_SetterErrorResult_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              failing.errorSet = 1
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("error from OnSetIndex");
        err.ShouldNotContain("__newindex callback failed");
    }

    [Fact]
    public void Userdata_MethodCallbackException_ShouldTranslateToLuaException()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        LuaException exception = Should.Throw<LuaException>(() =>
            state.DoString("x = getmetatable(failing).__namecall(failing, \"explodeMethod\")")
        );

        exception.Message.ShouldContain("__namecall callback failed");
        exception.Message.ShouldContain("Boom from OnMethodCall");
    }

    [Fact]
    public void Userdata_MethodCallbackException_ShouldBeCatchableByPCall()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              return getmetatable(failing).__namecall(failing, "explodeMethod")
            end)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("__namecall callback failed");
        err.ShouldContain("Boom from OnMethodCall");
    }

    [Fact]
    public void Userdata_MethodCanReturnMultipleValues()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            counter.value = 10
            first, second = getmetatable(counter).__namecall(counter, "pair")
            """
        );

        state.Globals.TryGet("first", out int first).ShouldBeTrue();
        first.ShouldBe(10);

        state.Globals.TryGet("second", out int second).ShouldBeTrue();
        second.ShouldBe(11);
    }

    [Fact]
    public void Userdata_MethodCanReturnNoValues()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            counter.value = 10
            noResult = getmetatable(counter).__namecall(counter, "touch")
            after = counter.value
            """
        );

        state.Globals.TryGet("noResult", out LuauValue noResult).ShouldBeTrue();
        noResult.Type.ShouldBe(LuauValueType.Nil);

        state.Globals.TryGet("after", out int after).ShouldBeTrue();
        after.ShouldBe(11);
    }

    [Fact]
    public void Userdata_UnhandledMethodThatPushes_ShouldStillErrorAndStayUsable()
    {
        using var state = new LuauState();
        state.Globals.Set("counter", new CounterUserdata());

        state.DoString(
            """
            ok, err = pcall(function()
              return getmetatable(counter).__namecall(counter, "badUnknown")
            end)
            after = getmetatable(counter).__namecall(counter, "add", 1)
            """
        );

        state.Globals.TryGet("ok", out bool ok).ShouldBeTrue();
        ok.ShouldBeFalse();

        state.Globals.TryGet("err", out string? err).ShouldBeTrue();
        err.ShouldContain("unknown userdata method 'badUnknown'");

        state.Globals.TryGet("after", out int after).ShouldBeTrue();
        after.ShouldBe(1);
    }

    [Fact]
    public void Userdata_RepeatedPCallFailure_ShouldStayStable()
    {
        using var state = new LuauState();
        state.Globals.Set("failing", new FailingUserdata());

        state.DoString(
            """
            failures = 0
            for i = 1, 2000 do
              local ok = pcall(function()
                failing.explodeSet = i
              end)
              if not ok then
                failures = failures + 1
              end
            end
            """
        );

        state.Globals.TryGet("failures", out int failures).ShouldBeTrue();
        failures.ShouldBe(2000);
    }

    private sealed class CounterUserdata : ILuauUserData<CounterUserdata>
    {
        public static string? LastMethodName { get; set; }
        public static int LastParameterCount { get; set; }

        public int Value { get; private set; }

        public static LuauReturnSingle OnIndex(
            CounterUserdata self,
            in LuauState state,
            in ReadOnlySpan<char> fieldName
        )
        {
            return fieldName switch
            {
                "value" => LuauReturnSingle.Ok(self.Value),
                _ => LuauReturnSingle.NotHandled,
            };
        }

        public static LuauOutcome OnSetIndex(CounterUserdata self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName)
        {
            switch (fieldName)
            {
                case "value":
                {
                    if (!args.TryReadNumber(out int value, out string? error))
                        return LuauOutcome.Error(error);
                    self.Value = value;
                    return LuauOutcome.Ok();
                }
                default:
                    return LuauOutcome.NotHandledError;
            }
        }

        public static LuauReturn OnMethodCall(
            CounterUserdata self,
            LuauArgs functionArgs,
            in ReadOnlySpan<char> methodName
        )
        {
            LastMethodName = methodName.ToString();
            LastParameterCount = functionArgs.ArgumentCount;

            switch (methodName)
            {
                case "add":
                {
                    if (functionArgs.ArgumentCount != 1)
                        return LuauReturn.Error($"expected 1 arguments, got {functionArgs.ArgumentCount}");
                    if (!functionArgs.TryReadNumber(1, out int offset, out string? error))
                        return LuauReturn.Error(error);

                    return LuauReturn.Ok(self.Value + offset);
                }
                case "pair":
                {
                    if (functionArgs.ArgumentCount != 0)
                        return LuauReturn.Error($"expected 0 arguments, got {functionArgs.ArgumentCount}");

                    return LuauReturn.Ok(self.Value, self.Value + 1);
                }
                case "touch":
                {
                    if (functionArgs.ArgumentCount != 0)
                        return LuauReturn.Error($"expected 0 arguments, got {functionArgs.ArgumentCount}");

                    self.Value++;
                    return LuauReturn.Ok();
                }
                case "badUnknown":
                    return LuauReturn.NotHandledError;
                default:
                    return LuauReturn.NotHandledError;
            }
        }

        public static implicit operator IntoLuau(CounterUserdata value) => IntoLuau.FromUserdata(value);
    }

    private sealed class FailingUserdata : ILuauUserData<FailingUserdata>
    {
        public static LuauReturnSingle OnIndex(
            FailingUserdata self,
            in LuauState state,
            in ReadOnlySpan<char> fieldName
        )
        {
            return fieldName switch
            {
                "explode" => throw new InvalidOperationException("Boom from OnIndex"),
                "errorIndex" => LuauReturnSingle.Error("error from OnIndex"),
                _ => LuauReturnSingle.NotHandled,
            };
        }

        public static LuauOutcome OnSetIndex(FailingUserdata self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName)
        {
            return fieldName switch
            {
                "explodeSet" => throw new InvalidOperationException("Boom from OnSetIndex"),
                "errorSet" => LuauOutcome.Error("error from OnSetIndex"),
                _ => LuauOutcome.NotHandledError,
            };
        }

        public static LuauReturn OnMethodCall(
            FailingUserdata self,
            LuauArgs functionArgs,
            in ReadOnlySpan<char> methodName
        )
        {
            return methodName switch
            {
                "explodeMethod" => throw new InvalidOperationException("Boom from OnMethodCall"),
                _ => LuauReturn.NotHandledError,
            };
        }

        public static implicit operator IntoLuau(FailingUserdata value) => IntoLuau.FromUserdata(value);
    }
}
