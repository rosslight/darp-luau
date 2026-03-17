using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Platform.Logging;
using Shouldly;

namespace Darp.Luau.Generator.Tests;

public static class VerifyHelper
{
    public static Task VerifyGenerator(string source, [CallerFilePath] string? callerFilePath = null)
    {
        string fileName =
            Path.GetFileNameWithoutExtension(callerFilePath) ?? throw new ArgumentNullException(nameof(callerFilePath));
        AddReferenceAssemblyMarker<LuauState>();
        AddReferenceAssemblyMarker<ILogger>();
        return VerifyGenerator<DarpLuauInterceptor>(
            [source],
            [],
            "DBO0",
            fileName,
            task => task.ScrubGeneratedCodeAttribute(),
            new Dictionary<string, string>(),
            new Dictionary<string, string> { { "InterceptorsNamespaces", "Darp.Luau.Generator" } },
            LanguageVersion.CSharp13
        );
    }

    public static Task VerifyGeneratorWithErrors(string source, [CallerFilePath] string? callerFilePath = null)
    {
        string fileName =
            Path.GetFileNameWithoutExtension(callerFilePath) ?? throw new ArgumentNullException(nameof(callerFilePath));
        AddReferenceAssemblyMarker<LuauState>();
        AddReferenceAssemblyMarker<ILogger>();
        return VerifyGenerator<DarpLuauInterceptor>(
            [source],
            [],
            "DLUAU",
            fileName,
            task => task.ScrubGeneratedCodeAttribute(),
            new Dictionary<string, string>(),
            new Dictionary<string, string> { { "InterceptorsNamespaces", "Darp.Luau.Generator" } },
            LanguageVersion.CSharp13,
            allowCompilationErrors: true
        );
    }

    public static Task VerifyGeneratorAndAnalyzerWithErrors(
        string source,
        DiagnosticAnalyzer analyzer,
        [CallerFilePath] string? callerFilePath = null
    )
    {
        string fileName =
            Path.GetFileNameWithoutExtension(callerFilePath) ?? throw new ArgumentNullException(nameof(callerFilePath));
        AddReferenceAssemblyMarker<LuauState>();
        AddReferenceAssemblyMarker<ILogger>();
        return VerifyGenerator<DarpLuauInterceptor>(
            [source],
            [],
            "DLUAU",
            fileName,
            task => task.ScrubGeneratedCodeAttribute(),
            new Dictionary<string, string>(),
            new Dictionary<string, string> { { "InterceptorsNamespaces", "Darp.Luau.Generator" } },
            LanguageVersion.CSharp13,
            allowCompilationErrors: true,
            analyzers: ImmutableArray.Create(analyzer),
            verifyDiagnosticsSnapshot: true
        );
    }

    /// <summary>
    /// This functions ensures that the assembly is referenced and <see cref="AppDomain.GetAssemblies()"/> of the <see cref="AppDomain.CurrentDomain"/> contains this assembly
    /// </summary>
    /// <typeparam name="TMarker"> The type of an object of the assembly </typeparam>
    /// <returns> The task </returns>
    public static void AddReferenceAssemblyMarker<TMarker>()
    {
        _ = typeof(TMarker).Assembly;
    }

    public static SettingsTask ScrubGeneratedCodeAttribute(
        this SettingsTask settingsTask,
        string scrubbedVersionName = "GeneratorVersion"
    )
    {
        return settingsTask.ScrubLinesWithReplace(line =>
        {
            var regex = new Regex("""GeneratedCodeAttribute\("[^"\n]+",\s*"(?<version>\d+\.\d+\.\d+\.\d+)"\)""");
            return regex.Replace(
                line,
                match =>
                {
                    string versionToReplace = match.Groups["version"].Value;
                    return match.Value.Replace(versionToReplace, scrubbedVersionName);
                }
            );
        });
    }

    private static async Task VerifyGenerator<TGenerator>(
        string[] sources,
        AdditionalText[] additionalTexts,
        string? allowedDiagnosticCode,
        string directory,
        Func<SettingsTask, SettingsTask> configureSettingsTask,
        IReadOnlyDictionary<string, string> analyzerConfigOptions,
        IReadOnlyDictionary<string, string> features,
        LanguageVersion languageVersion = LanguageVersion.Default,
        NullableContextOptions nullableContextOptions = NullableContextOptions.Enable,
        bool allowCompilationErrors = false,
        ImmutableArray<DiagnosticAnalyzer> analyzers = default,
        bool verifyDiagnosticsSnapshot = false
    )
        where TGenerator : IIncrementalGenerator, new()
    {
        CSharpParseOptions parseOptions = CSharpParseOptions
            .Default.WithLanguageVersion(languageVersion)
            .WithFeatures(features);
        SyntaxTree[] syntaxTrees = sources.Select(x => CSharpSyntaxTree.ParseText(x, parseOptions)).ToArray();

        // Get all references of the currently loaded assembly
        PortableExecutableReference[] references = AppDomain
            .CurrentDomain.GetAssemblies() // Get currently loaded assemblies
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => a.FullName?.Contains("JetBrains") is not true)
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: Assembly.GetExecutingAssembly().FullName,
            syntaxTrees: syntaxTrees,
            references: references,
            new CSharpCompilationOptions(OutputKind.NetModule, nullableContextOptions: nullableContextOptions)
        );

        // Assert that there are no compilation errors (except for CS5001 which informs about the missing program entry)
        // Skip this check if allowCompilationErrors is true (for error tests)
        if (!allowCompilationErrors)
        {
            compilation
                .GetDiagnostics()
                .Where(x => x.Id is not "CS5001" && (x.Severity > DiagnosticSeverity.Warning || x.IsWarningAsError))
                .ShouldBeEmpty();
        }

        var generator = new TGenerator();

        var provider = new OptionsProvider(analyzerConfigOptions);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: parseOptions,
            optionsProvider: provider
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation newCompilation, out _);
        ImmutableArray<Diagnostic> analyzerDiagnostics = analyzers.IsDefaultOrEmpty
            ? []
            : await newCompilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync();

        object verificationTarget = verifyDiagnosticsSnapshot
            ? new
            {
                Diagnostics = analyzerDiagnostics
                    .Where(x => x.Id is not "CS5001")
                    .Select(ToSerializableDiagnostic)
                    .ToArray(),
            }
            : driver;
        SettingsTask settingsTask = Verify(verificationTarget).UseDirectory(Path.Join("Snapshots", directory));
        await configureSettingsTask(settingsTask);
        // Assert that there are no compilation errors (except for CS5001 which informs about the missing program entry)
        newCompilation
            .GetDiagnostics()
            .Concat(analyzerDiagnostics)
            .ShouldNotContain(
                x => IsDiagnosticInvalid(allowedDiagnosticCode, x),
                string.Join(
                    "\n",
                    newCompilation
                        .GetDiagnostics()
                        .Concat(analyzerDiagnostics)
                        .Select(x => $"[{x.Location.GetLineSpan()}]: {x.GetMessage()}")
                )
                    + "\n"
                    + string.Join("\n", driver.GetRunResult().ToReadableString())
            );
    }

    private static object ToSerializableDiagnostic(Diagnostic diagnostic)
    {
        return new
        {
            Location = diagnostic.Location.ToString(),
            Message = diagnostic.GetMessage(),
            Severity = diagnostic.Severity.ToString(),
            Descriptor = new
            {
                diagnostic.Descriptor.Id,
                diagnostic.Descriptor.Title,
                diagnostic.Descriptor.MessageFormat,
                diagnostic.Descriptor.Category,
                DefaultSeverity = diagnostic.Descriptor.DefaultSeverity.ToString(),
                diagnostic.Descriptor.IsEnabledByDefault,
            },
        };
    }

    private static string ToReadableString(this GeneratorDriverRunResult result)
    {
        return string.Join(
            "\n",
            result.GeneratedTrees.SelectMany(x => x.GetText().Lines).Select((x, i) => $"{i + 1, 4} {x}")
        );
    }

    private static bool IsDiagnosticInvalid(string? allowedDiagnosticCode, Diagnostic x)
    {
        return x.Id is not "CS5001"
            && (allowedDiagnosticCode is null || !x.Id.StartsWith(allowedDiagnosticCode, StringComparison.Ordinal))
            && (x.Severity > DiagnosticSeverity.Warning || x.IsWarningAsError);
    }
}

file sealed class EmbeddedResourcesAdditionalText(string logicalPath) : AdditionalText
{
    private readonly Assembly _assembly = typeof(EmbeddedResourcesAdditionalText).Assembly;

    public override string Path { get; } = logicalPath;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        string resourceName = System.IO.Path.Join(_assembly.GetName().Name, Path).Replace('\\', '.').Replace('/', '.');
        using Stream s =
            _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName} ({Path})");

        using var reader = new StreamReader(s);
        return SourceText.From(reader.ReadToEnd());
    }
}

file sealed class DictOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
{
    private readonly IReadOnlyDictionary<string, string> _values = values;

    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}

file sealed class OptionsProvider(IReadOnlyDictionary<string, string> globalValues) : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _empty = new DictOptions(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GlobalOptions { get; } = new DictOptions(globalValues);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _empty;
}
