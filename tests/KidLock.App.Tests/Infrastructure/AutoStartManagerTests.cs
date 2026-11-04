using KidLock.App.Infrastructure;
using Microsoft.Win32;

namespace KidLock.App.Tests.Infrastructure;

/// <summary>Usa uma chave temporária em HKCU, nunca a chave Run real.</summary>
public sealed class AutoStartManagerTests : IDisposable
{
    private readonly string _keyPath = $@"Software\KidLock.Tests-{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    [Fact]
    public void EnableThenDisable_WritesAndRemovesQuotedCommand()
    {
        var manager = new AutoStartManager("KidLock", @"C:\Apps\Kid Lock\KidLock.exe", _keyPath);
        Assert.False(manager.IsEnabled);

        manager.SetEnabled(true);
        Assert.True(manager.IsEnabled);
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath))
        {
            Assert.Equal("\"C:\\Apps\\Kid Lock\\KidLock.exe\"", key?.GetValue("KidLock"));
        }

        manager.SetEnabled(false);
        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void EntryPointingToAnotherCopy_IsNotConsideredEnabled()
    {
        new AutoStartManager("KidLock", @"C:\Old\KidLock.exe", _keyPath).SetEnabled(true);

        Assert.False(new AutoStartManager("KidLock", @"C:\New\KidLock.exe", _keyPath).IsEnabled);
    }

    [Fact]
    public void Disable_WhenNothingRegistered_DoesNotThrow()
    {
        new AutoStartManager("KidLock", @"C:\KidLock.exe", _keyPath).SetEnabled(false);
    }
}
