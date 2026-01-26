using System.Runtime.CompilerServices;

namespace Darp.Luau.Generator.Tests;

public static class VerifySettingsInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifySourceGenerators.Initialize();
    }
}

public sealed class VerifySettingsTests
{
    [Fact]
    public Task RunVerifyChecks() => VerifyChecks.Run();
}
