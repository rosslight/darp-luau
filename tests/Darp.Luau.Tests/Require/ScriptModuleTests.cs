using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class ScriptModuleTests
{
    private static string PathForRequireStringLiteral(string strPath) => strPath.Replace('\\', '/');

    private static string SourceForRunProtectedRequire(string strPath) =>
        $"return pcall(function() return require(\"{PathForRequireStringLiteral(strPath)}\") end)";

    [Fact]
    public void RequireSimpleRelativePath()
    {
        var fs = new FakeFileSystem([
            ("./main.luau", """return require("./without_config/dependency")"""),
            ("./without_config/dependency.luau", """return {"result from dependency"}"""),
        ]);

        using var state = new LuauState(LuauLibraries.All, fs);
        state.EnableScriptModules();

        using LuauTable result = state.LoadFile("./main.luau").Execute<LuauTable>();
        result.GetUtf8String(1).ShouldBe("result from dependency");
    }
}
