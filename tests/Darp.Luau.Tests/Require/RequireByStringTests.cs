using System.Text;
using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class RequireByStringTests
{
    private const string ScriptPath = "./Require/scripts";

    private static bool ContainsAll(IEnumerable<string> values, IEnumerable<string> expected)
    {
        foreach (string strExpected in expected)
        {
            if (!values.Any(v => v.Contains(strExpected)))
                return false;
        }
        return true;
    }

    private static List<string> GetStringValuesRecursively(LuauTable table)
    {
        var result = new List<string>();

        foreach (KeyValuePair<int, LuauValue> p in table.IPairs())
        {
            if (p.Value.TryGet(out string? strValue))
            {
                result.Add(strValue);
            }
            else if (p.Value.TryGet(out LuauTable subTable))
            {
                result.AddRange(GetStringValuesRecursively(subTable));
            }
        }

        return result;
    }

    private static void ResultsShouldContainAll(LuauValue[] results, string[] expected)
    {
        results.Length.ShouldBeGreaterThan(0);

        results[0].TryGet(out bool bResult).ShouldBeTrue();
        string strResult = bResult.ToString().ToLower();

        if (results.Length < 2)
        {
            ContainsAll([strResult], expected).ShouldBeTrue();
        }
        else if (results[1].TryGet(out LuauTable table))
        {
            List<string> values = GetStringValuesRecursively(table);
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

        foreach ((string Input, string Expected) in cases)
        {
            LuauRequireByString.Navigator.NormalizePath(Input).ShouldBe(Expected);
        }
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSimpleRelativePath()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSimpleRelativePathWithinPcall()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/dependency");
        string strSource = $"return pcall(require, \"{strPath}\")";
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireRelativeToRequiringFile()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/module");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency", "required into module"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireLua()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/lua_dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from lua_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireInitLuau()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/luau");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from init.luau"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireInitLua()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/lua");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from init.lua"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSubmoduleUsingSelfIndirectly()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/nested_module_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from submodule"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireSubmoduleUsingSelfDirectly()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/nested");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from submodule"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void CannotRequireInitLuauDirectly()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/nested/init");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "could not resolve child component \"init\""]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireNestedInits()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/nested_inits_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from nested_inits/init", "required into module"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireWithFileAmbiguity()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/ambiguous_file_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"./ambiguous/file/dependency\": could not resolve child component \"dependency\" (ambiguous)");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireWithDirectoryAmbiguity()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/ambiguous_directory_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"./ambiguous/directory/dependency\": could not resolve child component \"dependency\" (ambiguous)");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireAbsolutePath()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = "/an/absolute/path";
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "require path must start with a valid prefix: ./, ../, or @"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireUnprefixedPath()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = "an/unprefixed/path";
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "require path must start with a valid prefix: ./, ../, or @"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithAlias01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/src/alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithAlias02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/src/alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithParentAlias01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/src/parent_alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from other_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithParentAlias02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/src/parent_alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from other_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithAliasPointingToDirectory01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/src/directory_alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from subdirectory_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequirePathWithAliasPointingToDirectory02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/src/directory_alias_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from subdirectory_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireAliasThatDoesNotExist()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strSource = SourceForRunProtectedRequire("@this.alias.does.not.exist");
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "@this.alias.does.not.exist is not a valid alias"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void AliasHasIllegalFormat01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strSource = SourceForRunProtectedRequire("@@");
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "@@ is not a valid alias"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void AliasHasIllegalFormat02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strSource = SourceForRunProtectedRequire("@.");
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "@. is not a valid alias"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void AliasHasIllegalFormat03()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strSource = SourceForRunProtectedRequire("@..");
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "@.. is not a valid alias"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void AliasHasIllegalFormat04()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strSource = SourceForRunProtectedRequire("@");
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", " is not a valid alias"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void AliasNotParsedIfConfigsAmbiguous()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/config_ambiguity/requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("could not resolve alias \"dep\" (ambiguous configuration file)");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void CannotRequireConfigLuau()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/config_cannot_be_required/requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("could not resolve child component \".config\"");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireBoolean()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/boolean");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "false"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireBuffer()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/buffer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "buffer"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireFunction()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/function");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "function"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireNil()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/nil");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "nil"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireNumber()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/number");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "number"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireString()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/string");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "foo"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireTable()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/table");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "foo", "bar"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireThread()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/thread");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        // while thread is not supported
        Should.Throw<InvalidOperationException>(() =>
        {
            LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["true", "thread"]);
            context.LoadError.ShouldBeNullOrEmpty();
        });
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireUserdata()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/userdata");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "userdata"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireVector()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "without_config/types/vector");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        // while vector is not supported
        Should.Throw<InvalidOperationException>(() =>
        {
            LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["true", "1, 2, 3"]);
            context.LoadError.ShouldBeNullOrEmpty();
        });
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesSuccess01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/successful_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from inner_dependency", "result from outer_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesSuccess02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/successful_requirer");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["true", "result from inner_dependency", "result from outer_dependency"]);
        context.LoadError.ShouldBeNullOrEmpty();
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureCyclic01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/failing_requirer_cyclic");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@cyclicentry\": detected alias cycle (@cyclic1 -> @cyclic2 -> @cyclic3 -> @cyclic1)");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureCyclic02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/failing_requirer_cyclic");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@cyclicentry\": detected alias cycle (@cyclic1 -> @cyclic2 -> @cyclic3 -> @cyclic1)");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureMissing01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/failing_requirer_missing");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@brokenchain\": @missing is not a valid alias");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureMissing02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/failing_requirer_missing");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@brokenchain\": @missing is not a valid alias");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureDependOnInnerAlias01()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/failing_requirer_inner_dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@dependoninner\": @passthroughinner is not a valid alias");
    }

    /// <summary>See https://github.com/luau-lang/luau</summary>
    [Fact]
    public void RequireChainedAliasesFailureDependOnInnerAlias02()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/failing_requirer_inner_dependency");
        string strSource = SourceForRunProtectedRequire(strPath);
        string strChunkName = "=stdin";
        LuauValue[] results = state.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
        ResultsShouldContainAll(results, ["false", "module must return a single value"]);
        context.LoadError.ShouldNotBeNullOrEmpty();
        context.LoadError.ShouldContain("error requiring module \"@dependoninner\": @passthroughinner is not a valid alias");
    }

    [Fact]
    public void StatesNavigatingIndependently()
    {
        using var state01 = new LuauState();
        using var state02 = new LuauState();
        using var state03 = new LuauState();
        using var state04 = new LuauState();

        {
            // RequireChainedAliasesFailureMissing01
            using LuauRequireByString.Context context = state01.EnableRequire();

            string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/failing_requirer_missing");
            string strSource = SourceForRunProtectedRequire(strPath);
            string strChunkName = "=stdin";
            LuauValue[] results = state01.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["false", "module must return a single value"]);
            context.LoadError.ShouldNotBeNullOrEmpty();
            context.LoadError.ShouldContain("error requiring module \"@brokenchain\": @missing is not a valid alias");
        }

        {
            // RequireChainedAliasesFailureDependOnInnerAlias02
            using LuauRequireByString.Context context = state02.EnableRequire();

            string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/failing_requirer_inner_dependency");
            string strSource = SourceForRunProtectedRequire(strPath);
            string strChunkName = "=stdin";
            LuauValue[] results = state02.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["false", "module must return a single value"]);
            context.LoadError.ShouldNotBeNullOrEmpty();
            context.LoadError.ShouldContain("error requiring module \"@dependoninner\": @passthroughinner is not a valid alias");
        }

        {
            // RequireChainedAliasesFailureCyclic01
            using LuauRequireByString.Context context = state03.EnableRequire();

            string strPath = Path.Combine(ScriptPath, "config_tests/with_config/chained_aliases/subdirectory/failing_requirer_cyclic");
            string strSource = SourceForRunProtectedRequire(strPath);
            string strChunkName = "=stdin";
            LuauValue[] results = state03.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["false", "module must return a single value"]);
            context.LoadError.ShouldNotBeNullOrEmpty();
            context.LoadError.ShouldContain("error requiring module \"@cyclicentry\": detected alias cycle (@cyclic1 -> @cyclic2 -> @cyclic3 -> @cyclic1)");
        }

        {
            // RequireChainedAliasesSuccess02
            using LuauRequireByString.Context context = state04.EnableRequire();

            string strPath = Path.Combine(ScriptPath, "config_tests/with_config_luau/chained_aliases/subdirectory/successful_requirer");
            string strSource = SourceForRunProtectedRequire(strPath);
            string strChunkName = "=stdin";
            LuauValue[] results = state04.DoStringAndReturn(Encoding.UTF8.GetBytes(strSource), Encoding.UTF8.GetBytes(strChunkName));
            ResultsShouldContainAll(results, ["true", "result from inner_dependency", "result from outer_dependency"]);
            context.LoadError.ShouldBeNullOrEmpty();
        }
    }

    [Fact]
    public void Just_enable_require_by_string()
    {
        using var state = new LuauState();
        state.EnableRequire();

        state.Globals.TryGet("require", out LuauValue value).ShouldBeTrue();
        value.Type.ShouldBe(LuauValueType.Function);
    }

    [Fact]
    public void Result_in_globals()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strFileName = Path.Combine(ScriptPath, "main.luau");
        string strChunkName = '@' + strFileName;
        state.DoString(File.ReadAllBytes(strFileName), Encoding.UTF8.GetBytes(strChunkName));
        context.LoadError.ShouldBeNullOrEmpty();

        state.Globals.TryGet("result", out int nResult).ShouldBeTrue();
        nResult.ShouldBe(15);
    }

    [Fact]
    public void Results_as_return_values()
    {
        using var state = new LuauState();
        using LuauRequireByString.Context context = state.EnableRequire();

        string strFileName = Path.Combine(ScriptPath, "main.luau");
        string strChunkName = '@' + strFileName;
        LuauValue[] results = state.DoStringAndReturn(File.ReadAllBytes(strFileName), Encoding.UTF8.GetBytes(strChunkName));
        context.LoadError.ShouldBeNullOrEmpty();

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