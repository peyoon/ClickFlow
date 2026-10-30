using ClickFlow.Core;
using Xunit;

namespace ClickFlow.Tests;

public class VirtualKeysTests
{
    [Theory]
    [InlineData("enter", 0x0D)]
    [InlineData("Return", 0x0D)]
    [InlineData("F12", 0x7B)]
    [InlineData("a", 0x41)]
    [InlineData("numpad5", 0x65)]
    [InlineData("space", 0x20)]
    public void KnownNames_Resolve(string name, int expected)
    {
        Assert.Equal((ushort)expected, VirtualKeys.GetCode(name));
        Assert.True(VirtualKeys.TryGetCode(name, out var code));
        Assert.Equal((ushort)expected, code);
    }

    [Fact]
    public void UnknownName_Throws()
    {
        Assert.Throws<ArgumentException>(() => VirtualKeys.GetCode("NOPE_KEY"));
        Assert.False(VirtualKeys.TryGetCode("NOPE_KEY", out _));
    }
}
