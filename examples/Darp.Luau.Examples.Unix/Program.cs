namespace Darp.Luau.Examples.Unix;

internal sealed class Progarm
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var state = new LuauState();

        Console.WriteLine("Hello world");
    }
}