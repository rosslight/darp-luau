using System.Text;
using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class RequireByStringTests
{
    private const string ScriptPath = "./Require/scripts";

    /// <summary>See https://github.com/luau-lang/luau</summary>
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
            LuauRequireByString.Navigator.NormalizePath(Input).ShouldBe(Expected);
        }
    }

    [Fact]
    public void Just_enable_require_by_string()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        state.Globals.TryGet("require", out LuauValue value).ShouldBeTrue();
        value.Type.ShouldBe(LuauValueType.Function);
    }

    [Fact]
    public void Result_in_globals()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        string strFileName = Path.Combine(ScriptPath, "main.luau");
        string strChunkName = '@' + strFileName;
        state.DoString(File.ReadAllBytes(strFileName), Encoding.UTF8.GetBytes(strChunkName));
        
        state.Globals.TryGet("result", out int nResult).ShouldBeTrue();
        nResult.ShouldBe(15);
    }

    [Fact]
    public void Results_as_return_values()
    {
        using var state = new LuauState();
        state.EnableRequireByString();

        string strFileName = Path.Combine(ScriptPath, "main.luau");
        string strChunkName = '@' + strFileName;
        LuauValue[] results = state.DoString(File.ReadAllBytes(strFileName), nNumExpectedRetValues: 3, Encoding.UTF8.GetBytes(strChunkName));
        
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
}