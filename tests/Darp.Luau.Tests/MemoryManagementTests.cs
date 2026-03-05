using Darp.Luau.Utils;
using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class MemoryManagementTests
{
    [Fact]
    public void LuauValue_Dispose_ShouldReleaseTrackedReference()
    {
        using var state = new LuauState();
        using (LuauTable table = state.CreateTable())
        {
            table.Set("v", 1);
            state.Globals.Set("payload", table);
        }

        int baselineActiveReferences = state.MemoryStatistics.ActiveRegistryReferences;

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences + 1);

        value.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);
    }

    [Fact]
    public void LuauValue_TryGetBuffer_ShouldCloneReferenceOwnership()
    {
        using var state = new LuauState();
        byte[] expected = [0x01, 0x02, 0x03, 0x04];
        state.Globals.Set("payload", expected);

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        value.TryGet(out LuauBuffer buffer).ShouldBeTrue();

        value.Dispose();

        using (buffer)
        {
            buffer.TryGet(out byte[] bytes).ShouldBeTrue();
            bytes.ShouldBe(expected);
        }
    }

    [Fact]
    public void Globals_Dispose_ShouldNotInvalidateStateGlobalTable()
    {
        using var state = new LuauState();

        LuauTable globals = state.Globals;
        globals.Dispose();

        state.Globals.Set("value", 123);
        state.Globals.TryGet("value", out int value).ShouldBeTrue();
        value.ShouldBe(123);
    }

    [Fact]
    public void MemoryStatistics_RepeatedValueRoundtrip_ShouldReturnToBaseline()
    {
        using var state = new LuauState();
        using (LuauTable table = state.CreateTable())
        {
            table.Set("v", 1);
            state.Globals.Set("payload", table);
        }

        int baselineActiveReferences = state.MemoryStatistics.ActiveRegistryReferences;

        for (int i = 0; i < 2000; i++)
        {
            state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
            value.TryGet(out LuauTable roundtrip).ShouldBeTrue();

            roundtrip.Dispose();
            value.Dispose();
        }

        LuauMemoryStatistics stats = state.MemoryStatistics;
        stats.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);
        (stats.CreatedRegistryReferences - stats.ReleasedRegistryReferences).ShouldBe(stats.ActiveRegistryReferences);
    }

    [Fact]
    public void LuauTable_ExplicitCastToLuauValue_ShouldCloneReferenceOwnership()
    {
        using var state = new LuauState();
        using LuauTable table = state.CreateTable();
        table.Set("value", 123);

        int baselineActiveReferences = state.MemoryStatistics.ActiveRegistryReferences;

        LuauValue value = (LuauValue)table;
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences + 1);

        value.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);

        table.GetNumber("value").ShouldBe(123);
    }

    [Fact]
    public void LuauValue_TryGetLuauTable_ShouldCloneReferenceOwnership()
    {
        using var state = new LuauState();
        using (LuauTable table = state.CreateTable())
        {
            table.Set("v", 1);
            state.Globals.Set("payload", table);
        }

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        int referencesAfterValue = state.MemoryStatistics.ActiveRegistryReferences;

        value.TryGet(out LuauTable tableAlias).ShouldBeTrue();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue + 1);

        tableAlias.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue);

        value.TryGet(out LuauTable secondAlias).ShouldBeTrue();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue + 1);
        secondAlias.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue);

        value.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue - 1);
    }

    [Fact]
    public void LuauTable_ToIntoLuau_ShouldBorrowWithoutExtraReferenceTracking()
    {
        using var state = new LuauState();
        using LuauTable table = state.CreateTable();

        int baselineActiveReferences = state.MemoryStatistics.ActiveRegistryReferences;
        state.Globals.Set("payload", table);

        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);
    }

    [Fact]
    public void CrossState_ReferenceUsage_ShouldThrowInvalidOperationException()
    {
        using var stateA = new LuauState();
        using var stateB = new LuauState();
        using LuauTable tableA = stateA.CreateTable();

        Should.Throw<InvalidOperationException>(() => stateB.Globals.Set("payload", tableA));

        LuauValue valueA = (LuauValue)tableA;
        try
        {
            Should.Throw<InvalidOperationException>(() => stateB.Globals.Set("payload2", valueA));
        }
        finally
        {
            valueA.Dispose();
        }
    }

    [Fact]
    public void LuauValue_TryGetString_Twice_ShouldSucceed()
    {
        using var state = new LuauState();
        state.Globals.Set("payload", "hello");

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        try
        {
            value.TryGet(out string? first).ShouldBeTrue();
            first.ShouldBe("hello");

            value.TryGet(out string? second).ShouldBeTrue();
            second.ShouldBe("hello");
        }
        finally
        {
            value.Dispose();
        }
    }

    [Fact]
    public void LuauValue_TryGetString_ShouldNotReleaseReferenceUntilDispose()
    {
        using var state = new LuauState();
        state.Globals.Set("payload", "hello");

        int baselineActiveReferences = state.MemoryStatistics.ActiveRegistryReferences;
        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        int referencesAfterValue = state.MemoryStatistics.ActiveRegistryReferences;
        referencesAfterValue.ShouldBe(baselineActiveReferences + 1);

        value.TryGet(out string? text).ShouldBeTrue();
        text.ShouldBe("hello");
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(referencesAfterValue);

        value.Dispose();
        state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(baselineActiveReferences);
    }

    [Fact]
    public void LuauValue_ToString_String_ShouldBeNonDestructive()
    {
        using var state = new LuauState();
        state.Globals.Set("payload", "hello");

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        try
        {
            value.ToString().ShouldBe("hello");
            value.TryGet(out string? afterToString).ShouldBeTrue();
            afterToString.ShouldBe("hello");
        }
        finally
        {
            value.Dispose();
        }
    }

    [Fact]
    public void LuauValue_TryGetBuffer_Twice_ShouldSucceed()
    {
        using var state = new LuauState();
        byte[] expected = [0x01, 0x02, 0x03, 0x04];
        state.Globals.Set("payload", expected);

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        try
        {
            value.TryGet(out byte[]? first).ShouldBeTrue();
            first.ShouldNotBeNull();
            first.ShouldBe(expected);

            value.TryGet(out byte[]? second).ShouldBeTrue();
            second.ShouldNotBeNull();
            second.ShouldBe(expected);
        }
        finally
        {
            value.Dispose();
        }
    }

    [Fact]
    public void LuauValue_ToString_Buffer_ShouldBeNonDestructive()
    {
        using var state = new LuauState();
        byte[] expected = [0x01, 0x02, 0x03];
        state.Globals.Set("payload", expected);

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        try
        {
            value.ToString().ShouldBe(Convert.ToHexString(expected));
            value.TryGet(out byte[]? afterToString).ShouldBeTrue();
            afterToString.ShouldNotBeNull();
            afterToString.ShouldBe(expected);
        }
        finally
        {
            value.Dispose();
        }
    }

    [Fact]
    public void LuauValue_ToString_Userdata_ShouldBeNonDestructive()
    {
        using var state = new LuauState();
        var payload = new ValueUserdata { Value = 123 };
        state.Globals.Set("payload", payload);

        state.Globals.TryGetLuauValue("payload", out LuauValue value).ShouldBeTrue();
        try
        {
            _ = value.ToString();

            value.TryGet(out LuauUserdata userdata).ShouldBeTrue();
            using (userdata)
            {
                userdata.TryGetManaged(out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
                ReferenceEquals(payload, resolved).ShouldBeTrue();
            }
        }
        finally
        {
            value.Dispose();
        }
    }
}
