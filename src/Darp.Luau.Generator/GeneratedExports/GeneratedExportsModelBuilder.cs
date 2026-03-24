using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Darp.Luau.Generator.GeneratedExports;

internal sealed class GeneratedExportsModelBuilder
{
    private readonly INamedTypeSymbol _libraryAttributeSymbol;
    private readonly INamedTypeSymbol _userdataAttributeSymbol;
    private readonly INamedTypeSymbol _memberAttributeSymbol;
    private readonly INamedTypeSymbol _luauUserdataInterfaceSymbol;

    private GeneratedExportsModelBuilder(
        INamedTypeSymbol libraryAttributeSymbol,
        INamedTypeSymbol userdataAttributeSymbol,
        INamedTypeSymbol memberAttributeSymbol,
        INamedTypeSymbol luauUserdataInterfaceSymbol
    )
    {
        _libraryAttributeSymbol = libraryAttributeSymbol;
        _userdataAttributeSymbol = userdataAttributeSymbol;
        _memberAttributeSymbol = memberAttributeSymbol;
        _luauUserdataInterfaceSymbol = luauUserdataInterfaceSymbol;
    }

    public static GeneratedExportsModelBuilder? Create(Compilation compilation)
    {
        INamedTypeSymbol? libraryAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauLibraryAttribute");
        INamedTypeSymbol? userdataAttributeSymbol = compilation.GetTypeByMetadataName(
            "Darp.Luau.LuauUserdataAttribute"
        );
        INamedTypeSymbol? memberAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauMemberAttribute");
        INamedTypeSymbol? luauUserdataInterfaceSymbol = compilation.GetTypeByMetadataName("Darp.Luau.ILuauUserData`1");
        if (
            libraryAttributeSymbol is null
            || userdataAttributeSymbol is null
            || memberAttributeSymbol is null
            || luauUserdataInterfaceSymbol is null
        )
        {
            return null;
        }

        return new GeneratedExportsModelBuilder(
            libraryAttributeSymbol,
            userdataAttributeSymbol,
            memberAttributeSymbol,
            luauUserdataInterfaceSymbol
        );
    }

    public GeneratedExportsTypeAnalysis AnalyzeType(INamedTypeSymbol type)
    {
        AttributeData? libraryAttribute = GetAttribute(type, _libraryAttributeSymbol);
        AttributeData? userdataAttribute = GetAttribute(type, _userdataAttributeSymbol);
        if (libraryAttribute is null && userdataAttribute is null)
            return new GeneratedExportsTypeAnalysis(null, []);

        var diagnostics = new List<Diagnostic>();
        if (libraryAttribute is not null && userdataAttribute is not null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    GetAttributeLocation(libraryAttribute, type),
                    $"type '{type.Name}' cannot be both a [LuauLibrary] and a [LuauUserdata]"
                )
            );
            return new GeneratedExportsTypeAnalysis(null, diagnostics.ToImmutableArray());
        }

        LuauExportedTypeModel? model = libraryAttribute is not null
            ? AnalyzeLibrary(type, libraryAttribute, diagnostics)
            : AnalyzeUserdata(type, userdataAttribute!, diagnostics);

        return new GeneratedExportsTypeAnalysis(model, diagnostics.ToImmutableArray());
    }

    private LuauExportedTypeModel AnalyzeLibrary(
        INamedTypeSymbol type,
        AttributeData libraryAttribute,
        List<Diagnostic> diagnostics
    )
    {
        if (!IsPartial(type))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.LuauLibraryTypeMustBePartialDescriptor,
                    GetAttributeLocation(libraryAttribute, type)
                )
            );
        }

        if (type.TypeKind == TypeKind.Struct)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InstanceLibraryStructNotSupportedDescriptor,
                    GetAttributeLocation(libraryAttribute, type)
                )
            );
        }

        string? libraryName = GetStringConstructorArgument(libraryAttribute);
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    GetAttributeLocation(libraryAttribute, type),
                    "root libraries must supply a non-empty name via [LuauLibrary(\"...\")]"
                )
            );
        }

        ImmutableArray<LuauExportedMemberModel> members = AnalyzeMembers(
            type,
            LuauExportedTypeKind.Library,
            diagnostics
        );
        LuauLibraryExportNode root = BuildLibraryTree(type, members, diagnostics);
        return new LuauExportedTypeModel(type, LuauExportedTypeKind.Library, libraryName, members, root);
    }

    private LuauExportedTypeModel AnalyzeUserdata(
        INamedTypeSymbol type,
        AttributeData userdataAttribute,
        List<Diagnostic> diagnostics
    )
    {
        if (!IsPartial(type))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.LuauUserdataTypeMustBePartialDescriptor,
                    GetAttributeLocation(userdataAttribute, type)
                )
            );
        }

        if (type.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    GetAttributeLocation(userdataAttribute, type),
                    "types marked with [LuauUserdata] must be classes"
                )
            );
        }

        if (ImplementsManualUserdataHooks(type))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedUserdataManualInteropConflictDescriptor,
                    GetAttributeLocation(userdataAttribute, type),
                    type.Name
                )
            );
        }

        ImmutableArray<LuauExportedMemberModel> members = AnalyzeMembers(
            type,
            LuauExportedTypeKind.Userdata,
            diagnostics
        );
        ReportDuplicateNames(type, LuauExportedTypeKind.Userdata, members, diagnostics);
        return new LuauExportedTypeModel(type, LuauExportedTypeKind.Userdata, null, members, null);
    }

    private ImmutableArray<LuauExportedMemberModel> AnalyzeMembers(
        INamedTypeSymbol type,
        LuauExportedTypeKind exportedTypeKind,
        List<Diagnostic> diagnostics
    )
    {
        var members = ImmutableArray.CreateBuilder<LuauExportedMemberModel>();
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;

            AttributeData? memberAttribute = GetAttribute(member, _memberAttributeSymbol);
            if (memberAttribute is null)
                continue;

            LuauExportedMemberModel? model = member switch
            {
                IPropertySymbol property => AnalyzeProperty(
                    type,
                    property,
                    memberAttribute,
                    exportedTypeKind,
                    diagnostics
                ),
                IMethodSymbol method => AnalyzeMethod(type, method, memberAttribute, exportedTypeKind, diagnostics),
                _ => null,
            };

            if (model is not null)
                members.Add(model);
        }

        return members.ToImmutable();
    }

    private LuauExportedMemberModel? AnalyzeProperty(
        INamedTypeSymbol type,
        IPropertySymbol property,
        AttributeData memberAttribute,
        LuauExportedTypeKind exportedTypeKind,
        List<Diagnostic> diagnostics
    )
    {
        Location location = GetAttributeLocation(memberAttribute, property);
        if (property.IsIndexer)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"property '{property.Name}' is an indexer, which is not supported"
                )
            );
            return null;
        }

        if (exportedTypeKind == LuauExportedTypeKind.Userdata && property.IsStatic)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"userdata property '{property.Name}' must be an instance member"
                )
            );
            return null;
        }

        string? exportedName = GetStringConstructorArgument(memberAttribute);
        if (exportedName is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"member '{property.Name}' must specify a Luau-facing name"
                )
            );
            return null;
        }

        if (!TryGetPropertyAccess(memberAttribute, location, diagnostics, out LuauExportPropertyAccess requestedAccess))
            return null;

        if (
            !TryResolvePropertyAccess(
                property,
                requestedAccess,
                out LuauExportPropertyAccess resolvedAccess,
                out string? reason
            )
        )
        {
            diagnostics.Add(
                Diagnostic.Create(DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor, location, reason)
            );
            return null;
        }

        if (exportedTypeKind == LuauExportedTypeKind.Library && resolvedAccess != LuauExportPropertyAccess.ReadOnly)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.LibraryPropertyMustBeReadOnlyDescriptor,
                    location,
                    property.Name
                )
            );
            return null;
        }

        if (
            !TryParsePath(
                exportedTypeKind,
                exportedName,
                property.Name,
                location,
                diagnostics,
                out ImmutableArray<string> pathSegments
            )
        )
        {
            return null;
        }

        LuauInteropTypeUsage typeUsage =
            exportedTypeKind == LuauExportedTypeKind.Library
                ? LuauInteropTypeUsage.LibraryProperty
                : LuauInteropTypeUsage.UserdataPropertyGet;
        if (
            !TryMapPropertyType(
                property,
                typeUsage,
                location,
                exportedTypeKind,
                diagnostics,
                out LuauTypeMapping mapping
            )
        )
            return null;

        return new LuauExportedMemberModel(
            property,
            LuauExportedMemberKind.Property,
            exportedName,
            pathSegments,
            resolvedAccess,
            mapping,
            ImmutableArray<LuauTypeMapping>.Empty,
            ImmutableArray<LuauTypeMapping>.Empty
        );
    }

    private LuauExportedMemberModel? AnalyzeMethod(
        INamedTypeSymbol type,
        IMethodSymbol method,
        AttributeData memberAttribute,
        LuauExportedTypeKind exportedTypeKind,
        List<Diagnostic> diagnostics
    )
    {
        Location location = GetAttributeLocation(memberAttribute, method);
        if (method.MethodKind != MethodKind.Ordinary)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"method '{method.Name}' is not an ordinary method"
                )
            );
            return null;
        }

        if (exportedTypeKind == LuauExportedTypeKind.Userdata && method.IsStatic)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"userdata method '{method.Name}' must be an instance member"
                )
            );
            return null;
        }

        string? exportedName = GetStringConstructorArgument(memberAttribute);
        if (exportedName is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"member '{method.Name}' must specify a Luau-facing name"
                )
            );
            return null;
        }

        if (
            !TryParsePath(
                exportedTypeKind,
                exportedName,
                method.Name,
                location,
                diagnostics,
                out ImmutableArray<string> pathSegments
            )
        )
        {
            return null;
        }

        if (
            !TryMapMethodSignature(method, exportedTypeKind, location, diagnostics, out var parameters, out var returns)
        )
            return null;

        return new LuauExportedMemberModel(
            method,
            LuauExportedMemberKind.Method,
            exportedName,
            pathSegments,
            LuauExportPropertyAccess.Auto,
            null,
            parameters,
            returns
        );
    }

    private bool TryMapPropertyType(
        IPropertySymbol property,
        LuauInteropTypeUsage usage,
        Location location,
        LuauExportedTypeKind exportedTypeKind,
        List<Diagnostic> diagnostics,
        out LuauTypeMapping mapping
    )
    {
        if (!TryMapExportType(property.Type, usage, out mapping))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedGeneratedPropertyTypeDescriptor,
                    location,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    exportedTypeKind == LuauExportedTypeKind.Library ? "library property" : "userdata property",
                    property.Name
                )
            );
            mapping = default;
            return false;
        }

        return true;
    }

    private bool TryMapMethodSignature(
        IMethodSymbol method,
        LuauExportedTypeKind exportedTypeKind,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<LuauTypeMapping> parameters,
        out ImmutableArray<LuauTypeMapping> returns
    )
    {
        if (method.IsGenericMethod)
        {
            ReportUnsupportedMethodShape(
                exportedTypeKind,
                method,
                location,
                diagnostics,
                "generic methods are not supported"
            );
            parameters = [];
            returns = [];
            return false;
        }

        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            ReportUnsupportedMethodShape(
                exportedTypeKind,
                method,
                location,
                diagnostics,
                "by-ref returns are not supported"
            );
            parameters = [];
            returns = [];
            return false;
        }

        var parameterBuilder = ImmutableArray.CreateBuilder<LuauTypeMapping>();
        LuauInteropTypeUsage parameterUsage =
            exportedTypeKind == LuauExportedTypeKind.Library
                ? LuauInteropTypeUsage.LibraryFunctionParameter
                : LuauInteropTypeUsage.UserdataMethodParameter;
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                ReportUnsupportedMethodShape(
                    exportedTypeKind,
                    method,
                    location,
                    diagnostics,
                    $"parameter '{parameter.Name}' cannot use ref, in, or out"
                );
                parameters = [];
                returns = [];
                return false;
            }

            if (parameter.IsOptional || parameter.HasExplicitDefaultValue)
            {
                ReportUnsupportedMethodShape(
                    exportedTypeKind,
                    method,
                    location,
                    diagnostics,
                    $"parameter '{parameter.Name}' is optional"
                );
                parameters = [];
                returns = [];
                return false;
            }

            if (parameter.IsParams)
            {
                ReportUnsupportedMethodShape(
                    exportedTypeKind,
                    method,
                    location,
                    diagnostics,
                    $"parameter '{parameter.Name}' uses params"
                );
                parameters = [];
                returns = [];
                return false;
            }

            if (!TryMapExportType(parameter.Type, parameterUsage, out LuauTypeMapping parameterMapping))
            {
                ReportUnsupportedMethodShape(
                    exportedTypeKind,
                    method,
                    location,
                    diagnostics,
                    $"parameter '{parameter.Name}' has unsupported type '{parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}'"
                );
                parameters = [];
                returns = [];
                return false;
            }

            parameterBuilder.Add(parameterMapping);
        }

        if (!TryMapMethodReturns(method, exportedTypeKind, location, diagnostics, out returns))
        {
            parameters = [];
            return false;
        }

        parameters = parameterBuilder.ToImmutable();
        return true;
    }

    private bool TryMapMethodReturns(
        IMethodSymbol method,
        LuauExportedTypeKind exportedTypeKind,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<LuauTypeMapping> returns
    )
    {
        if (method.ReturnType.SpecialType == SpecialType.System_Void)
        {
            returns = [];
            return true;
        }

        var returnBuilder = ImmutableArray.CreateBuilder<LuauTypeMapping>();
        LuauInteropTypeUsage returnUsage =
            exportedTypeKind == LuauExportedTypeKind.Library
                ? LuauInteropTypeUsage.LibraryFunctionReturn
                : LuauInteropTypeUsage.UserdataMethodReturn;
        if (method.ReturnType is not INamedTypeSymbol { IsTupleType: true } tupleType)
        {
            if (
                !TryMapReturnType(
                    method,
                    method.ReturnType,
                    "return value",
                    returnUsage,
                    location,
                    diagnostics,
                    out LuauTypeMapping mapping
                )
            )
            {
                returns = [];
                return false;
            }

            returnBuilder.Add(mapping);
            returns = returnBuilder.ToImmutable();
            return true;
        }

        if (tupleType.TupleElements.Length > 4)
        {
            ReportUnsupportedMethodShape(
                exportedTypeKind,
                method,
                location,
                diagnostics,
                "only up to 4 tuple return values are supported"
            );
            returns = [];
            return false;
        }

        foreach (IFieldSymbol tupleElement in tupleType.TupleElements)
        {
            if (tupleElement.Type is INamedTypeSymbol { IsTupleType: true })
            {
                ReportUnsupportedMethodShape(
                    exportedTypeKind,
                    method,
                    location,
                    diagnostics,
                    "nested tuple returns are not supported"
                );
                returns = [];
                return false;
            }

            string usageDescription = tupleElement.Name is { Length: > 0 }
                ? $"return tuple element '{tupleElement.Name}'"
                : "return tuple element";
            if (
                !TryMapReturnType(
                    method,
                    tupleElement.Type,
                    usageDescription,
                    returnUsage,
                    location,
                    diagnostics,
                    out LuauTypeMapping mapping
                )
            )
            {
                returns = [];
                return false;
            }

            returnBuilder.Add(mapping);
        }

        returns = returnBuilder.ToImmutable();
        return true;
    }

    private bool TryMapReturnType(
        IMethodSymbol method,
        ITypeSymbol type,
        string usageDescription,
        LuauInteropTypeUsage usage,
        Location location,
        List<Diagnostic> diagnostics,
        out LuauTypeMapping mapping
    )
    {
        if (!TryMapExportType(type, usage, out mapping))
        {
            ReportUnsupportedMethodShape(
                usage is LuauInteropTypeUsage.LibraryFunctionReturn
                    ? LuauExportedTypeKind.Library
                    : LuauExportedTypeKind.Userdata,
                method,
                location,
                diagnostics,
                $"{usageDescription} has unsupported type '{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}'"
            );
            mapping = default;
            return false;
        }

        return true;
    }

    private bool TryMapExportType(ITypeSymbol type, LuauInteropTypeUsage usage, out LuauTypeMapping mapping)
    {
        if (LuauInteropTypeMapper.TryMapType(type, out mapping) && IsMappingSupportedForUsage(mapping, usage))
            return true;

        if (TryMapGeneratedUserdataType(type, out mapping) && IsMappingSupportedForUsage(mapping, usage))
            return true;

        mapping = default;
        return false;
    }

    private bool TryMapGeneratedUserdataType(ITypeSymbol type, out LuauTypeMapping mapping)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            mapping = default;
            return false;
        }

        if (GetAttribute(namedType, _userdataAttributeSymbol) is null)
        {
            mapping = default;
            return false;
        }

        mapping = new LuauTypeMapping(
            LuauValueType.ManagedUserdata,
            type.NullableAnnotation is NullableAnnotation.Annotated,
            namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );
        return true;
    }

    private static bool IsMappingSupportedForUsage(LuauTypeMapping mapping, LuauInteropTypeUsage usage)
    {
        return LuauInteropTypeMapper.SupportsUsage(mapping, usage)
            && (!mapping.IsNullable || LuauInteropTypeMapper.SupportsNullableValue(mapping.Type));
    }

    private LuauLibraryExportNode BuildLibraryTree(
        INamedTypeSymbol type,
        ImmutableArray<LuauExportedMemberModel> members,
        List<Diagnostic> diagnostics
    )
    {
        var root = new LuauLibraryExportNode(string.Empty);
        foreach (LuauExportedMemberModel member in members)
            AddLibraryMember(root, type, member, diagnostics);

        return root;
    }

    private void AddLibraryMember(
        LuauLibraryExportNode root,
        INamedTypeSymbol type,
        LuauExportedMemberModel member,
        List<Diagnostic> diagnostics
    )
    {
        LuauLibraryExportNode current = root;
        for (int i = 0; i < member.PathSegments.Length; i++)
        {
            string segment = member.PathSegments[i];
            bool isLeaf = i == member.PathSegments.Length - 1;
            if (!current.Children.TryGetValue(segment, out LuauLibraryExportNode? child))
            {
                child = new LuauLibraryExportNode(segment);
                current.Children.Add(segment, child);
            }

            if (isLeaf)
            {
                if (child.Children.Count > 0)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.LuauExportPathConflictDescriptor,
                            GetSymbolLocation(member.Symbol),
                            member.Name,
                            GetNamespaceConflictPath(child, member.Name)
                        )
                    );
                    return;
                }

                if (child.Member is not null)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateLuauMemberNameDescriptor,
                            GetSymbolLocation(member.Symbol),
                            member.Name,
                            "library",
                            type.Name
                        )
                    );
                    return;
                }

                child.Member = member;
                return;
            }

            if (child.Member is not null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.LuauExportPathConflictDescriptor,
                        GetSymbolLocation(member.Symbol),
                        child.Member.Name,
                        member.Name
                    )
                );
                return;
            }

            current = child;
        }
    }

    private void ReportDuplicateNames(
        INamedTypeSymbol type,
        LuauExportedTypeKind exportedTypeKind,
        ImmutableArray<LuauExportedMemberModel> members,
        List<Diagnostic> diagnostics
    )
    {
        var names = new Dictionary<string, LuauExportedMemberModel>(StringComparer.Ordinal);
        foreach (LuauExportedMemberModel member in members)
        {
            if (names.TryGetValue(member.Name, out _))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateLuauMemberNameDescriptor,
                        GetSymbolLocation(member.Symbol),
                        member.Name,
                        exportedTypeKind == LuauExportedTypeKind.Library ? "library" : "userdata",
                        type.Name
                    )
                );
                continue;
            }

            names.Add(member.Name, member);
        }
    }

    private static string GetNamespaceConflictPath(LuauLibraryExportNode node, string prefix)
    {
        if (node.Member is not null)
            return node.Member.Name;

        foreach (KeyValuePair<string, LuauLibraryExportNode> child in node.Children)
            return GetNamespaceConflictPath(child.Value, prefix + "." + child.Key);

        return prefix;
    }

    private static bool TryResolvePropertyAccess(
        IPropertySymbol property,
        LuauExportPropertyAccess requestedAccess,
        out LuauExportPropertyAccess resolvedAccess,
        out string? reason
    )
    {
        bool hasGetter = property.GetMethod is not null;
        bool hasSetter = property.SetMethod is not null;
        switch (requestedAccess)
        {
            case LuauExportPropertyAccess.Auto:
                if (hasGetter && hasSetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.ReadWrite;
                    reason = null;
                    return true;
                }

                if (hasGetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.ReadOnly;
                    reason = null;
                    return true;
                }

                if (hasSetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.WriteOnly;
                    reason = null;
                    return true;
                }

                reason = $"property '{property.Name}' must define a getter and/or setter";
                resolvedAccess = default;
                return false;
            case LuauExportPropertyAccess.ReadOnly:
                if (hasGetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.ReadOnly;
                    reason = null;
                    return true;
                }

                reason = $"property '{property.Name}' must define a getter for LuauPropertyAccess.ReadOnly";
                resolvedAccess = default;
                return false;
            case LuauExportPropertyAccess.WriteOnly:
                if (hasSetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.WriteOnly;
                    reason = null;
                    return true;
                }

                reason = $"property '{property.Name}' must define a setter for LuauPropertyAccess.WriteOnly";
                resolvedAccess = default;
                return false;
            case LuauExportPropertyAccess.ReadWrite:
                if (hasGetter && hasSetter)
                {
                    resolvedAccess = LuauExportPropertyAccess.ReadWrite;
                    reason = null;
                    return true;
                }

                reason =
                    $"property '{property.Name}' must define both a getter and setter for LuauPropertyAccess.ReadWrite";
                resolvedAccess = default;
                return false;
            default:
                reason =
                    $"property '{property.Name}' uses an unknown LuauPropertyAccess value '{(int)requestedAccess}'";
                resolvedAccess = default;
                return false;
        }
    }

    private static bool TryGetPropertyAccess(
        AttributeData memberAttribute,
        Location location,
        List<Diagnostic> diagnostics,
        out LuauExportPropertyAccess access
    )
    {
        foreach (KeyValuePair<string, TypedConstant> namedArgument in memberAttribute.NamedArguments)
        {
            if (namedArgument.Key != "Access")
                continue;

            if (namedArgument.Value.Value is int rawValue && Enum.IsDefined(typeof(LuauExportPropertyAccess), rawValue))
            {
                access = (LuauExportPropertyAccess)rawValue;
                return true;
            }

            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"LuauPropertyAccess value '{namedArgument.Value.Value}' is not supported"
                )
            );
            access = default;
            return false;
        }

        access = LuauExportPropertyAccess.Auto;
        return true;
    }

    private static bool TryParsePath(
        LuauExportedTypeKind exportedTypeKind,
        string exportedName,
        string memberName,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<string> pathSegments
    )
    {
        return exportedTypeKind == LuauExportedTypeKind.Library
            ? GeneratedExportsPathParser.TryParseLibraryPath(exportedName, location, diagnostics, out pathSegments)
            : GeneratedExportsPathParser.TryParseUserdataPath(
                exportedName,
                memberName,
                location,
                diagnostics,
                out pathSegments
            );
    }

    private void ReportUnsupportedMethodShape(
        LuauExportedTypeKind exportedTypeKind,
        IMethodSymbol method,
        Location location,
        List<Diagnostic> diagnostics,
        string reason
    )
    {
        diagnostics.Add(
            Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedGeneratedFunctionShapeDescriptor,
                location,
                exportedTypeKind == LuauExportedTypeKind.Library ? "library function" : "userdata method",
                method.Name,
                reason
            )
        );
    }

    private bool ImplementsManualUserdataHooks(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, _luauUserdataInterfaceSymbol)
        );
    }

    private static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        return symbol
            .GetAttributes()
            .FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol)
            );
    }

    private static string? GetStringConstructorArgument(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        return attribute.ConstructorArguments[0].Value as string;
    }

    private static bool IsPartial(INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Length > 0
            && type.DeclaringSyntaxReferences.Select(static x => x.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .All(static x => x.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static Location GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? GetSymbolLocation(fallbackSymbol);
    }

    private static Location GetSymbolLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }
}
