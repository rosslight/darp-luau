using System.Text;

namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    private const string KEY_SubTypeName = "SubTypeName";
    private const string KEY_TheParseResult = "TheParseResult";
    private const string FUNCTION_parse = "parse";

    [STAThread]
    public static void Main(string[] args)
    {
        using var state = new LuauState();
        SetupCallbacks(state);

        state.DoString(File.ReadAllBytes("./scripts/main.luau"), Encoding.UTF8.GetBytes("main"));
        state.DoString(File.ReadAllBytes("./scripts/apple.luau"), Encoding.UTF8.GetBytes("apple"));

        foreach(string strData in AdvData)
        {
            Console.WriteLine("---------------------------------------------------------------------");
            Parse(state, strData);
            Console.WriteLine();
        }
    }

    private static readonly string[] AdvData =
    [
        // Apple - Beacon
        "021550765CB7D9EA4E2199A4FA879613A492CB1AD302CE",
        // Apple - NearbyInfo
        "1006091AD0C23F46",
        "1006111E54C734B7",
        // Unknown
        "AABBCC",
    ];

    private static void Parse(LuauState state, string strData)
    {
        Console.WriteLine("Data: {0}", strData);
        Console.WriteLine();

        if (!state.Globals.TryGet(FUNCTION_parse, out LuauFunction funcParse))
        {
            Console.Error.WriteLine("Function '{0}' not found", FUNCTION_parse);
            return;
        }

        using LuauBuffer data = state.CreateBuffer(Convert.FromHexString(strData));
        _ = funcParse.Call<bool>(data);
        if (!state.Globals.TryGet(KEY_TheParseResult, out LuauTable table))
        {
            Console.Error.WriteLine("Key '{0}' not found", KEY_TheParseResult);
            return;
        }

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
        state.Globals.Set("fromHexString", state.CreateFunctionBuilder((ref onCalled) =>
        {
            byte[] bytes = Convert.FromHexString(onCalled.CheckString(1));
            using LuauBuffer buffer = state.CreateBuffer(bytes);
            onCalled.ReturnParameter(buffer);
        }));

        state.Globals.Set("toHexString", state.CreateFunctionBuilder((ref onCalled) =>
        {
            string str = Convert.ToHexString(onCalled.CheckBuffer(1));
            onCalled.ReturnParameter(str);
        }));
    }
}