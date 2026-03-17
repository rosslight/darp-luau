using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Darp.Luau.Generator.Tests;

public static partial class VerifySettingsInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.AddScrubber(builder =>
        {
            string text = builder.ToString();
            text = InterceptLocationRegex()
                .Replace(text, """InterceptsLocationAttribute(1, "ScrubbedInterceptLocation")""");
            builder.Clear();
            builder.Append(text);
        });
        VerifySourceGenerators.Initialize();
    }

    [GeneratedRegex("""InterceptsLocationAttribute\(1,\s*"[^"]+"\)""")]
    private static partial Regex InterceptLocationRegex();
}

public sealed class VerifySettingsTests
{
    [Fact]
    public Task RunVerifyChecks() => VerifyChecks.Run();
}
