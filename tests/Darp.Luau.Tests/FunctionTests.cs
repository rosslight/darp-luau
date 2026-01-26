using Shouldly;

namespace Darp.Luau.Tests;

public sealed class FunctionTests
{
    [Fact]
    public void Simple()
    {
        using var state = new LuauState();
        state.DoString(
            """
            function get_value()
              return "Hello";
            end
            """
        );
        _ = state.Globals.TryGet("get_value", out LuauFunction func);
        string r = func.Call<string>();
        r.ShouldBe("Hello");
    }
}
