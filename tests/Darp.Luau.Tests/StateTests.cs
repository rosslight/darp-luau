using Shouldly;

namespace Darp.Luau.Tests;

public sealed class StateTests : IDisposable
{
    private readonly LuauState _state = new();

    [Fact]
    public void DoString_ShouldReturnVoid_U8()
    {
        _state.DoString("result = 42"u8);

        _state.Globals.GetNumber("result").ShouldBe(42);
    }

    [Fact]
    public void DoString_ShouldReturnVoid()
    {
        _state.DoString("result = 42");

        _state.Globals.GetNumber("result").ShouldBe(42);
    }

    [Fact]
    public void DoString_ShouldReturnTypedValue_U8()
    {
        int result = _state.DoString<int>("return 42"u8);

        result.ShouldBe(42);
    }

    [Fact]
    public void DoString_ShouldReturnTypedValue()
    {
        int result = _state.DoString<int>("return 42");

        result.ShouldBe(42);
    }

    [Fact]
    public void DoString_TupleReturn_ShouldIgnoreAdditionalReturnValues_U8()
    {
        (byte first, short second) = _state.DoString<byte, short>("return 10, 11, 12"u8);

        first.ShouldBe<byte>(10);
        second.ShouldBe<short>(11);
    }

    [Fact]
    public void DoString_TupleReturn_ShouldIgnoreAdditionalReturnValues()
    {
        (byte first, short second) = _state.DoString<byte, short>("return 10, 11, 12");

        first.ShouldBe<byte>(10);
        second.ShouldBe<short>(11);
    }

    [Fact]
    public void DoString_TupleReturn_WithOwnedReference_ShouldCloneReferenceOwnership()
    {
        (LuauTable table, int count) = _state.DoString<LuauTable, int>("return { value = 42 }, 5");
        using (table)
        {
            count.ShouldBe(5);
            table.GetNumber("value").ShouldBe(42);
        }
    }

    [Fact]
    public void DoString_ShouldReturnTypedTuple()
    {
        (int number, string? text, bool flag) = _state.DoString<int, string?, bool>("return 10, 'hello', true");

        number.ShouldBe(10);
        text.ShouldBe("hello");
        flag.ShouldBeTrue();
    }

    [Fact]
    public void DoString_ShouldReturnTypedTuple4_U8()
    {
        (int number, string? nilText, string text, bool flag) = _state.DoString<int, string?, string, bool>(
            "return 10, nil, 'hello', true"u8
        );

        number.ShouldBe(10);
        nilText.ShouldBeNull();
        text.ShouldBe("hello");
        flag.ShouldBeTrue();
    }

    [Fact]
    public void DoString_ShouldReturnTypedTuple4()
    {
        (int number, string? nilText, string text, bool flag) = _state.DoString<int, string?, string, bool>(
            "return 10, nil, 'hello', true"
        );

        number.ShouldBe(10);
        nilText.ShouldBeNull();
        text.ShouldBe("hello");
        flag.ShouldBeTrue();
    }

    [Fact]
    public void DoString_TupleReturn_ShouldThrowWhenTooFewValuesAreReturned()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _state.DoString<int, int>("return 1"));
    }

    [Fact]
    public void DoStringMulti_ShouldReturnEmptyArray_WhenChunkReturnsNothing()
    {
        LuauValue[] values = _state.DoStringMulti("return");

        values.ShouldBeEmpty();
    }

    [Fact]
    public void DoStringMulti_ShouldReadAllReturnValues()
    {
        LuauValue[] values = _state.DoStringMulti("return 10, 'hello', true");
        values.Length.ShouldBe(3);

        using LuauValue value1 = values[0];
        using LuauValue value2 = values[1];
        using LuauValue value3 = values[2];

        value1.TryGet(out int number, acceptNil: false).ShouldBeTrue();
        value2.TryGet(out string? text, acceptNil: false).ShouldBeTrue();
        value3.TryGet(out bool flag, acceptNil: false).ShouldBeTrue();

        number.ShouldBe(10);
        text.ShouldBe("hello");
        flag.ShouldBeTrue();
    }

    [Fact]
    public void DoStringMulti_WithOwnedReference_ShouldCloneReferenceOwnership()
    {
        ulong baselineActiveReferences = _state.MemoryStatistics.ActiveRegistryReferences;

        LuauValue[] values = _state.DoStringMulti("return { value = 42 }, 5");
        values.Length.ShouldBe(2);

        using LuauValue tableValue = values[0];
        using LuauValue countValue = values[1];

        tableValue.TryGet(out LuauTable table).ShouldBeTrue();
        using (table)
        {
            table.GetNumber("value").ShouldBe(42);
        }

        countValue.TryGet(out int count, acceptNil: false).ShouldBeTrue();
        count.ShouldBe(5);
    }

    [Fact]
    public void DoStringMulti_ByteSpanOverload_ShouldMatchCharSpanOverload()
    {
        LuauValue[] values = _state.DoStringMulti("return 1, 2"u8);
        values.Length.ShouldBe(2);

        using LuauValue value1 = values[0];
        using LuauValue value2 = values[1];

        value1.TryGet(out int first, acceptNil: false).ShouldBeTrue();
        value2.TryGet(out int second, acceptNil: false).ShouldBeTrue();

        first.ShouldBe(1);
        second.ShouldBe(2);
    }

    [Fact]
    public void DoStringMulti_ShouldThrowLuaException_WhenChunkErrors()
    {
        LuaException exception = Should.Throw<LuaException>(() => _state.DoStringMulti("error('boom')"));

        exception.Message.ShouldContain("boom");
    }

    public void Dispose()
    {
        _state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(2U);
        _state.Dispose();
    }
}
