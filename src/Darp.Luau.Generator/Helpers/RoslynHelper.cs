using System.CodeDom.Compiler;

namespace Darp.Luau.Generator.Helpers;

internal static class RoslynHelper
{
    public static void WriteMultiLine(this IndentedTextWriter writer, string multiLineString)
    {
        foreach (var se in multiLineString.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(se))
                writer.WriteLineNoTabs("");
            else if (se.Trim().StartsWith("#", StringComparison.Ordinal))
                writer.WriteLineNoTabs(se);
            else
                writer.WriteLine(se);
        }
    }

    public static string GetGeneratedVersionAttribute()
    {
        var generatorName = typeof(DarpLuauInterceptor).Assembly.GetName().Name;
        Version generatorVersion = typeof(DarpLuauInterceptor).Assembly.GetName().Version;
        return $"""[global::System.CodeDom.Compiler.GeneratedCodeAttribute("{generatorName}", "{generatorVersion}")]""";
    }
}
