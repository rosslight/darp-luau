using Shouldly;

namespace Darp.Luau.Tests;

public sealed class LibraryLoadingTests
{
    [Fact]
    public void BuiltinLibraries_None_ShouldKeepBaseAndTable()
    {
        using var state = new LuauState(0);

        state.EnabledLibraries.ShouldBe(LuauLibraries.Base | LuauLibraries.Table);
        state.Globals.ContainsKey("pcall").ShouldBeTrue();
        state.Globals.ContainsKey("table").ShouldBeTrue();
        state.Globals.ContainsKey("error").ShouldBeTrue();
        state.Globals.ContainsKey("math").ShouldBeFalse();
    }

    [Fact]
    public void BuiltinLibraries_ShouldLoadOnlyRequestedOptionalLibraries()
    {
        using var state = new LuauState(LuauLibraries.Math | LuauLibraries.Vector);

        state.EnabledLibraries.ShouldBe(
            LuauLibraries.Base | LuauLibraries.Table | LuauLibraries.Math | LuauLibraries.Vector
        );
        state.Globals.ContainsKey("math").ShouldBeTrue();
        state.Globals.ContainsKey("vector").ShouldBeTrue();
        state.Globals.ContainsKey("utf8").ShouldBeFalse();
    }

    [Fact]
    public void CustomLibrary_ShouldBeAvailableAsGlobalTable()
    {
        using var state = new LuauState();
        state.OpenLibrary(
            "game",
            static (state, in library) =>
            {
                library.Set("answer", 42);

                using LuauFunction add = state.CreateFunctionBuilder(static args =>
                {
                    if (!args.TryReadNumber(1, out int a, out string? error))
                        return LuauReturn.Error(error);
                    if (!args.TryReadNumber(2, out int b, out error))
                        return LuauReturn.Error(error);
                    return LuauReturn.Ok(a + b);
                });
                library.Set("add", add);
            }
        );
        state.DoString(
            """
            sum = game.add(4, 7)
            answer = game.answer
            """
        );

        state.Globals.TryGet("sum", out int sum).ShouldBeTrue();
        sum.ShouldBe(11);
        state.Globals.TryGet("answer", out int answer).ShouldBeTrue();
        answer.ShouldBe(42);
    }

    [Fact]
    public void CustomLibrary_Conflict_ShouldThrow()
    {
        using var state = new LuauState();
        state.OpenLibrary("game", static (_, in _) => { });

        Should.Throw<InvalidOperationException>(() => state.OpenLibrary("game", static (_, in _) => { }));
    }

    [Fact]
    public void RegisterLibrary_ShouldAllowRuntimeRegistration()
    {
        using var state = new LuauState();

        state.OpenLibrary("game", static (_, in library) => library.Set("answer", 99));
        state.DoString("answer = game.answer");

        state.Globals.TryGet("answer", out int answer).ShouldBeTrue();
        answer.ShouldBe(99);
    }
}
