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

    [Fact]
    public async Task CharSpanParameter()
    {
        const string code = """
            using System;
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((ReadOnlySpan<char> p1) => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task NumberParameter()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((double p1) => {});
                    state.CreateFunction((int p1) => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task NullableNumberParameter()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((double? p1) => {});
                    state.CreateFunction((int? p1) => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task ParameterRoundtrip()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public delegate string? MyDelegate(string? x);

                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((MyDelegate)((string? p1) => p1));
                    state.CreateFunction((System.Func<string?, string?>)((string? p1) => p1));
                    state.CreateFunction((string p1) => p1);
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task LuauParameter()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((LuauValue p1) => {});
                    state.CreateFunction((LuauTable p1, LuauString p2, LuauFunction p3) => {});
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task ReturnParameters()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction(() => 1);
                    state.CreateFunction(() => "myString");
                    state.CreateFunction((string x) => x);
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    [Fact]
    public async Task EnumParameter()
    {
        const string code = """
            using Darp.Luau;

            public enum MyEnum;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((MyEnum p1) => p1);
                    state.CreateFunction((MyEnum? p1) => p1);
                }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }

    /*
    [Fact]
    public async Task Varargs()
    {
        const string code = """
            using Darp.Luau;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction(OnCall1);
                    state.CreateFunction(OnCall2);
                    state.CreateFunction(OnCall3);
                    state.CreateFunction(OnCall4);
                }

                private static void OnCall1(params string[] p1) { }
                private static void OnCall2(string[] p1) { }
                private static void OnCall3(params ReadOnlySpan<byte> p1) { }
                private static void OnCall4(ReadOnlySpan<byte> p1) { }
            }
            """;
        await VerifyHelper.VerifyGenerator(code);
    }
    */

    [Fact]
    public async Task UnsupportedType_Lambda()
    {
        const string code = """
            using Darp.Luau;
            using System.Collections.Generic;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction((object p1) => {});
                    state.CreateFunction(() => new object());
                }
            }
            """;
        await VerifyHelper.VerifyGeneratorWithErrors(code);
    }

    [Fact]
    public async Task UnsupportedType_MethodDeclaration()
    {
        const string code = """
            using Darp.Luau;
            using System.Collections.Generic;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    state.CreateFunction(MyCallback);
                }

                public static object? MyCallback(object? p1) => p1;
            }
            """;
        await VerifyHelper.VerifyGeneratorWithErrors(code);
    }

    [Fact]
    public async Task InvalidDelegateType_DelegateBase()
    {
        const string code = """
            using Darp.Luau;
            using System;

            public static class Hi
            {
                public static void DoSomething(LuauState state)
                {
                    Delegate del = null!;
                    state.CreateFunction(del);
                    state.CreateFunction<Delegate>(() => {});
                }
            }
            """;
        await VerifyHelper.VerifyGeneratorWithErrors(code);
    }
}
