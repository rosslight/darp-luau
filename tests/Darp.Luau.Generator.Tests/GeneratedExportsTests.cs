namespace Darp.Luau.Generator.Tests;

public class GeneratedExportsTests
{
    [Fact]
    public async Task ValidLibraryAndUserdata_ShouldReportNoDiagnostics()
    {
        const string code = """
            using Darp.Luau;

            [LuauUserdata]
            public sealed partial class Player
            {
                [LuauMember("name")]
                public string Name { get; set; } = "unknown";

                [LuauMember("rename")]
                public void Rename(string name) => Name = name;
            }

            [LuauLibrary("game")]
            public static partial class GameLibrary
            {
                [LuauMember("answer")]
                public static int Answer => 42;

                [LuauMember("create_player")]
                public static Player CreatePlayer(string name) => new() { Name = name };
            }
            """;

        await VerifyHelper.VerifyGeneratedExports(code);
    }

    [Fact]
    public async Task PartialLibraryAcrossFiles_ShouldReportNoDiagnostics()
    {
        string[] sources =
        [
            """
                using Darp.Luau;

                [LuauLibrary("mathx")]
                public static partial class MathX
                {
                    [LuauMember("pi")]
                    public static double Pi => 3.14;
                }
                """,
            """
                using Darp.Luau;

                public static partial class MathX
                {
                    [LuauMember("clamp")]
                    public static int Clamp(int value, int min, int max) => value;
                }
                """,
        ];

        await VerifyHelper.VerifyGeneratedExports(sources);
    }

    [Fact]
    public async Task NonPartialExportTypes_ShouldFail()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("game")]
            public static class GameLibrary
            {
            }

            [LuauUserdata]
            public sealed class Player
            {
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsWithErrors(code);
    }

    [Fact]
    public async Task LibraryPathConflictsAndInvalidPaths_ShouldFail()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("aa")]
            public static partial class AnalyzerLibrary
            {
                [LuauMember("Field")]
                public static int Field => 1;

                [LuauMember("Field.u8")]
                public static int CreateU8() => 8;

                [LuauMember("Field..u16")]
                public static int CreateU16() => 16;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsWithErrors(code);
    }

    [Fact]
    public async Task LibraryPropertyAndMethodShapeDiagnostics_ShouldFail()
    {
        const string code = """
            using Darp.Luau;

            [LuauLibrary("game")]
            public static partial class GameLibrary
            {
                [LuauMember("current")]
                public static int Current { get; set; }

                [LuauMember("create")]
                public static int Create(int value = 0) => value;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsWithErrors(code);
    }

    [Fact]
    public async Task UserdataPropertyAndManualHookConflicts_ShouldFail()
    {
        const string code = """
            using System;
            using System.Collections.Generic;
            using Darp.Luau;

            [LuauUserdata]
            public sealed partial class Player : ILuauUserData<Player>
            {
                [LuauMember("stats")]
                public Dictionary<string, int> Stats { get; } = new();

                [LuauMember("stats.total")]
                public int Total => 1;

                public static LuauReturnSingle OnIndex(Player self, in LuauState state, in ReadOnlySpan<char> fieldName) => LuauReturnSingle.NotHandled;
                public static LuauOutcome OnSetIndex(Player self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName) => LuauOutcome.NotHandledError;
                public static LuauReturn OnMethodCall(Player self, LuauArgs functionArgs, in ReadOnlySpan<char> methodName) => LuauReturn.NotHandledError;
            }
            """;

        await VerifyHelper.VerifyGeneratedExportsWithErrors(code);
    }
}
