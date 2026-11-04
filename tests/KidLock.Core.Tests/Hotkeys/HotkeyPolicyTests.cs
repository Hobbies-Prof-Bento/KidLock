using KidLock.Core.Hotkeys;
using KidLock.Core.Locking;

namespace KidLock.Core.Tests.Hotkeys;

public class HotkeyPolicyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Shift+L")]
    [InlineData("Ctrl+Alt+F12")]
    [InlineData("Win+Shift+K")]
    [InlineData("Ctrl+Shift+Alt+Win+P")]
    public void Validate_AcceptsStrongHotkeys(string text)
    {
        Assert.Empty(HotkeyPolicy.Validate(Hotkey.Parse(text)));
    }

    [Theory]
    [InlineData("L")]
    [InlineData("Ctrl+L")]
    [InlineData("Shift+F1")]
    public void Validate_RejectsHotkeysAChildCouldHitByAccident(string text)
    {
        Assert.NotEmpty(HotkeyPolicy.Validate(Hotkey.Parse(text)));
    }

    [Theory]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Ctrl+Alt+Shift+Delete")]
    [InlineData("Win+Alt+L")]
    [InlineData("Ctrl+Shift+Esc")]
    public void Validate_RejectsCombosReservedByWindows(string text)
    {
        Assert.Contains(HotkeyPolicy.Validate(Hotkey.Parse(text)), p => p.Contains("reservado", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultHotkey_IsValid()
    {
        Assert.Empty(HotkeyPolicy.Validate(FilterOptions.DefaultHotkey));
    }
}
