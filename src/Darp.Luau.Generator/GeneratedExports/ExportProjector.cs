using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class ExportProjector
{
    public static GeneratedExportSurfaceIr Project(ValidatedExportType validatedType)
    {
        return new GeneratedExportSurfaceIr(
            validatedType.Type.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            validatedType.Type.Symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : validatedType.Type.Symbol.ContainingNamespace.ToDisplayString(),
            GetTypeDeclaration(validatedType.Type.Symbol),
            validatedType.Type.Symbol.IsStatic,
            GetHintName(validatedType.Type.Symbol, validatedType.Type.Kind),
            validatedType.Type.Kind,
            validatedType.Type.LibraryName,
            validatedType.Type.Members.Select(ProjectMember).ToImmutableEquatableArray(),
            validatedType.LibraryRoot is null ? null : ProjectNode(validatedType.LibraryRoot)
        );
    }

    private static GeneratedExportMemberIr ProjectMember(NormalizedExportMember member)
    {
        return member switch
        {
            NormalizedExportPropertyMember property => new GeneratedExportPropertyIr(
                property.ManagedName,
                property.LuauName,
                property.PathSegments,
                property.Property.Getter is null ? null : new GeneratedExportAccessorIr(property.Property.Getter.Type),
                property.Property.Setter is null ? null : new GeneratedExportAccessorIr(property.Property.Setter.Type)
            ),
            NormalizedExportMethodMember method => new GeneratedExportMethodIr(
                method.ManagedName,
                method.LuauName,
                method.PathSegments,
                method.Method.Parameters,
                method.Method.ReturnTypes
            ),
            _ => throw new InvalidOperationException($"Unsupported normalized member type '{member.GetType().Name}'"),
        };
    }

    private static GeneratedLibraryExportNodeIr ProjectNode(ValidatedLibraryExportNode node)
    {
        return new GeneratedLibraryExportNodeIr(
            node.Name,
            node.Member is null ? null : ProjectMember(node.Member),
            node.Children.Select(ProjectNode).ToImmutableEquatableArray()
        );
    }

    private static string GetTypeDeclaration(INamedTypeSymbol type)
    {
        TypeDeclarationSyntax syntax = type.DeclaringSyntaxReferences
            .Select(static x => x.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .First();

        string modifiers = string.Join(
            " ",
            syntax.Modifiers.Where(static x =>
                    x.IsKind(SyntaxKind.PublicKeyword)
                    || x.IsKind(SyntaxKind.InternalKeyword)
                    || x.IsKind(SyntaxKind.PrivateKeyword)
                    || x.IsKind(SyntaxKind.ProtectedKeyword)
                    || x.IsKind(SyntaxKind.StaticKeyword)
                    || x.IsKind(SyntaxKind.AbstractKeyword)
                    || x.IsKind(SyntaxKind.SealedKeyword)
                    || x.IsKind(SyntaxKind.PartialKeyword)
                )
                .Select(static x => x.Text)
        );

        return syntax switch
        {
            ClassDeclarationSyntax classDeclaration => $"{modifiers} class {classDeclaration.Identifier.Text}",
            StructDeclarationSyntax structDeclaration => $"{modifiers} struct {structDeclaration.Identifier.Text}",
            RecordDeclarationSyntax recordDeclaration =>
                $"{modifiers} record {recordDeclaration.ClassOrStructKeyword.Text} {recordDeclaration.Identifier.Text}",
            _ => throw new InvalidOperationException($"Unsupported generated export type declaration '{syntax.Kind()}'."),
        };
    }

    private static string GetHintName(INamedTypeSymbol type, LuauExportedTypeKind kind)
    {
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new System.Text.StringBuilder(name.Length + 32);
        foreach (char c in name)
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        builder.Append(kind == LuauExportedTypeKind.Library ? ".LuauLibrary.g.cs" : ".LuauUserdata.g.cs");
        return builder.ToString();
    }
}
