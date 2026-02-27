using Darp.Luau.Utils;
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
}
