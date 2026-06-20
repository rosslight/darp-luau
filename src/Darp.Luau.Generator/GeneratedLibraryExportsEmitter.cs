using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Generator.GeneratedExports;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Darp.Luau.Generator;

internal static class GeneratedLibraryExportsEmitter
{
    public static bool TryEmit(
        INamedTypeSymbol type,
        GeneratedExportSurfaceIr model,
        [NotNullWhen(true)] out string? source,
        out List<Diagnostic> diagnostics
    )
    {
        diagnostics = [];
        if (model.LibraryRoot is null || string.IsNullOrWhiteSpace(model.LibraryName))
        {
            source = null;
            return false;
        }

        using var stringWriter = new StringWriter();
        using var writer = new IndentedTextWriter(stringWriter);
        WriteFile(writer, type, model);
        source = stringWriter.ToString();
        return true;
    }

    private static void WriteFile(IndentedTextWriter writer, INamedTypeSymbol type, GeneratedExportSurfaceIr model)
    {
        GeneratedExportsEmitterHelper.WriteFileHeader(writer);
        bool hasNamespace = GeneratedExportsEmitterHelper.WriteNamespaceStart(writer, type);

        writer.WriteLine(GeneratedExportsEmitterHelper.GetTypeDeclaration(type));
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"public const string LuauLibraryName = {SymbolDisplay.FormatLiteral(model.LibraryName!, quote: true)};");
        writer.WriteLine();
        writer.WriteLine($"public {GetRegisterModifier(type)}void Register(global::Darp.Luau.LuauState lua)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("global::System.ArgumentNullException.ThrowIfNull(lua);");
        writer.WriteLine("lua.OpenLibrary(LuauLibraryName, BuildLibrary);");
        writer.WriteLine();
        writer.WriteLine("void BuildLibrary(global::Darp.Luau.LuauState state, in global::Darp.Luau.LuauTable lib)");
        writer.WriteLine("{");
        writer.Indent++;
        var localNames = new GeneratedExportsEmitterHelper.LocalNameAllocator();
        WriteLibraryNodeContents(writer, model.LibraryRoot!, "lib", localNames);
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");

        GeneratedExportsEmitterHelper.WriteNamespaceEnd(writer, hasNamespace);
    }

    private static void WriteLibraryNodeContents(
        IndentedTextWriter writer,
        GeneratedLibraryExportNodeIr node,
        string tableVariableName,
        GeneratedExportsEmitterHelper.LocalNameAllocator localNames
    )
    {
        foreach (GeneratedLibraryExportNodeIr child in node.Children.OrderBy(static x => x.Name, StringComparer.Ordinal))
        {
            if (child.Member is null)
            {
                string nestedTableName = localNames.Next();
                writer.WriteLine($"using global::Darp.Luau.LuauTable {nestedTableName} = state.CreateTable();");
                WriteLibraryNodeContents(writer, child, nestedTableName, localNames);
                writer.WriteLine($"{tableVariableName}.Set({SymbolDisplay.FormatLiteral(child.Name, quote: true)}, {nestedTableName});");
                writer.WriteLine();
                continue;
            }

            WriteMember(writer, child.Member, tableVariableName, localNames);
        }
    }

    private static void WriteMember(
        IndentedTextWriter writer,
        GeneratedExportMemberIr member,
        string tableVariableName,
        GeneratedExportsEmitterHelper.LocalNameAllocator localNames
    )
    {
        switch (member)
        {
            case GeneratedExportPropertyIr property:
                WriteProperty(writer, property, tableVariableName);
                break;
            case GeneratedExportMethodIr method:
                WriteMethod(writer, method, tableVariableName, localNames);
                break;
            default:
                throw new InvalidOperationException($"Unsupported generated library member '{member.GetType().Name}'.");
        }
    }

    private static void WriteProperty(IndentedTextWriter writer, GeneratedExportPropertyIr property, string tableVariableName)
    {
        GeneratedExportAccessorIr accessor =
            property.Getter ?? throw new InvalidOperationException("Generated library properties must have a getter.");
        string keyLiteral = SymbolDisplay.FormatLiteral(property.PathSegments[^1], quote: true);
        writer.WriteLine(
            $"{tableVariableName}.Set({keyLiteral}, {LuauMarshallingEmitter.FormatIntoLuauExpression(property.ManagedName, accessor.Type)});"
        );
    }

    private static void WriteMethod(
        IndentedTextWriter writer,
        GeneratedExportMethodIr method,
        string tableVariableName,
        GeneratedExportsEmitterHelper.LocalNameAllocator localNames
    )
    {
        string functionVariableName = localNames.Next();
        string keyLiteral = SymbolDisplay.FormatLiteral(method.PathSegments[^1], quote: true);

        writer.WriteLine($"using global::Darp.Luau.LuauFunction {functionVariableName} = state.CreateFunctionBuilder(args =>");
        writer.WriteLine("{");
        writer.Indent++;
        GeneratedExportCallbackEmitter.WriteStaticMethodBody(writer, method);

        writer.Indent--;
        writer.WriteLine("});");
        writer.WriteLine($"{tableVariableName}.Set({keyLiteral}, {functionVariableName});");
    }

    private static string GetRegisterModifier(INamedTypeSymbol type) => type.IsStatic ? "static " : string.Empty;
}
