using Darp.Luau.Internal.Require;
using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class FileUtilsTests
{
#if TARGET_WINDOWS
    private const string StrPrefix = "C:/";
#else
    private const string StrPrefix = "/";
#endif

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Theory]
    // 1. Basic formatting checks
    [InlineData("", "./")]
    [InlineData(".", "./")]
    [InlineData("..", "../")]
    [InlineData("a/relative/path", "./a/relative/path")]
    // 2. Paths containing extraneous '.' and '/' symbols
    [InlineData("./remove/extraneous/symbols/", "./remove/extraneous/symbols")]
    [InlineData("./remove/extraneous//symbols", "./remove/extraneous/symbols")]
    [InlineData("./remove/extraneous/symbols/.", "./remove/extraneous/symbols")]
    [InlineData("./remove/extraneous/./symbols", "./remove/extraneous/symbols")]
    [InlineData("../remove/extraneous/symbols/", "../remove/extraneous/symbols")]
    [InlineData("../remove/extraneous//symbols", "../remove/extraneous/symbols")]
    [InlineData("../remove/extraneous/symbols/.", "../remove/extraneous/symbols")]
    [InlineData("../remove/extraneous/./symbols", "../remove/extraneous/symbols")]
    [InlineData(StrPrefix + "remove/extraneous/symbols/", StrPrefix + "remove/extraneous/symbols")]
    [InlineData(StrPrefix + "remove/extraneous//symbols", StrPrefix + "remove/extraneous/symbols")]
    [InlineData(StrPrefix + "remove/extraneous/symbols/.", StrPrefix + "remove/extraneous/symbols")]
    [InlineData(StrPrefix + "remove/extraneous/./symbols", StrPrefix + "remove/extraneous/symbols")]
    // 3. Paths containing '..'
    // a. '..' removes the erasable component before it
    [InlineData("./remove/me/..", "./remove")]
    [InlineData("./remove/me/../", "./remove")]
    [InlineData("../remove/me/..", "../remove")]
    [InlineData("../remove/me/../", "../remove")]
    [InlineData(StrPrefix + "remove/me/..", StrPrefix + "remove")]
    [InlineData(StrPrefix + "remove/me/../", StrPrefix + "remove")]
    // b. '..' stays if path is relative and component is non-erasable    [InlineData]
    [InlineData("./..", "../")]
    [InlineData("./../", "../")]
    [InlineData("../..", "../../")]
    [InlineData("../../", "../../")]
    // c. '..' disappears if path is absolute and component is non-erasable
    [InlineData(StrPrefix + "..", StrPrefix)]
    public void PathNormalization(string input, string expected)
    {
        LuauModuleNavigator.NormalizePath(input).ShouldBe(expected);
    }
}
