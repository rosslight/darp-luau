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

    [Fact]
    public async Task Library_WithRecoverableMemberErrors_ShouldGenerateRegisterMethodAndValidMembers()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("recoverable")]
            public sealed partial class RecoverableLibrary
            {
                public int CurrentValue { get; set; }

                [LuauMember("ok")]
                public int Ok() => 1;

                [LuauMember("unsupported")]
                public int Unsupported(int value = 0) => value;

                [LuauMember("duplicate")]
                public int DuplicateOne() => 1;

                [LuauMember("duplicate")]
                public int DuplicateTwo() => 2;

                [LuauMember("Field")]
                public int CreateField() => 1;

                [LuauMember("Field.u8")]
                public int CreateU8() => 8;

                [LuauMember("bad..path")]
                public int BadPath() => 0;

                [LuauMember("current")]
                public int Current => CurrentValue;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSourceWithErrors(code);
    }

    [Fact]
    public async Task Library_WithFatalTypeErrors_ShouldNotGenerateRegisterMethod()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("nonpartial")]
            public static class NonPartialLibrary
            {
                [LuauMember("ok")]
                public static int Ok() => 1;
            }

            [LuauLibrary("ledger")]
            public sealed partial class LedgerLibrary
            {
                public const string LuauLibraryName = "ledger";

                [LuauMember("ok")]
                public int Ok() => 1;
            }

            [LuauLibrary(" ")]
            public static partial class BlankLibraryName
            {
                [LuauMember("ok")]
                public static int Ok() => 1;
            }

            [LuauLibrary("generic")]
            public sealed partial class GenericLibrary<T>
            {
            }

            public static partial class Container
            {
                [LuauLibrary("nested")]
                public static partial class NestedLibrary
                {
                }
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSourceWithErrors(code);
    }

    [Fact]
    public async Task Library_WithBracketAccessNamesAndCollidingPathText_ShouldGenerateSafeLocalNames()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("demo")]
            public static partial class DemoLibrary
            {
                [LuauMember("foo-bar")]
                public static int FooBar() => 1;

                [LuauMember("tools.create item")]
                public static int CreateItem() => 2;

                [LuauMember("a.b_c")]
                public static int One() => 3;

                [LuauMember("a_b.c")]
                public static int Two() => 4;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsSourceWithErrors(code);
    }
}
