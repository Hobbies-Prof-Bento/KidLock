using KidLock.App.Hooks;
using KidLock.App.Native;
using KidLock.Core.Input;

namespace KidLock.App.Tests.Hooks;

public class HookEventMapperTests
{
    [Theory]
    [InlineData(NativeMethods.WM_KEYDOWN, true)]
    [InlineData(NativeMethods.WM_SYSKEYDOWN, true)]
    [InlineData(NativeMethods.WM_KEYUP, false)]
    [InlineData(NativeMethods.WM_SYSKEYUP, false)]
    public void Keyboard_MapsDownAndUpMessages(int message, bool expectedDown)
    {
        Assert.True(HookEventMapper.TryMapKeyboard(message, 0x41, 0, out var input));
        Assert.Equal(new KeyboardInput(0x41, expectedDown, IsInjected: false), input);
    }

    [Fact]
    public void Keyboard_ReadsInjectedFlag()
    {
        HookEventMapper.TryMapKeyboard(NativeMethods.WM_KEYDOWN, 0x41, NativeMethods.LLKHF_INJECTED, out var input);

        Assert.True(input.IsInjected);
    }

    [Fact]
    public void Keyboard_UnknownMessage_IsNotMapped()
    {
        Assert.False(HookEventMapper.TryMapKeyboard(NativeMethods.WM_MOUSEMOVE, 0x41, 0, out _));
    }

    [Theory]
    [InlineData(NativeMethods.WM_MOUSEMOVE, 0u, MouseAction.Move, MouseButton.None)]
    [InlineData(NativeMethods.WM_LBUTTONDOWN, 0u, MouseAction.ButtonDown, MouseButton.Left)]
    [InlineData(NativeMethods.WM_LBUTTONUP, 0u, MouseAction.ButtonUp, MouseButton.Left)]
    [InlineData(NativeMethods.WM_RBUTTONDOWN, 0u, MouseAction.ButtonDown, MouseButton.Right)]
    [InlineData(NativeMethods.WM_RBUTTONUP, 0u, MouseAction.ButtonUp, MouseButton.Right)]
    [InlineData(NativeMethods.WM_MBUTTONDOWN, 0u, MouseAction.ButtonDown, MouseButton.Middle)]
    [InlineData(NativeMethods.WM_MBUTTONUP, 0u, MouseAction.ButtonUp, MouseButton.Middle)]
    [InlineData(NativeMethods.WM_XBUTTONDOWN, 0x0001_0000u, MouseAction.ButtonDown, MouseButton.X1)]
    [InlineData(NativeMethods.WM_XBUTTONUP, 0x0002_0000u, MouseAction.ButtonUp, MouseButton.X2)]
    [InlineData(NativeMethods.WM_MOUSEWHEEL, 0x0078_0000u, MouseAction.Wheel, MouseButton.None)]
    [InlineData(NativeMethods.WM_MOUSEHWHEEL, 0xFF88_0000u, MouseAction.HorizontalWheel, MouseButton.None)]
    public void Mouse_MapsEveryLowLevelMessage(int message, uint mouseData, MouseAction action, MouseButton button)
    {
        Assert.True(HookEventMapper.TryMapMouse(message, mouseData, 0, out var input));
        Assert.Equal(new MouseInput(action, button, IsInjected: false), input);
    }

    [Fact]
    public void Mouse_ReadsInjectedFlag()
    {
        HookEventMapper.TryMapMouse(NativeMethods.WM_MOUSEMOVE, 0, NativeMethods.LLMHF_INJECTED, out var input);

        Assert.True(input.IsInjected);
    }

    [Fact]
    public void Mouse_UnknownMessage_IsNotMapped()
    {
        Assert.False(HookEventMapper.TryMapMouse(NativeMethods.WM_KEYDOWN, 0, 0, out _));
    }
}
