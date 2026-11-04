using System.Runtime.InteropServices;
using KidLock.App.Native;

namespace KidLock.App.Tests.Native;

/// <summary>Se o layout das structs divergir do Win32, SendInput falha em silêncio ou o gancho lê lixo.</summary>
public class NativeLayoutTests
{
    [Fact]
    public void StructSizes_MatchWin32()
    {
        var is64Bit = IntPtr.Size == 8;

        Assert.Equal(is64Bit ? 40 : 28, Marshal.SizeOf<NativeMethods.INPUT>());
        Assert.Equal(is64Bit ? 24 : 20, Marshal.SizeOf<NativeMethods.KBDLLHOOKSTRUCT>());
        Assert.Equal(is64Bit ? 32 : 24, Marshal.SizeOf<NativeMethods.MSLLHOOKSTRUCT>());
    }

    [Fact]
    public void KeyboardInputHelper_FillsKeyboardUnion()
    {
        var input = NativeMethods.KeyboardInput(0x87, keyUp: true, extraInfo: 42);

        Assert.Equal(NativeMethods.INPUT_KEYBOARD, input.type);
        Assert.Equal(0x87, input.data.ki.wVk);
        Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, input.data.ki.dwFlags);
        Assert.Equal((nuint)42, input.data.ki.dwExtraInfo);
    }
}
