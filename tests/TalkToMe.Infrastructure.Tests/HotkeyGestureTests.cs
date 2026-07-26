using TalkToMe.Infrastructure;

namespace TalkToMe.Infrastructure.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void ParsesWindowsOem102Combination()
    {
        Assert.True(HotkeyGesture.TryParse("Win+<", out HotkeyGesture gesture));
        Assert.Equal(0x0008u, gesture.Modifiers);
        Assert.Equal(0x00E2u, gesture.VirtualKey);
    }

    [Theory]
    [InlineData("Ctrl+Alt+F9", 0x0003u, 0x0078u)]
    [InlineData("Win+Shift+K", 0x000Cu, 0x004Bu)]
    public void ParsesSupportedCombinations(string value, uint modifiers, uint virtualKey)
    {
        Assert.True(HotkeyGesture.TryParse(value, out HotkeyGesture gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(virtualKey, gesture.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("F9")]
    [InlineData("Win+")]
    [InlineData("Banana+F9")]
    [InlineData("Win+Ctrl+Escape")]
    public void RejectsInvalidCombinations(string value)
    {
        Assert.False(HotkeyGesture.TryParse(value, out _));
    }
}
