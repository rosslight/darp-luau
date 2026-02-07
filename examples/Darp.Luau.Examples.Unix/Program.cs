using System.Text;

namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    private const string KEY_SubTypeName = "SubTypeName";
    private const string KEY_TheParseResult = "TheParseResult";

    [STAThread]
    public static void Main(string[] args)
    {
        using var state = new LuauState();
        SetupCallbacks(state);

        state.DoString(File.ReadAllBytes("./scripts/main.luau"));
        state.DoString(File.ReadAllBytes("./scripts/helpers.luau"), Encoding.UTF8.GetBytes("helpers"));
        state.DoString(File.ReadAllBytes("./scripts/apple.luau"), Encoding.UTF8.GetBytes("apple"));

        // Beacon
        Console.WriteLine("---------------------------------------------------------------------");
        Parse(state, "021550765CB7D9EA4E2199A4FA879613A492CB1AD302CE");
        Console.WriteLine();

        // NearbyInfo
        Console.WriteLine("---------------------------------------------------------------------");
        Parse(state, "1006091AD0C23F46");
        Console.WriteLine();
        Console.WriteLine("---------------------------------------------------------------------");
        Parse(state, "1006111E54C734B7");
        Console.WriteLine();
    }

    private static void Parse(LuauState state, string strData)
    {
        if (!state.Globals.TryGet("parse", out LuauFunction funcParse))
            return;

        _ = funcParse.Call<bool>(strData);
        if (!state.Globals.TryGet(KEY_TheParseResult, out LuauTable table))
            return;

        if (table.TryGet(KEY_SubTypeName, out LuauValue valSubTypeName))
        {
            Console.WriteLine("{0} : {1}", KEY_SubTypeName, valSubTypeName);
        }

        using LuauTable.Enumerator enumerator = table.GetEnumerator();
        while(enumerator.MoveNext())
        {
            KeyValuePair<LuauValue, LuauValue> kv = enumerator.Current;
            if (kv.Key.ToString().Equals(KEY_SubTypeName))
                continue;

            Console.WriteLine("{0} ==> {1} [{2}]", kv.Key, kv.Value, kv.Value.Type);
        }
    }

    private static void SetupCallbacks(LuauState state)
    {
        //TODO
        // state.Globals.Set("fromHexString", state.CreateFunctionBuilder((ref onCalled) =>
        // {
        //     byte[] bytes = Convert.FromHexString(onCalled.CheckString(1));
        //     onCalled.ReturnParameter(bytes);
        // }));

        //TODO
        // state.Globals.Set("toHexString", state.CreateFunctionBuilder((ref onCalled) =>
        // {
        //     string str = Convert.ToHexString(onCalled.CheckBuffer(1));
        //     onCalled.ReturnParameter(str);
        // }));
    }
}