using System.Text;
using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class RequireByStringTests
{
    private const string ScriptPath = "./Require/scripts";

    private static bool ContainsAll(IEnumerable<string> values, IEnumerable<string> expected)
    {
        foreach(string strExpected in expected)
        {
            if (!values.Any(v => v.Contains(strExpected)))
                return false;
        }
        return true;
    }

    private static void ResultsShouldContainAll(LuauValue[] results, string[] expected)
    {
        results[0].TryGet(out bool bResult).ShouldBeTrue();
        string strResult = bResult.ToString().ToLower();

        if (results[1].TryGet(out LuauTable table))
        {
            IEnumerable<string> values = table.IPairs()
                .Where(p => p.Value.Type == LuauValueType.String)
                .Select(p => p.Value.As<string>()!);
            ContainsAll(values.Prepend(strResult), expected).ShouldBeTrue();
        }
        else if (results[1].TryGet(out string? strValue))
        {
            ContainsAll([strResult, strValue], expected).ShouldBeTrue();
        }            
    }

    private static string SourceForRunProtectedRequire(string strPath) => $"return pcall(function() return require(\"{strPath}\") end)";


    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void PathNormalization()
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

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSimpleRelativePath()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSimpleRelativePathWithinPcall()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/dependency");
        string strSource = $"return pcall(require, \"{strPath}\")";
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireRelativeToRequiringFile()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/module");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency", "required into module"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireLua()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/lua_dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from lua_dependency"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireInitLuau()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/luau");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from init.luau"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireInitLua()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/lua");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from init.lua"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSubmoduleUsingSelfIndirectly()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/nested_module_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from submodule"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSubmoduleUsingSelfDirectly()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/nested");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from submodule"]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void CannotRequireInitLuauDirectly()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/nested/init");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "could not resolve child component \"init\""]);
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireNestedInits()
    {
        using var state = new LuauState();
        state.EnableRequireByString();
        
        string strPath = Path.Combine(ScriptPath, "without_config/nested_inits_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from nested_inits/init", "required into module"]);
    }

    //TODO LuaException cannot been catched!
    //
    // /// <summary>See https://github.com/luau-lang/luau</summary>
    // [Fact]
    // public void RequireWithFileAmbiguity()
    // {
    //     using var state = new LuauState();
    //     state.EnableRequireByString();
        
    //     string strPath = Path.Combine(ScriptPath, "without_config/ambiguous_file_requirer");
    //     string strSource = SourceForRunProtectedRequire(strPath);
    //     string strChunkName = "=stdin";
    //     LuaException exc = Should.Throw<LuaException>(() =>
    //     {
    //         state.DoString(Encoding.UTF8.GetBytes(strSource), nNumExpectedRetValues: 2, Encoding.UTF8.GetBytes(strChunkName));
    //     });
    //     exc.Message.ShouldContain("error requiring module \"./ambiguous/file/dependency\": could not resolve child component \"dependency\" (ambiguous)");
    // }




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