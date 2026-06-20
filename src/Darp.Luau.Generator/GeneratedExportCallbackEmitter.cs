using System.CodeDom.Compiler;
using Darp.Luau.Generator.GeneratedExports;
using Darp.Luau.Generator.Helpers;

namespace Darp.Luau.Generator;

internal static class GeneratedExportCallbackEmitter
{
    public static void WriteMethodBody(IndentedTextWriter writer, GeneratedExportMethodIr method, string receiverExpression)
    {
        string callExpression =
            $"{receiverExpression}.{method.ManagedName}({string.Join(", ", Enumerable.Range(1, method.Parameters.Length).Select(static i => $"a{i}"))})";
        WriteBody(writer, method, callExpression);
    }

    public static void WriteStaticMethodBody(IndentedTextWriter writer, GeneratedExportMethodIr method)
    {
        string callExpression =
            $"{method.ManagedName}({string.Join(", ", Enumerable.Range(1, method.Parameters.Length).Select(static i => $"a{i}"))})";
        WriteBody(writer, method, callExpression);
    }

    private static void WriteBody(IndentedTextWriter writer, GeneratedExportMethodIr method, string callExpression)
    {
        writer.WriteLine($"if (!args.TryValidateArgumentCount({method.Parameters.Length}, out string? error))");
        writer.WriteLine("    return global::Darp.Luau.LuauReturn.Error(error);");

        for (int i = 0; i < method.Parameters.Length; i++)
            writer.WriteMultiLine(LuauMarshallingEmitter.GenerateParameterRead(i + 1, method.Parameters[i]));

        if (method.ReturnTypes.Length == 0)
        {
            writer.WriteLine($"{callExpression};");
            writer.WriteLine("return global::Darp.Luau.LuauReturn.Ok();");
            return;
        }

        writer.WriteLine($"var returns = {callExpression};");
        if (method.ReturnTypes.Length == 1)
        {
            writer.WriteLine(
                $"return global::Darp.Luau.LuauReturn.Ok({LuauMarshallingEmitter.FormatIntoLuauExpression("returns", method.ReturnTypes[0])});"
            );
            return;
        }

        writer.WriteLine(
            $"return global::Darp.Luau.LuauReturn.Ok({string.Join(", ", method.ReturnTypes.Select((x, i) => LuauMarshallingEmitter.FormatIntoLuauExpression($"returns.Item{i + 1}", x)))});"
        );
    }
}
