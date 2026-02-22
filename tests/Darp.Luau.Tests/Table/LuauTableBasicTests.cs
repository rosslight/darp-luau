using Shouldly;

namespace Darp.Luau.Tests.Table;

public sealed class LuauTableBasicTests
{
    [Fact]
    public void Set_then_TryGet_double_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("myKey", 1);

        double value = table.GetNumber("myKey");

        value.ShouldBe(1);
    }

    [Fact]
    public void Set_then_GetNumber_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("myKey", 1);

        table.GetNumber("myKey").ShouldBe(1);
    }

    [Fact]
    public void Set_then_TryGet_bool_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("boolKey", true);

        bool value = table.GetBoolean("boolKey");

        value.ShouldBeTrue();
    }

    [Fact]
    public void Set_then_TryGet_string_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("stringKey", lua.CreateString("test string"));

        table.TryGet("stringKey", out string? value).ShouldBeTrue();

        value.ShouldBe("test string");
    }

    [Fact]
    public void Set_overwrites_existing_key()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("key", 1);
        table.Set("key", 42);

        table.TryGet("key", out double value).ShouldBeTrue();

        value.ShouldBe(42);
    }

    [Fact]
    public void Set_multiple_entries_all_retrievable()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("key1", 1);
        table.Set("key2", 2);
        table.Set("key3", 3);

        table.TryGet("key1", out double value1).ShouldBeTrue();
        table.TryGet("key2", out double value2).ShouldBeTrue();
        table.TryGet("key3", out double value3).ShouldBeTrue();

        value1.ShouldBe(1);
        value2.ShouldBe(2);
        value3.ShouldBe(3);
    }

    [Fact]
    public void Set_nested_table_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable parent = lua.CreateTable();
        LuauTable nested = lua.CreateTable();
        nested.Set("nestedKey", 123);
        parent.Set("tableKey", nested);

        parent.TryGet("tableKey", out LuauTable retrieved).ShouldBeTrue();
        retrieved.TryGet("nestedKey", out double nestedValue).ShouldBeTrue();

        nestedValue.ShouldBe(123);
    }

    [Fact]
    public void TryGet_missing_key_returns_false_and_default_for_typed_overload()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGet("missing", out double value).ShouldBeFalse();

        value.ShouldBe(0);
    }

    [Fact]
    public void GetNumber_missing_key_throws_Exception()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        Should.Throw<Exception>(() => table.GetNumber("missing"));
    }

    [Fact]
    public void TryGetNumberOrNil_missing_key_returns_true_and_null()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGetNumberOrNil("missing", out double? value).ShouldBeTrue();
        value.ShouldBeNull();
    }

    [Fact]
    public void GetNumberOrNil_missing_key_returns_null()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.GetNumberOrNil("missing").ShouldBeNull();
    }

    [Fact]
    public void TryGet_missing_key_returns_Nil_for_raw_value_overload()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.TryGet("missing", out LuauValue value).ShouldBeTrue();

        value.ShouldBe(default);
        value.Type.ShouldBe(LuauValueType.Nil);
    }

    [Fact]
    public void Indexer_missing_key_returns_Nil_value()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        LuauValue value = table["missing"];

        value.ShouldBe(default);
        value.Type.ShouldBe(LuauValueType.Nil);
    }

    [Fact]
    public void Set_then_TryGet_numeric_key_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set(1.0, 42);

        table.TryGet(1.0, out double value).ShouldBeTrue();

        value.ShouldBe(42);
    }

    [Fact]
    public void Set_then_TryGet_span_char_key_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        string keyStr = "spanCharKey";
        ReadOnlySpan<char> key = keyStr.AsSpan();
        table.Set(key, 42.0);

        table.TryGet(key, out LuauValue value).ShouldBeTrue();
        value.TryGet(out double doubleValue).ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    [Fact]
    public void Set_then_TryGet_span_byte_key_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        var key = "spanByteKey"u8;
        LuauValue keyValue = lua.CreateString(key);
        table.Set(keyValue, 42.0);

        table.TryGet(keyValue, out LuauValue value).ShouldBeTrue();
        value.TryGet(out double doubleValue).ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    [Fact]
    public void TryGet_wrong_type_string_as_number_returns_false()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("stringKey", "not a number");

        table.TryGet("stringKey", out double value).ShouldBeFalse();

        value.ShouldBe(0);
    }

    [Fact]
    public void TryGet_wrong_type_number_as_string_returns_false()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();
        table.Set("numKey", 42.0);

        table.TryGet("numKey", out string? value).ShouldBeFalse();

        value.ShouldBeNull();
    }

    [Fact]
    public void MultipleTables_are_independent()
    {
        using var lua = new LuauState();
        LuauTable table1 = lua.CreateTable();
        LuauTable table2 = lua.CreateTable();

        table1.Set("key", 1);
        table2.Set("key", 2);

        table1.TryGet("key", out double value1).ShouldBeTrue();
        table2.TryGet("key", out double value2).ShouldBeTrue();

        value1.ShouldBe(1);
        value2.ShouldBe(2);
    }

    [Fact]
    public void NestedTables_multiple_levels_roundtrip()
    {
        using var lua = new LuauState();
        LuauTable level1 = lua.CreateTable();
        LuauTable level2 = lua.CreateTable();
        LuauTable level3 = lua.CreateTable();

        level3.Set("deep", 999);
        level2.Set("level3", level3);
        level1.Set("level2", level2);

        level1.TryGet("level2", out LuauTable retrieved2).ShouldBeTrue();
        retrieved2.TryGet("level3", out LuauTable retrieved3).ShouldBeTrue();
        retrieved3.TryGet("deep", out double deepValue).ShouldBeTrue();

        deepValue.ShouldBe(999);
    }

    [Fact]
    public void Table_with_mixed_value_types_roundtrips()
    {
        using var lua = new LuauState();
        LuauTable table = lua.CreateTable();

        table.Set("bool", true);
        table.Set("number", 42.0);
        table.Set("string", lua.CreateString("text"));
        LuauTable nested = lua.CreateTable();
        nested.Set("nested", 123);
        table.Set("table", nested);

        table.TryGet("bool", out bool v1).ShouldBeTrue();
        v1.ShouldBeTrue();

        table.TryGet("number", out double v2).ShouldBeTrue();
        v2.ShouldBe(42.0);

        table.TryGet("string", out string? v3).ShouldBeTrue();
        v3.ShouldBe("text");

        table.TryGet("table", out LuauTable v4).ShouldBeTrue();
        v4.TryGet("nested", out double v5).ShouldBeTrue();
        v5.ShouldBe(123);
    }

    [Fact]
    public void Table_DefaultShouldBeNil()
    {
        LuauTable table = default;
        table.ToString().ShouldBe("<nil>");
    }
}
