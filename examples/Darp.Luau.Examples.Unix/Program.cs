using System.Text;

namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        using var state = new LuauState();
        SetupState(state);

        state.DoString(File.ReadAllBytes("./scripts/test.luau"));

        // state.DoString(
        //     """
        //     function add(a: number, b: number): ()
        //         return a + b
        //     end
            
        //     log("hello from luau")
        //     """
        // );

        _ = state.Globals.TryGet("add", out LuauFunction funcAdd);
        _ = state.Globals.TryGet("subtract", out LuauFunction funcSubtract);

        Console.WriteLine("add: {0}", funcAdd.Call<double>(1, 2));
        Console.WriteLine("subtract: {0}", funcSubtract.Call<double>(1, 2));
    }

    private static void SetupState(LuauState state)
    {
        LuauFunction funcLog = state.CreateFunctionBuilder((ref onCalled) =>
        {
            string strMsg = Encoding.UTF8.GetString(onCalled.CheckString(1));
            Console.WriteLine(strMsg);
        });
        state.Globals.Set("log", funcLog);
    }
}