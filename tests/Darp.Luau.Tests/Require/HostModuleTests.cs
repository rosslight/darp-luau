using Shouldly;

namespace Darp.Luau.Tests.Require;

public sealed class HostModuleTests
{
    [Fact]
    public void ScriptModule_ShouldRequireRegisteredHostModule()
    {
        using var state = new LuauState();
        state.RegisterModule("myHost", static (_, in module) => module.Set("answer", 42));

        const string strSource = """return require("myHost").answer""";
        int result = state.Load(strSource).Execute<int>();

        result.ShouldBe(42);
    }

    [Fact]
    public void ScriptFile_ShouldRequireRegisteredHostModule()
    {
        var fs = new FakeFileSystem([
            ("./main.luau", """return require("myHost").answer"""),
        ]);

        using var state = new LuauState(LuauLibraries.All, fs);
        state.RegisterModule("myHost", static (_, in module) => module.Set("answer", 42));
        state.EnableScriptModules();

        int result = state.LoadFile("./main.luau").Execute<int>();

        result.ShouldBe(42);
    }

    [Fact]
    public void ScriptModule_RequiredHostModuleNotPresent_ShouldThrow()
    {
        using var state = new LuauState();
        state.RegisterModule("myHost", static (_, in module) => module.Set("answer", 42));

        const string strSource = """return require("myOtherHost")""";
        LuaException exception = Should.Throw<LuaException>(() => state.Load(strSource).Execute<int>());

        exception.Message.ShouldContain("module 'myOtherHost' is not registered");
    }
}
