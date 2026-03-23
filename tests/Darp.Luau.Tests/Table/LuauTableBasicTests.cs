using Shouldly;

namespace Darp.Luau.Tests.Table;

public sealed class LuauTableBasicTests : IDisposable
{
    private readonly LuauState _lua = new();

    [Fact]
    public void Set_Then_GetNumber_Double_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("myKey", 1);

        double value = table.GetNumber("myKey");

        value.ShouldBe(1);
    }

    [Fact]
    public void Set_Then_GetNumber_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("myKey", 1);

        table.GetNumber("myKey").ShouldBe(1);
    }

    [Fact]
    public void Set_Then_GetBoolean_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("boolKey", true);

        bool value = table.GetBoolean("boolKey");

        value.ShouldBeTrue();
    }

    [Fact]
    public void Set_Then_GetUtf8String_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();
        using LuauString str = _lua.CreateString("test string");
        table.Set("stringKey", str);

        table.GetUtf8String("stringKey").ShouldBe("test string");
    }

    [Fact]
    public void Set_Overwrites_ExistingKey()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("key", 1);
        table.Set("key", 42);

        table.GetNumber("key").ShouldBe(42);
    }

    [Fact]
    public void Set_MultipleEntries_AllRetrievable()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("key1", 1);
        table.Set("key2", 2);
        table.Set("key3", 3);

        table.GetNumber("key1").ShouldBe(1);
        table.GetNumber("key2").ShouldBe(2);
        table.GetNumber("key3").ShouldBe(3);
    }

    [Fact]
    public void ContainsKey_PresentKey_ReturnsTrue()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("present", 1);

        table.ContainsKey("present").ShouldBeTrue();
    }

    [Fact]
    public void ContainsKey_MissingKey_ReturnsFalse()
    {
        using LuauTable table = _lua.CreateTable();

        table.ContainsKey("missing").ShouldBeFalse();
    }

    [Fact]
    public void ContainsKey_KeySetToNil_ReturnsFalse()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("key", 1);
        table.Set("key", (string?)null);

        table.ContainsKey("key").ShouldBeFalse();
    }

    [Fact]
    public void ContainsKey_UsesIndexMetamethod_ReturnsTrue()
    {
        using LuauTable table = _lua.CreateTable();
        _lua.Globals.Set("tableUnderTest", table);

        _lua.Load(
                "setmetatable(tableUnderTest, { __index = function(_, key) if key == 'virtualKey' then return 123 end end })"
            )
            .Execute();

        table.ContainsKey("virtualKey").ShouldBeTrue();
    }

    [Fact]
    public void Set_NestedTable_RoundTrips()
    {
        using LuauTable parent = _lua.CreateTable();
        using LuauTable nested = _lua.CreateTable();
        nested.Set("nestedKey", 123);
        parent.Set("tableKey", nested);

        using LuauTable retrieved = parent.GetLuauTable("tableKey");
        retrieved.GetNumber("nestedKey").ShouldBe(123);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalseAndDefault_ForTypedOverload()
    {
        using LuauTable table = _lua.CreateTable();

        table.TryGet("missing", out double value).ShouldBeFalse();

        value.ShouldBe(0);
    }

    [Fact]
    public void GetNumber_MissingKey_ThrowsException()
    {
        using LuauTable table = _lua.CreateTable();

        Should.Throw<Exception>(() => table.GetNumber("missing"));
    }

    [Fact]
    public void TryGetNumberOrNil_MissingKey_ReturnsTrueAndNull()
    {
        using LuauTable table = _lua.CreateTable();

        table.TryGetNumberOrNil("missing", out double? value).ShouldBeTrue();
        value.ShouldBeNull();
    }

    [Fact]
    public void GetNumberOrNil_MissingKey_ReturnsNull()
    {
        using LuauTable table = _lua.CreateTable();

        table.GetNumberOrNil("missing").ShouldBeNull();
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsNil_ForRawValueOverload()
    {
        using LuauTable table = _lua.CreateTable();

        table.TryGet("missing", out LuauValue value).ShouldBeTrue();

        value.ShouldBe(default);
        value.Type.ShouldBe(LuauValueType.Nil);
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsNilValue()
    {
        using LuauTable table = _lua.CreateTable();

        LuauValue value = table["missing"];

        value.ShouldBe(default);
        value.Type.ShouldBe(LuauValueType.Nil);
    }

    [Fact]
    public void Set_Then_GetNumber_NumericKey_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set(1.0, 42);

        table.GetNumber(1.0).ShouldBe(42);
    }

    [Fact]
    public void Set_Then_GetLuauValue_SpanCharKey_RoundTrips()
    {
        const string keyStr = "spanCharKey";
        using LuauTable table = _lua.CreateTable();

        ReadOnlySpan<char> key = keyStr.AsSpan();
        table.Set(key, 42.0);

        LuauValue value = table.GetLuauValue(key);
        value.TryGet(out double doubleValue).ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    [Fact]
    public void Set_Then_GetLuauValue_SpanByteKey_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();

        var key = "spanByteKey"u8;
        using LuauValue keyValue = _lua.CreateString(key).DisposeAndToLuauValue();
        table.Set(keyValue, 42.0);

        LuauValue value = table.GetLuauValue(keyValue);
        value.TryGet(out double doubleValue).ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    [Fact]
    public void TryGet_WrongType_StringAsNumber_ReturnsFalse()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("stringKey", "not a number");

        table.TryGet("stringKey", out double value).ShouldBeFalse();

        value.ShouldBe(0);
    }

    [Fact]
    public void TryGet_WrongType_NumberAsString_ReturnsFalse()
    {
        using LuauTable table = _lua.CreateTable();
        table.Set("numKey", 42.0);

        table.TryGet("numKey", out string? value).ShouldBeFalse();

        value.ShouldBeNull();
    }

    [Fact]
    public void MultipleTables_AreIndependent()
    {
        using LuauTable table1 = _lua.CreateTable();
        using LuauTable table2 = _lua.CreateTable();

        table1.Set("key", 1);
        table2.Set("key", 2);

        table1.GetNumber("key").ShouldBe(1);
        table2.GetNumber("key").ShouldBe(2);
    }

    [Fact]
    public void NestedTables_MultipleLevels_RoundTrip()
    {
        using LuauTable level1 = _lua.CreateTable();
        using LuauTable level2 = _lua.CreateTable();
        using LuauTable level3 = _lua.CreateTable();

        level3.Set("deep", 999);
        level2.Set("level3", level3);
        level1.Set("level2", level2);

        using LuauTable retrieved2 = level1.GetLuauTable("level2");
        using LuauTable retrieved3 = retrieved2.GetLuauTable("level3");
        retrieved3.GetNumber("deep").ShouldBe(999);
    }

    [Fact]
    public void Table_WithMixedValueTypes_RoundTrips()
    {
        using LuauTable table = _lua.CreateTable();

        table.Set("bool", true);
        table.Set("number", 42.0);
        using LuauString str = _lua.CreateString("text");
        table.Set("string", str);
        using LuauTable nested = _lua.CreateTable();
        nested.Set("nested", 123);
        table.Set("table", nested);

        table.GetBoolean("bool").ShouldBeTrue();
        table.GetNumber("number").ShouldBe(42.0);
        table.GetUtf8String("string").ShouldBe("text");

        using LuauTable v4 = table.GetLuauTable("table");
        v4.GetNumber("nested").ShouldBe(123);
    }

    [Fact]
    public void Table_DefaultShouldBeNil()
    {
        using LuauTable table = default;
        table.ToString().ShouldBe("<nil>");
    }

    public void Dispose()
    {
        _lua.MemoryStatistics.ActiveRegistryReferences.ShouldBe(2UL);
        _lua.Dispose();
    }
}
