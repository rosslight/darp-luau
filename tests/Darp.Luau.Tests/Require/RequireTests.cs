using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class RequireTests
{
    private const string ScriptPath = "./Require/scripts";

    [Fact]
    public void Result_in_globals()
    {
        using var state = new LuauState();
        state.EnableRequire(ScriptPath);

        state.DoString(File.ReadAllBytes(Path.Combine(ScriptPath, "main.luau")));
        state.Globals.TryGet("result", out int nResult).ShouldBeTrue();
        nResult.ShouldBe(15);
    }

    [Fact]
    public void Results_as_return_values()
    {
        using var state = new LuauState();
        state.EnableRequire(ScriptPath);

        LuauValue[] results = state.DoString(File.ReadAllBytes(Path.Combine(ScriptPath, "main.luau")), nNumExpectedRetValues: 3);
        
        results[0].TryGet(out int nSum).ShouldBeTrue();
        nSum.ShouldBe(3);
        
        results[1].TryGet(out int nDifference).ShouldBeTrue();
        nDifference.ShouldBe(5);

        results[2].TryGet(out LuauTable table).ShouldBeTrue();
        table.TryGet("sum", out nSum).ShouldBeTrue();
        nSum.ShouldBe(3);
        table.TryGet("difference", out nDifference).ShouldBeTrue();
        nDifference.ShouldBe(5);
    }

    [Fact]
    public void Enable_require_by_string()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        state.Globals.TryGet("require", out LuauValue value).ShouldBeTrue();        
        value.Type.ShouldBe(LuauValueType.Function);
    }

    [Fact]
    public void Use_require_by_string()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        state.DoString(File.ReadAllBytes(Path.Combine(ScriptPath, "main.luau")));
    }
}