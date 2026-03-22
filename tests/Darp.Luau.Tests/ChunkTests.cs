using Shouldly;

namespace Darp.Luau.Tests;

public sealed class ChunkTests : IDisposable
{
    private readonly LuauState _lua = new();

    [Fact]
    public void Execute_Simple()
    {
        LuauChunk chunk = _lua.Load("result = 1");
        chunk.Execute();

        _lua.Globals.GetNumber("result").ShouldBe(1);
    }

    [Fact]
    public void Execute_Simple_Arguments()
    {
        LuauChunk chunk = _lua.Load("result = ...");
        chunk.Execute(6);

        _lua.Globals.GetNumber("result").ShouldBe(6);
    }

    [Fact]
    public void Execute_Returns()
    {
        LuauChunk chunk = _lua.Load("return 1 + 1");
        int result = chunk.Execute<int>();

        result.ShouldBe(2);
    }

    [Fact]
    public void Execute_Returns2()
    {
        LuauChunk chunk = _lua.Load("return 1, 2");
        (int result1, int result2) = chunk.Execute<int, int>();

        result1.ShouldBe(1);
        result2.ShouldBe(2);
    }

    [Fact]
    public void Execute_Arguments()
    {
        LuauChunk chunk = _lua.Load(
            """
            local a, b = ...
            return a + b
            """
        );
        int result = chunk.Execute<int>(1, 2);

        result.ShouldBe(3);
    }

    [Fact]
    public void ToFunction_Simple()
    {
        LuauChunk chunk = _lua.Load("return 1 + 1");
        using LuauFunction func = chunk.ToFunction();

        int result = func.Invoke<int>();

        result.ShouldBe(2);
    }

    [Fact]
    public void ToFunction_Arguments()
    {
        LuauChunk chunk = _lua.Load(
            """
            local a, b = ...
            return a + b
            """
        );
        using LuauFunction func = chunk.ToFunction();

        int result = func.Invoke<int>(1, 2);

        result.ShouldBe(3);
    }

    [Fact]
    public void ExecuteMulti_ReturnsAllValues()
    {
        LuauChunk chunk = _lua.Load("return 10, 'hello', true");
        LuauValue[] values = chunk.ExecuteMulti();

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
    public void ExecuteMulti_WithOwnedReference_ClonesOwnership()
    {
        LuauChunk chunk = _lua.Load("return { value = 42 }, 5");
        LuauValue[] values = chunk.ExecuteMulti();

        values.Length.ShouldBe(2);

        using LuauValue tableValue = values[0];
        using LuauValue countValue = values[1];

        ulong beforeClone = _lua.MemoryStatistics.ActiveRegistryReferences;
        tableValue.TryGet(out LuauTable table).ShouldBeTrue();
        using (table)
        {
            _lua.MemoryStatistics.ActiveRegistryReferences.ShouldBe(beforeClone + 1);
            table.GetNumber("value").ShouldBe(42);
        }
        _lua.MemoryStatistics.ActiveRegistryReferences.ShouldBe(beforeClone);

        countValue.TryGet(out int count, acceptNil: false).ShouldBeTrue();
        count.ShouldBe(5);
    }

    [Fact]
    public void Load_ByteSpan_Execute()
    {
        LuauChunk chunk = _lua.Load("result = 42"u8);
        chunk.Execute();

        _lua.Globals.GetNumber("result").ShouldBe(42);
    }

    [Fact]
    public void Load_ByteSpan_ExecuteTyped()
    {
        LuauChunk chunk = _lua.Load("return 42"u8);
        int result = chunk.Execute<int>();

        result.ShouldBe(42);
    }

    [Fact]
    public void Load_ByteSpan_ExecuteMulti()
    {
        LuauChunk chunk = _lua.Load("return 1, 2"u8);
        LuauValue[] values = chunk.ExecuteMulti();

        values.Length.ShouldBe(2);

        using LuauValue value1 = values[0];
        using LuauValue value2 = values[1];

        value1.TryGet(out int first, acceptNil: false).ShouldBeTrue();
        value2.TryGet(out int second, acceptNil: false).ShouldBeTrue();

        first.ShouldBe(1);
        second.ShouldBe(2);
    }

    [Fact]
    public void WithName_UsesChunkNameInError()
    {
        LuauChunk chunk = _lua.Load("error('boom')").WithName("@named_chunk.luau");

        LuaException exception;
        try
        {
            chunk.Execute();
            throw new ShouldAssertException("Expected LuaException to be thrown.");
        }
        catch (LuaException ex)
        {
            exception = ex;
        }

        exception.Message.ShouldContain("named_chunk.luau");
        exception.Message.ShouldContain("boom");
    }

    [Fact]
    public void WithName_WorksWithByteSource()
    {
        LuauChunk chunk = _lua.Load("error('boom')"u8).WithName("@named_utf8_chunk.luau");

        LuaException exception;
        try
        {
            chunk.Execute();
            throw new ShouldAssertException("Expected LuaException to be thrown.");
        }
        catch (LuaException ex)
        {
            exception = ex;
        }

        exception.Message.ShouldContain("named_utf8_chunk.luau");
    }

    public void Dispose()
    {
        _lua.MemoryStatistics.ActiveRegistryReferences.ShouldBe(2U);
        _lua.Dispose();
    }
}
