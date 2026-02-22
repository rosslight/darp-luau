namespace Darp.Luau.Tests.Fixtures;

internal sealed class ValueUserdata : ILuauUserData<ValueUserdata>
{
    public int Value { get; set; }

    public static LuauReturnSingle OnIndex(ValueUserdata self, in LuauState state, in ReadOnlySpan<char> fieldName) =>
        LuauReturnSingle.NotHandled;

    public static LuauOutcome OnSetIndex(ValueUserdata self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName) =>
        LuauOutcome.NotHandledError;

    public static LuauReturn OnMethodCall(
        ValueUserdata self,
        LuauArgs functionArgs,
        in ReadOnlySpan<char> methodName
    ) => LuauReturn.NotHandledError;

    public static implicit operator IntoLuau(ValueUserdata value) => IntoLuau.FromUserdata(value);
}

internal sealed class OtherValueUserdata : ILuauUserData<OtherValueUserdata>
{
    public static LuauReturnSingle OnIndex(
        OtherValueUserdata self,
        in LuauState state,
        in ReadOnlySpan<char> fieldName
    ) => LuauReturnSingle.NotHandled;

    public static LuauOutcome OnSetIndex(
        OtherValueUserdata self,
        LuauArgsSingle args,
        in ReadOnlySpan<char> fieldName
    ) => LuauOutcome.NotHandledError;

    public static LuauReturn OnMethodCall(
        OtherValueUserdata self,
        LuauArgs functionArgs,
        in ReadOnlySpan<char> methodName
    ) => LuauReturn.NotHandledError;

    public static implicit operator IntoLuau(OtherValueUserdata value) => IntoLuau.FromUserdata(value);
}
