using Shouldly;

namespace Darp.Luau.Tests;

public sealed class StandardLibraryAndModuleLoadingTests : IDisposable
{
    private readonly List<LuauState> _states = [];

    [Fact]
    public void BuiltinLibraries_None_ShouldKeepBaseAndTable()
    {
        LuauState state = CreateState(0);

        state.EnabledLibraries.ShouldBe(LuauLibraries.Base | LuauLibraries.Table);
        state.Globals.ContainsKey("pcall").ShouldBeTrue();
        state.Globals.ContainsKey("table").ShouldBeTrue();
        state.Globals.ContainsKey("error").ShouldBeTrue();
        state.Globals.ContainsKey("math").ShouldBeFalse();
    }

    [Fact]
    public void BuiltinLibraries_ShouldLoadOnlyRequestedOptionalLibraries()
    {
        LuauState state = CreateState(LuauLibraries.Math | LuauLibraries.Vector);

        state.EnabledLibraries.ShouldBe(
            LuauLibraries.Base | LuauLibraries.Table | LuauLibraries.Math | LuauLibraries.Vector
        );
        state.Globals.ContainsKey("math").ShouldBeTrue();
        state.Globals.ContainsKey("vector").ShouldBeTrue();
        state.Globals.ContainsKey("utf8").ShouldBeFalse();
    }

    [Fact]
    public void CustomModule_ShouldBeAvailableThroughRequire()
    {
        LuauState state = CreateState();
        state.RegisterModule(
            "game",
            static (state, in module) =>
            {
                module.Set("answer", 42);

                using LuauFunction add = state.CreateFunctionBuilder(static args =>
                {
                    if (!args.TryReadNumber(1, out int a, out string? error))
                        return LuauReturn.Error(error);
                    if (!args.TryReadNumber(2, out int b, out error))
                        return LuauReturn.Error(error);
                    return LuauReturn.Ok(a + b);
                });
                module.Set("add", add);
            }
        );
        state
            .Load(
                """
                local game = require("game")
                sum = game.add(4, 7)
                answer = game.answer
                """
            )
            .Execute();

        state.Globals.TryGet("sum", out int sum).ShouldBeTrue();
        sum.ShouldBe(11);
        state.Globals.TryGet("answer", out int answer).ShouldBeTrue();
        answer.ShouldBe(42);
    }

    [Fact]
    public void CustomModule_Conflict_ShouldThrow()
    {
        LuauState state = CreateState();
        state.RegisterModule("game", static (_, in _) => { });

        Should.Throw<InvalidOperationException>(() => state.RegisterModule("game", static (_, in _) => { }));
    }

    [Fact]
    public void CustomModule_ShouldLoadOnceAndCacheTable()
    {
        LuauState state = CreateState();
        int loadCount = 0;

        state.RegisterModule(
            "game",
            (_, in module) =>
            {
                loadCount++;
                module.Set("answer", 99);
            }
        );
        state
            .Load(
                """
                local first = require("game")
                local second = require("game")
                same_table = first == second
                answer = second.answer
                """
            )
            .Execute();

        state.Globals.TryGet("answer", out int answer).ShouldBeTrue();
        answer.ShouldBe(99);
        state.Globals.TryGet("same_table", out bool sameTable).ShouldBeTrue();
        sameTable.ShouldBeTrue();
        loadCount.ShouldBe(1);
    }

    [Fact]
    public void RegisterModule_Generic_ShouldUseModuleContract()
    {
        LuauState state = CreateState();
        var module = new TestModule(123);

        state.RegisterModule<TestModule>(module);
        state.Load("answer = require('typed').answer").Execute();

        state.Globals.TryGet("answer", out int answer).ShouldBeTrue();
        answer.ShouldBe(123);
        module.LoadCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("./game")]
    [InlineData("../game")]
    [InlineData("@game")]
    [InlineData("/game")]
    [InlineData("\\game")]
    [InlineData("game/module")]
    [InlineData("game\\module")]
    public void RegisterModule_ShouldRejectScriptModuleNames(string name)
    {
        LuauState state = CreateState();

        Should.Throw<ArgumentException>(() => state.RegisterModule(name, static (_, in _) => { }));
    }

    [Fact]
    public void Require_UnregisteredBareModule_ShouldFail()
    {
        LuauState state = CreateState();
        state.RegisterModule("registered", static (_, in _) => { });

        (bool success, string error) = state.Load("return pcall(require, 'missing')").Execute<bool, string>();

        success.ShouldBeFalse();
        error.ShouldContain("module 'missing' is not registered");
    }

    public void Dispose()
    {
        foreach (LuauState state in _states)
        {
            if (!state.IsDisposed)
                state.Dispose();
            state.MemoryStatistics.ActiveRegistryReferences.ShouldBe(0UL);
        }
    }

    private LuauState CreateState(LuauLibraries libraries = LuauLibraries.All)
    {
        LuauState state = new(libraries);
        _states.Add(state);
        return state;
    }

    private sealed class TestModule(int answer) : ILuauModule<TestModule>
    {
        public static string ModuleName => "typed";

        public int LoadCount { get; private set; }

        public void OnLoad(LuauState lua, in LuauTable module)
        {
            LoadCount++;
            module.Set("answer", answer);
        }
    }
}
