using KidLock.Core.Hotkeys;
using KidLock.Core.Settings;

namespace KidLock.Core.Tests.Settings;

public class HotkeySettingsModelTests
{
    [Fact]
    public void SameHotkeyForBoth_StartsAsToggle()
    {
        var model = new HotkeySettingsModel(new AppSettings());

        Assert.False(model.SeparateUnlock);
        Assert.Equal("Ctrl + Alt + Shift + L", model.LockHotkey.ToDisplayString());
        Assert.Equal(model.LockHotkey, model.EffectiveUnlockHotkey);
        Assert.False(model.HasChanges);
        Assert.False(model.CanSave);
    }

    [Fact]
    public void DifferentHotkeys_StartWithSeparateUnlock()
    {
        var model = new HotkeySettingsModel(new AppSettings { LockHotkey = "Ctrl+Alt+K", UnlockHotkey = "Ctrl+Alt+Shift+U" });

        Assert.True(model.SeparateUnlock);
        Assert.Equal(Hotkey.Parse("Ctrl+Alt+Shift+U"), model.EffectiveUnlockHotkey);
    }

    [Fact]
    public void NewToggleHotkey_SavesBothFields()
    {
        var settings = new AppSettings { PlaySounds = false };
        var model = new HotkeySettingsModel(settings);

        Assert.Null(model.TrySetLockHotkey(Hotkey.Parse("Ctrl+Alt+F9")));

        Assert.True(model.CanSave);
        var saved = model.ApplyTo(settings);
        Assert.Equal("Ctrl+Alt+F9", saved.LockHotkey);
        Assert.Equal("Ctrl+Alt+F9", saved.UnlockHotkey);
        Assert.False(saved.PlaySounds); // o resto das configurações fica como estava
    }

    [Theory]
    [InlineData("F9")]
    [InlineData("Ctrl+K")]
    [InlineData("Ctrl+Alt+Delete")]
    public void UnsafeHotkey_IsRejected_AndPreviousStays(string text)
    {
        var model = new HotkeySettingsModel(new AppSettings());

        var error = model.TrySetLockHotkey(Hotkey.Parse(text));

        Assert.NotNull(error);
        Assert.Contains("não serve", error, StringComparison.Ordinal);
        Assert.Equal(Hotkey.Parse(AppSettings.DefaultHotkey), model.LockHotkey);
        Assert.False(model.HasChanges);
    }

    [Fact]
    public void SeparateUnlock_WithSameHotkey_CannotBeSaved()
    {
        var model = new HotkeySettingsModel(new AppSettings()) { SeparateUnlock = true };

        Assert.NotNull(model.ValidationError);
        Assert.False(model.CanSave);

        model.TrySetUnlockHotkey(Hotkey.Parse("Ctrl+Alt+Shift+U"));

        Assert.Null(model.ValidationError);
        Assert.True(model.CanSave);
        Assert.Equal("Ctrl+Alt+Shift+U", model.ApplyTo(new AppSettings()).UnlockHotkey);
    }

    [Fact]
    public void TurningSeparateUnlockOff_GoesBackToToggle()
    {
        var model = new HotkeySettingsModel(new AppSettings { LockHotkey = "Ctrl+Alt+K", UnlockHotkey = "Ctrl+Alt+Shift+U" })
        {
            SeparateUnlock = false,
        };

        var saved = model.ApplyTo(new AppSettings());

        Assert.Equal("Ctrl+Alt+K", saved.UnlockHotkey);
        Assert.True(model.HasChanges);
    }
}
