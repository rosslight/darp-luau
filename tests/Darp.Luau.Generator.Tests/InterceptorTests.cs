namespace Darp.Luau.Generator.Tests;

public class InterceptorTests
{
    [Fact]
    public async Task NoParameters()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction(() => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task NoParameters_Two()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction(() => {});
                    state.CreateFunction(() => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task StringParameter()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((string p1) => {});
                    state.CreateFunction((string p1, string p2) => {});
                    state.CreateFunction(OnCall);
                }

                private static void OnCall(string p1, string p2) { }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }
}
