using System.Text;
using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class RequireByStringTests
{
    private const string ScriptPath = "./Require/scripts";

    [Fact]
    public void Path_normalization()
    {
        string strPrefix = OperatingSystem.IsWindows() ? "C:/" : "/";

        (string Input, string Expected)[] cases =
        [
            // 1. Basic formatting checks
            ("", "./"),
            (".", "./"),
            ("..", "../"),
            ("a/relative/path", "./a/relative/path"),
            
            // 2. Paths containing extraneous '.' and '/' symbols
            ("./remove/extraneous/symbols/", "./remove/extraneous/symbols"),
            ("./remove/extraneous//symbols", "./remove/extraneous/symbols"),
            ("./remove/extraneous/symbols/.", "./remove/extraneous/symbols"),
            ("./remove/extraneous/./symbols", "./remove/extraneous/symbols"),

            ("../remove/extraneous/symbols/", "../remove/extraneous/symbols"),
            ("../remove/extraneous//symbols", "../remove/extraneous/symbols"),
            ("../remove/extraneous/symbols/.", "../remove/extraneous/symbols"),
            ("../remove/extraneous/./symbols", "../remove/extraneous/symbols"),

            (strPrefix + "remove/extraneous/symbols/", strPrefix + "remove/extraneous/symbols"),
            (strPrefix + "remove/extraneous//symbols", strPrefix + "remove/extraneous/symbols"),
            (strPrefix + "remove/extraneous/symbols/.", strPrefix + "remove/extraneous/symbols"),
            (strPrefix + "remove/extraneous/./symbols", strPrefix + "remove/extraneous/symbols"),


            // 3. Paths containing '..'
            // a. '..' removes the erasable component before it
            ("./remove/me/..", "./remove"),
            ("./remove/me/../", "./remove"),

            ("../remove/me/..", "../remove"),
            ("../remove/me/../", "../remove"),

            (strPrefix + "remove/me/..", strPrefix + "remove"),
            (strPrefix + "remove/me/../", strPrefix + "remove"),

            // b. '..' stays if path is relative and component is non-erasable
            ("./..", "../"),
            ("./../", "../"),

            ("../..", "../../"),
            ("../../", "../../"),

            // c. '..' disappears if path is absolute and component is non-erasable
            (strPrefix + "..", strPrefix),
        ];

        foreach((string Input, string Expected) in cases)
        {
            LuauRequireByString.NormalizePath(Input).ShouldBe(Expected);
        }
    }

    [Fact]
    public void Enable_require_by_string()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        state.Globals.TryGet("require", out LuauValue value).ShouldBeTrue();
        value.Type.ShouldBe(LuauValueType.Function);
    }

    // [Fact]
    // public void Use_require_by_string()
    // {
    //     using var state = new LuauState();
    //     state.EnableRequireByString();

    //     state.DoString(File.ReadAllBytes(Path.Combine(ScriptPath, "main.luau")), Encoding.UTF8.GetBytes("@main"));
    // }
}