using Shouldly;

namespace Darp.Luau.Tests;

public sealed class GeneratedLibraryRegistrationTests
{
    [Fact]
    public void GeneratedStaticLibraryRegister_ShouldExposeValuesAndFunctions()
    {
        using var state = new LuauState();

        ArcadeLibrary.Register(state);
        state.Load("sum = arcade.add_score(40, 2); tokens = arcade.tokens; difficulty = arcade.difficulty").Execute();

        state.Globals.TryGet("sum", out int sum).ShouldBeTrue();
        sum.ShouldBe(42);
        state.Globals.TryGet("tokens", out int tokens).ShouldBeTrue();
        tokens.ShouldBe(7);
        state.Globals.TryGet("difficulty", out int difficulty).ShouldBeTrue();
        difficulty.ShouldBe((int)ArcadeDifficulty.Hard);
    }

    [Fact]
    public void GeneratedLibraryRegister_ShouldCreateNestedTables()
    {
        using var state = new LuauState();
        var workshop = new WorkshopLibrary();

        workshop.Register(state);
        state.Load("bundle = workshop.tools.hammer(5)").Execute();

        state.Globals.TryGet("bundle", out int bundle).ShouldBeTrue();
        bundle.ShouldBe(10);
    }

    [Fact]
    public void GeneratedLibraryRegister_ShouldRoundtripManagedUserdata()
    {
        using var state = new LuauState();
        var guild = new GuildLibrary();

        guild.Register(state);
        state.Load("champion = guild.heroes.create('Ada')").Execute();

        state.Globals.TryGetUserdata("champion", out HeroCard? champion).ShouldBeTrue();
        champion.ShouldNotBeNull();
        champion.Name.ShouldBe("Ada");
    }

    [Fact]
    public void GeneratedLibraryRegister_ShouldPreserveDuplicateGlobalCheck()
    {
        using var state = new LuauState();

        ArcadeLibrary.Register(state);

        Should.Throw<InvalidOperationException>(() => ArcadeLibrary.Register(state));
    }
}

public enum ArcadeDifficulty
{
    Normal = 1,
    Hard = 2,
}

[LuauLibrary("arcade")]
public static partial class ArcadeLibrary
{
    [LuauMember("tokens")]
    public static int Tokens => 7;

    [LuauMember("difficulty")]
    public static ArcadeDifficulty Difficulty => ArcadeDifficulty.Hard;

    [LuauMember("add_score")]
    public static int AddScore(int current, int bonus) => current + bonus;
}

[LuauLibrary("workshop")]
public sealed partial class WorkshopLibrary
{
    [LuauMember("tools.hammer")]
    public int MakeHammer(int size) => size * 2;
}

[LuauLibrary("guild")]
public sealed partial class GuildLibrary
{
    [LuauMember("heroes.create")]
    public HeroCard CreateHero(string name) => new(name);
}

public sealed class HeroCard(string name) : ILuauUserData<HeroCard>
{
    public string Name { get; } = name;

    public static LuauReturnSingle OnIndex(HeroCard self, in LuauState state, in ReadOnlySpan<char> fieldName) =>
        fieldName switch
        {
            "name" => LuauReturnSingle.Ok(self.Name),
            _ => LuauReturnSingle.NotHandled,
        };

    public static LuauOutcome OnSetIndex(HeroCard self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName) =>
        LuauOutcome.NotHandledError;

    public static LuauReturn OnMethodCall(HeroCard self, LuauArgs functionArgs, in ReadOnlySpan<char> methodName) =>
        LuauReturn.NotHandledError;
}
