using Shouldly;

namespace Darp.Luau.Tests;

public sealed class IntoLuauTests
{
    [Theory]
    [InlineData(null, IntoLuau.Kind.Nil)]
    [InlineData(false, IntoLuau.Kind.Bool)]
    internal void Into_Bool(bool? value, IntoLuau.Kind expectedKind)
    {
        IntoLuau intoValue = value;

        intoValue.Type.ShouldBe(expectedKind);
    }

    [Theory]
    [InlineData(null, IntoLuau.Kind.Nil)]
    [InlineData("test", IntoLuau.Kind.Chars)]
    internal void Into_String(string? value, IntoLuau.Kind expectedKind)
    {
        IntoLuau intoValue = value;

        intoValue.Type.ShouldBe(expectedKind);
    }

    [Theory]
    [InlineData(null, IntoLuau.Kind.Nil)]
    [InlineData(1.0d, IntoLuau.Kind.Number)]
    internal void Into_Double(double? value, IntoLuau.Kind expectedKind)
    {
        IntoLuau intoValue = value;

        intoValue.Type.ShouldBe(expectedKind);
    }

    [Theory]
    [InlineData(null, IntoLuau.Kind.Nil)]
    [InlineData(1, IntoLuau.Kind.Number)]
    internal void Into_Int32(int? value, IntoLuau.Kind expectedKind)
    {
        IntoLuau intoValue = value;

        intoValue.Type.ShouldBe(expectedKind);
    }

    [Fact]
    internal void Into_Value()
    {
        IntoLuau intoValue = default(LuauValue);

        intoValue.Type.ShouldBe(IntoLuau.Kind.Value);
    }

    [Fact]
    internal void Into_Table()
    {
        IntoLuau intoValue = default(LuauTable);

        intoValue.Type.ShouldBe(IntoLuau.Kind.Value);
    }

    [Fact]
    internal void Into_Function()
    {
        IntoLuau intoValue = default(LuauFunction);

        intoValue.Type.ShouldBe(IntoLuau.Kind.Value);
    }

    [Fact]
    internal void Into_Default()
    {
        IntoLuau intoValue = default;

        intoValue.Type.ShouldBe(IntoLuau.Kind.Nil);
    }
}
