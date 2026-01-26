using System.Collections.Immutable;

namespace Darp.Luau.Generator.Helpers;

internal static class EmitterHelper
{
    public static string LuauValueToString(LuauValueType type)
    {
        return type switch
        {
            LuauValueType.String => "string",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    public static string GetFunctionRepresentation(InvocationMethodSignature signature)
    {
        ImmutableArray<LuauValueType> parameters = signature.Parameters;
        ImmutableArray<LuauValueType> returnParameters = signature.ReturnParameters;
        return (parameters.Length, returnParameters.Length) switch
        {
            (0, 0) => "global::System.Action",
            (_, 0) => $"global::System.Action<{string.Join(",", parameters.Select(LuauValueToString))}>",
            (0, 1) => $"global::System.Func<{LuauValueToString(returnParameters[0])}>",
            (0, _) => $"global::System.Func<({string.Join("\n", returnParameters.Select(LuauValueToString))})>",
            (_, 1) =>
                $"global::System.Func<{string.Join(",", parameters.Select(LuauValueToString))}, {LuauValueToString(returnParameters[0])}>",
            (_, _) =>
                $"global::System.Func<{string.Join(",", parameters.Select(LuauValueToString))}, ({string.Join("\n", returnParameters.Select(LuauValueToString))})>",
        };
    }
}
