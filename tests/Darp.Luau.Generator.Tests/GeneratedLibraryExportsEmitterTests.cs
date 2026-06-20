namespace Darp.Luau.Generator.Tests;

public sealed class GeneratedLibraryExportsEmitterTests
{
    [Fact]
    public async Task StaticLibrary_ShouldGenerateRegisterMethod()
    {
        const string code = """
            using Darp.Luau;

            public enum Difficulty
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
                public static Difficulty Difficulty => Difficulty.Hard;

                [LuauMember("add_score")]
                public static int AddScore(int current, int bonus) => current + bonus;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSource(code);
    }

    [Fact]
    public async Task InstanceLibrary_ShouldGenerateRegisterMethod()
    {
        const string code = """
            using System;
            using Darp.Luau;

            public sealed class HeroCard : ILuauUserData<HeroCard>
            {
                public HeroCard(string name) => Name = name;

                public string Name { get; }

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

            [LuauLibrary("guild")]
            public sealed partial class GuildLibrary
            {
                [LuauMember("heroes.create")]
                public HeroCard CreateHero(string name) => new(name);
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSource(code);
    }

    [Fact]
    public async Task Library_WithInvalidMember_ShouldGenerateValidMembers()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("arcade")]
            public static partial class ArcadeLibrary
            {
                [LuauMember("tokens")]
                public static int Tokens => 7;

                [LuauMember("unsupported")]
                public static int Unsupported(int value = 0) => value;

                [LuauMember("add_score")]
                public static int AddScore(int current, int bonus) => current + bonus;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSourceWithErrors(code);
    }
}
