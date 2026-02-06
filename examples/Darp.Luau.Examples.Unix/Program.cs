using System.Text;

namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    private const string KEY_SubTypeName = "SubTypeName";

    [STAThread]
    public static async Task Main(string[] args)
    {
        using var state = new LuauState();
        SetupState(state);

        state.Globals.Set("Data", "021550765CB7D9EA4E2199A4FA879613A492CB1AD302CE");
        state.DoString(File.ReadAllBytes("./scripts/test.luau"));
        if (state.Globals.TryGet("Result", out LuauTable table))
        {
            Console.WriteLine(table);
        }
        else
        {
            Console.WriteLine("Kein Resultat");
        }


        // state.DoString(File.ReadAllBytes("./scripts/test.luau"));
        // if (state.Globals.TryGet("parse", out LuauFunction funcParse))
        // {
        //     //TODO Wenn funcParse nil zurückgibt, knallt es!?
        //     using LuauTable table = funcParse.Call<LuauTable>("021550765CB7D9EA4E2199A4FA879613A492CB1AD302CE");

        //     if (table.TryGet(KEY_SubTypeName, out LuauValue valSubTypeName))
        //     {
        //         Console.WriteLine("{0} : {1}", KEY_SubTypeName, valSubTypeName);
        //     }

        //     using LuauTable.Enumerator enumerator = table.GetEnumerator();
        //     while(enumerator.MoveNext())
        //     {
        //         KeyValuePair<LuauValue, LuauValue> kv = enumerator.Current;
        //         if (kv.Key.ToString().Equals(KEY_SubTypeName))
        //             continue;

        //         Console.WriteLine("{0} ==> {1} [{2}]", kv.Key, kv.Value, kv.Value.Type);
        //     }
        // }
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