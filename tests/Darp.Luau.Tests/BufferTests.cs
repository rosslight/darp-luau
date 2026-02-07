using Shouldly;

namespace Darp.Luau.Tests;

public sealed class BufferTests
{
    [Fact]
    public void Create_And_Get()
    {
        byte[] expected = [0x01, 0x02, 0x03];

        using var state = new LuauState();

        LuauBuffer buffer = state.CreateBuffer(expected);

        buffer.TryGet(out byte[] found).ShouldBeTrue();
        found.ShouldBe<byte>(expected);
    }

    [Fact]
    public void TryGet_Bytes()
    {
        byte[] expected = [0x01, 0x02, 0x03];

        using var state = new LuauState();

        LuauValue value = state.CreateBuffer(expected);
        value.Type.ShouldBe(LuauValueType.Buffer);

        value.TryGet(out byte[]? found).ShouldBeTrue();
        found.ShouldBe<byte>(expected);
    }

    [Fact]
    public void TryGet_Span()
    {
        ReadOnlySpan<byte> expected = new ([0x01, 0x02, 0x03]);

        using var state = new LuauState();

        LuauValue value = state.CreateBuffer(expected);
        value.Type.ShouldBe(LuauValueType.Buffer);

        value.TryGet(out ReadOnlySpan<byte> found).ShouldBeTrue();
        found.ToArray().ShouldBe<byte>(expected.ToArray());
    }

    [Fact]
    public void TryGet_Buffer()
    {
        ReadOnlySpan<byte> expected = new ([0x01, 0x02, 0x03]);

        using var state = new LuauState();

        LuauValue value = state.CreateBuffer(expected);
        value.Type.ShouldBe(LuauValueType.Buffer);

        value.TryGet(out LuauBuffer found).ShouldBeTrue();
        found.ToString().ShouldBeEquivalentTo(Convert.ToHexString(expected));
    }

    [Fact]
    public void To_String()
    {
        byte[] expected = [0x01, 0x02, 0x03];

        using var state = new LuauState();

        LuauBuffer buffer = state.CreateBuffer(expected);
        buffer.ToString().ShouldBeEquivalentTo(Convert.ToHexString(expected));
    }
}
