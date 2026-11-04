using KidLock.Core.Hotkeys;
using KidLock.Core.Input;

namespace KidLock.Core.Tests.Hotkeys;

public class HotkeyTests
{
    private const ModifierKeys CtrlAltShift = ModifierKeys.Ctrl | ModifierKeys.Alt | ModifierKeys.Shift;

    [Theory]
    [InlineData("Ctrl+Alt+Shift+L", CtrlAltShift, 0x4C)]
    [InlineData("ctrl + alt + shift + l", CtrlAltShift, 0x4C)]
    [InlineData("Shift+Control+Alt+L", CtrlAltShift, 0x4C)]
    [InlineData("Win+Alt+F12", ModifierKeys.Win | ModifierKeys.Alt, 0x7B)]
    [InlineData("Meta+Shift+Esc", ModifierKeys.Win | ModifierKeys.Shift, 0x1B)]
    [InlineData("Ctrl+Alt+NumPad5", ModifierKeys.Ctrl | ModifierKeys.Alt, 0x65)]
    [InlineData("Ctrl+Alt+Espaço", ModifierKeys.Ctrl | ModifierKeys.Alt, 0x20)]
    [InlineData("Ctrl+Alt+7", ModifierKeys.Ctrl | ModifierKeys.Alt, 0x37)]
    [InlineData("F9", ModifierKeys.None, 0x78)]
    public void Parse_AcceptsValidText(string text, ModifierKeys modifiers, int key)
    {
        var hotkey = Hotkey.Parse(text);

        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(key, hotkey.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+L+K")]
    [InlineData("Ctrl+Ctrl+L")]
    [InlineData("Ctrl+Alt+Banana")]
    [InlineData("Ctrl++L")]
    [InlineData("Ctrl+Alt+")]
    public void TryParse_RejectsInvalidText_WithMessage(string? text)
    {
        var ok = Hotkey.TryParse(text, out var hotkey, out var error);

        Assert.False(ok);
        Assert.Null(hotkey);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Parse_InvalidText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Hotkey.Parse("Ctrl+Alt"));
    }

    [Fact]
    public void ToString_UsesCanonicalOrderAndNames()
    {
        Assert.Equal("Ctrl+Alt+Shift+Win+F5", Hotkey.Parse("shift+win+control+alt+f5").ToString());
        Assert.Equal("Ctrl+Alt+Delete", Hotkey.Parse("ctrl+alt+del").ToString());
    }

    [Fact]
    public void ToDisplayString_IsSpacedForReading()
    {
        Assert.Equal("Ctrl + Alt + Shift + L", Hotkey.Parse("Ctrl+Alt+Shift+L").ToDisplayString());
    }

    [Fact]
    public void KeysWithoutName_RoundTripAsHexCode()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl | ModifierKeys.Alt, 0xBA); // tecla de símbolo do layout

        Assert.Equal("Ctrl+Alt+0xBA", hotkey.ToString());
        Assert.Equal(hotkey, Hotkey.Parse(hotkey.ToString()));
    }

    [Theory]
    [InlineData("Ctrl+Alt+0xA0")] // é Shift: modificador não pode ser a tecla principal
    [InlineData("Ctrl+Alt+0xFF")]
    [InlineData("Ctrl+Alt+0x")]
    [InlineData("Ctrl+Alt+0xZZ")]
    public void HexCodes_ThatAreNotValidMainKeys_AreRejected(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _, out _));
    }

    [Fact]
    public void EveryKnownKeyName_RoundTrips()
    {
        foreach (var name in KeyNames.CanonicalNames)
        {
            var hotkey = Hotkey.Parse($"Ctrl+Alt+{name}");
            Assert.Equal(hotkey, Hotkey.Parse(hotkey.ToString()));
        }
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new Hotkey(CtrlAltShift, 'L'), Hotkey.Parse("Ctrl+Alt+Shift+L"));
        Assert.NotEqual(new Hotkey(CtrlAltShift, 'L'), new Hotkey(CtrlAltShift, 'K'));
    }

    [Theory]
    [InlineData(VirtualKeys.LShift)]
    [InlineData(VirtualKeys.RControl)]
    [InlineData(VirtualKeys.LWin)]
    public void Constructor_RejectsModifierAsMainKey(int key)
    {
        Assert.Throws<ArgumentException>(() => new Hotkey(ModifierKeys.Ctrl, key));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0xFF)]
    public void Constructor_RejectsOutOfRangeKey(int key)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hotkey(ModifierKeys.Ctrl, key));
    }
}
