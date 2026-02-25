using System.Text;

namespace Darp.Luau;

public static class LuauStateExtensions
{
    public static LuauTable CreateTable(this LuauState state, ReadOnlySpan<double> values)
    {
        ArgumentNullException.ThrowIfNull(state);
        LuauTable table = state.CreateTable();
        for (int i = 0; i < values.Length; i++)
        {
            table.Set(i + 1, values[i]);
        }
        return table;
    }

    public static void EnableRequire(this LuauState state, string? strScriptPath = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        
        if (string.IsNullOrWhiteSpace(strScriptPath))
            strScriptPath = "";
        if (Path.IsPathRooted(strScriptPath))
            throw new ArgumentException($"Script path may not be rooted: {strScriptPath}");

        state.Globals.Set("require", state.CreateFunctionBuilder(args =>
        {
            if (!args.TryReadUtf8String(1, out string? strPath, out string? strError))
                throw new ArgumentException($"Missing path argument: {strError}");
            if (!Path.IsPathRooted(strPath))
                strPath = Path.Combine(Directory.GetCurrentDirectory(), strScriptPath, Path.GetFileName(strPath));
            if (!Path.HasExtension(strPath))
                strPath += ".luau";
            if (!File.Exists(strPath))
                throw new ArgumentException($"Script file does not exist: {strPath}");

            string strChunkName = Path.GetFileNameWithoutExtension(strPath);
            LuauValue[] results = state.DoString(File.ReadAllBytes(strPath), nNumExpectedRetValues: 1, Encoding.UTF8.GetBytes(strChunkName));
            return LuauReturn.Ok(results[0]);
        }));
    }
}
