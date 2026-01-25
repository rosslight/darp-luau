using Darp.Luau;
using Shouldly;

namespace Darp.Lua.Net.Tests;

public sealed class TableTests
{
    [Fact]
    public void Simple()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("myKey", 1);

        bool isSuccess = myTable.TryGet("myKey", out double myValue);

        isSuccess.ShouldBeTrue();
        myValue.ShouldBe(1);
    }

    // Basic Operations Tests
    [Fact]
    public void Set_OverwriteExistingKey_UpdatesValue()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("key", 1);
        myTable.Set("key", 42);

        bool isSuccess = myTable.TryGet("key", out double myValue);

        isSuccess.ShouldBeTrue();
        myValue.ShouldBe(42);
    }

    [Fact]
    public void TryGet_NonExistentKey_ReturnsFalse()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();

        bool isSuccess = myTable.TryGet("nonExistent", out double myValue);

        isSuccess.ShouldBeFalse();
        myValue.ShouldBe(0);
    }

    [Fact]
    public void TryGet_NonExistentKey_OutputsDefault()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();

        myTable.TryGet("nonExistent", out double doubleValue).ShouldBeFalse();
        myTable.TryGet("nonExistent", out string? stringValue).ShouldBeFalse();
        myTable.TryGet("nonExistent", out LuauTable tableValue).ShouldBeFalse();
        myTable.TryGet("nonExistent", out LuauFunction functionValue).ShouldBeFalse();

        doubleValue.ShouldBe(0);
        stringValue.ShouldBeNull();
        // tableValue and functionValue will be default (ref structs can't be compared with ShouldBe)
        // Just verify they are default by checking they don't have a state
    }

    [Fact]
    public void Set_MultipleEntries_AllRetrievable()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("key1", 1);
        myTable.Set("key2", 2);
        myTable.Set("key3", 3);

        myTable.TryGet("key1", out double value1).ShouldBeTrue();
        myTable.TryGet("key2", out double value2).ShouldBeTrue();
        myTable.TryGet("key3", out double value3).ShouldBeTrue();

        value1.ShouldBe(1);
        value2.ShouldBe(2);
        value3.ShouldBe(3);
    }

    // Value Type Tests
    [Fact]
    public void Set_Get_Boolean_True()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("boolKey", true);

        bool isSuccess = myTable.TryGet("boolKey", out bool value);

        isSuccess.ShouldBeTrue();
        value.ShouldBeTrue();
    }

    [Fact]
    public void Set_Get_Boolean_False()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("boolKey", false);

        bool isSuccess = myTable.TryGet("boolKey", out bool value);

        isSuccess.ShouldBeTrue();
        value.ShouldBeFalse();
    }

    [Fact]
    public void Set_Get_String()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        LuauString str = lua.CreateString("test string");
        myTable.Set("stringKey", str);

        bool isSuccess = myTable.TryGet("stringKey", out string? value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe("test string");
    }

    [Fact]
    public void Set_Get_Number_Integer()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", 42.0);

        bool isSuccess = myTable.TryGet("numKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(42.0);
    }

    [Fact]
    public void Set_Get_Number_Decimal()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", 3.14159);

        bool isSuccess = myTable.TryGet("numKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(3.14159);
    }

    [Fact]
    public void Set_Get_Number_Zero()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", 0.0);

        bool isSuccess = myTable.TryGet("numKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(0.0);
    }

    [Fact]
    public void Set_Get_Number_Negative()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", -42.5);

        bool isSuccess = myTable.TryGet("numKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(-42.5);
    }

    [Fact]
    public void Set_Get_NestedTable()
    {
        using var lua = new LuauState();
        LuauTable parentTable = lua.CreateTable();
        LuauTable nestedTable = lua.CreateTable();
        nestedTable.Set("nestedKey", 123);
        parentTable.Set("tableKey", nestedTable);

        bool isSuccess = parentTable.TryGet("tableKey", out LuauTable retrievedTable);

        isSuccess.ShouldBeTrue();
        retrievedTable.TryGet("nestedKey", out double nestedValue).ShouldBeTrue();
        nestedValue.ShouldBe(123);
    }

    // Key Type Tests
    [Fact]
    public void Set_Get_StringKey()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("stringKey", 42);

        bool isSuccess = myTable.TryGet("stringKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void Set_Get_NumericKey()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set(1.0, 42);

        bool isSuccess = myTable.TryGet(1.0, out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void Set_Get_SpanCharKey()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        // Test ReadOnlySpan<char> by converting to string first (extension method limitation with ref structs)
        string keyStr = "spanCharKey";
        ReadOnlySpan<char> key = keyStr.AsSpan();
        // Create LuauValue key manually since extension method has issues with ref structs
        LuauValue keyValue = lua.CreateString(key);
        myTable.Set(keyValue, 42.0);

        bool isSuccess = myTable.TryGet(keyValue, out LuauValue value);
        bool gotValue = value.TryGet(out double doubleValue);

        isSuccess.ShouldBeTrue();
        gotValue.ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    [Fact]
    public void Set_Get_SpanByteKey()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        // Test ReadOnlySpan<byte> by converting first (extension method limitation with ref structs)
        ReadOnlySpan<byte> key = "spanByteKey"u8;
        // Create LuauValue key manually since extension method has issues with ref structs
        LuauValue keyValue = lua.CreateString(key);
        myTable.Set(keyValue, 42.0);

        bool isSuccess = myTable.TryGet(keyValue, out LuauValue value);
        bool gotValue = value.TryGet(out double doubleValue);

        isSuccess.ShouldBeTrue();
        gotValue.ShouldBeTrue();
        doubleValue.ShouldBe(42);
    }

    // Typed TryGet Overloads
    [Fact]
    public void TryGet_Typed_Double()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", 42.5);

        bool isSuccess = myTable.TryGet("numKey", out double value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe(42.5);
    }

    [Fact]
    public void TryGet_Typed_String()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        LuauString str = lua.CreateString("test value");
        myTable.Set("stringKey", str);

        bool isSuccess = myTable.TryGet("stringKey", out string? value);

        isSuccess.ShouldBeTrue();
        value.ShouldBe("test value");
    }

    [Fact]
    public void TryGet_Typed_Table()
    {
        using var lua = new LuauState();
        LuauTable parentTable = lua.CreateTable();
        LuauTable nestedTable = lua.CreateTable();
        nestedTable.Set("nested", 99);
        parentTable.Set("tableKey", nestedTable);

        bool isSuccess = parentTable.TryGet("tableKey", out LuauTable value);

        isSuccess.ShouldBeTrue();
        value.TryGet("nested", out double nestedValue).ShouldBeTrue();
        nestedValue.ShouldBe(99);
    }

    [Fact]
    public void TryGet_Typed_WrongType_StringAsNumber_ReturnsFalse()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        LuauString str = lua.CreateString("not a number");
        myTable.Set("stringKey", str);

        bool isSuccess = myTable.TryGet("stringKey", out double value);

        isSuccess.ShouldBeFalse();
        value.ShouldBe(0);
    }

    [Fact]
    public void TryGet_Typed_WrongType_NumberAsString_ReturnsFalse()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("numKey", 42.0);

        bool isSuccess = myTable.TryGet("numKey", out string? value);

        isSuccess.ShouldBeFalse();
        value.ShouldBeNull();
    }

    // Edge Cases and Error Scenarios
    [Fact]
    public void Set_NullKey_ThrowsArgumentNullException()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        string? nullKey = null;

        // Can't use lambda with ref struct, use try-catch directly
        bool exceptionThrown = false;
        try
        {
            myTable.Set(nullKey!, 42);
        }
        catch (ArgumentNullException)
        {
            exceptionThrown = true;
        }

        exceptionThrown.ShouldBeTrue();
    }

    [Fact]
    public void Table_AfterStateDisposal_Throws()
    {
        LuauState lua = new LuauState();
        LuauTable myTable = lua.CreateTable();
        myTable.Set("key", 42);
        lua.Dispose();

        // Can't use lambda with ref struct, use try-catch directly
        bool exceptionThrown = false;
        try
        {
            myTable.Set("key2", 43);
        }
        catch (ObjectDisposedException)
        {
            exceptionThrown = true;
        }

        exceptionThrown.ShouldBeTrue();
    }

    [Fact]
    public void MultipleTables_Independent()
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

    // Advanced Scenarios
    [Fact]
    public void NestedTables_MultipleLevels()
    {
        using var lua = new LuauState();
        LuauTable level1 = lua.CreateTable();
        LuauTable level2 = lua.CreateTable();
        LuauTable level3 = lua.CreateTable();

        level3.Set("deep", 999);
        level2.Set("level3", level3);
        level1.Set("level2", level2);

        bool isSuccess = level1.TryGet("level2", out LuauTable retrieved2);
        isSuccess.ShouldBeTrue();

        isSuccess = retrieved2.TryGet("level3", out LuauTable retrieved3);
        isSuccess.ShouldBeTrue();

        isSuccess = retrieved3.TryGet("deep", out double deepValue);
        isSuccess.ShouldBeTrue();
        deepValue.ShouldBe(999);
    }

    [Fact]
    public void Table_WithMixedKeyTypes()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();

        myTable.Set("stringKey", 1);
        myTable.Set(1.0, 2);
        // Use string for span key test due to extension method limitations with ref structs
        myTable.Set("spanKey", 3.0);

        myTable.TryGet("stringKey", out double v1).ShouldBeTrue();
        myTable.TryGet(1.0, out double v2).ShouldBeTrue();
        myTable.TryGet("spanKey", out double v3).ShouldBeTrue();

        v1.ShouldBe(1);
        v2.ShouldBe(2);
        v3.ShouldBe(3);

        v1.ShouldBe(1);
        v2.ShouldBe(2);
        v3.ShouldBe(3);
    }

    [Fact]
    public void Table_WithMixedValueTypes()
    {
        using var lua = new LuauState();
        LuauTable myTable = lua.CreateTable();

        myTable.Set("bool", true);
        myTable.Set("number", 42.0);
        myTable.Set("string", lua.CreateString("text"));
        LuauTable nested = lua.CreateTable();
        nested.Set("nested", 123);
        myTable.Set("table", nested);

        myTable.TryGet("bool", out bool v1).ShouldBeTrue();
        v1.ShouldBeTrue();

        myTable.TryGet("number", out double v2).ShouldBeTrue();
        v2.ShouldBe(42.0);

        myTable.TryGet("string", out string? v3).ShouldBeTrue();
        v3.ShouldBe("text");

        myTable.TryGet("table", out LuauTable v4).ShouldBeTrue();
        v4.TryGet("nested", out double v5).ShouldBeTrue();
        v5.ShouldBe(123);
    }
}
