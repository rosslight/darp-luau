using System.Text;

namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var state = new LuauState();

        LuauFunction funcLog = state.CreateFunctionBuilder((ref onCalled) =>
        {
            string strMsg = Encoding.UTF8.GetString(onCalled.CheckString(1));
            Console.WriteLine(strMsg);
        });
        state.Globals.Set("log", funcLog);

        state.DoString(
            """
            function add(a: number, b: number): ()
                return a + b
            end
            
            log("hello from luau")
            """
        );

        _ = state.Globals.TryGet("add", out LuauFunction funcAdd);
        double result = funcAdd.Call<double>(1, 2);        
        Console.WriteLine(result);
    }
}