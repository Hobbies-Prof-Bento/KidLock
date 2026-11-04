using KidLock.Core.Hotkeys;
using KidLock.Core.Locking;
using KidLock.Core.Settings;
using KidLock.Core.Tests.TestSupport;

namespace KidLock.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"KidLock.Tests-{Guid.NewGuid():N}");

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesItWithDefaults()
    {
        var result = new SettingsStore(SettingsPath).Load();

        Assert.True(File.Exists(SettingsPath));
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.Equal(new FilterOptions(), result.FilterOptions);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = new AppSettings
        {
            LockHotkey = "Ctrl+Alt+K",
            UnlockHotkey = "Ctrl+Alt+Shift+U",
            BlockMouse = false,
            AllowInjectedInput = true,
            ShowHotkeyHintInOverlay = true,
            PlaySounds = false,
            UnlockOnSessionLock = false,
        };

        store.Save(settings);
        var result = store.Load();

        Assert.Equal(settings, result.Settings);
        Assert.Equal(Hotkey.Parse("Ctrl+Alt+K"), result.FilterOptions.LockHotkey);
        Assert.False(result.FilterOptions.BlockMouse);
        Assert.True(result.FilterOptions.AllowInjectedInput);
    }

    [Fact]
    public void File_UsesCamelCaseNames()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings());

        var json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"lockHotkey\": \"Ctrl+Alt+Shift+L\"", json, StringComparison.Ordinal);
        Assert.Contains("\"unlockOnSessionLock\": true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AcceptsCommentsTrailingCommasAndPartialFiles()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """
            {
              // só o que eu quero mudar
              "blockMouse": false,
            }
            """);

        var result = new SettingsStore(SettingsPath).Load();

        Assert.False(result.Settings.BlockMouse);
        Assert.True(result.Settings.BlockKeyboard);
        Assert.Equal(AppSettings.DefaultHotkey, result.Settings.LockHotkey);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Load_CorruptFile_KeepsBackupAndRestoresDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ isto não é json");
        var time = new FakeTimeProvider();

        var result = new SettingsStore(SettingsPath, time).Load();

        Assert.Equal(new AppSettings(), result.Settings);
        Assert.Single(result.Warnings);
        var backup = Assert.Single(Directory.GetFiles(_directory, "settings.json.invalido-*"));
        Assert.Equal("{ isto não é json", File.ReadAllText(backup));
        Assert.Equal(new AppSettings(), new SettingsStore(SettingsPath).Load().Settings);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Banana")]
    [InlineData("Ctrl+L")]
    [InlineData("Ctrl+Alt+Delete")]
    public void Load_InvalidOrUnsafeHotkey_FallsBackToDefaultWithWarning(string hotkey)
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppSettings { LockHotkey = hotkey });

        var result = store.Load();

        Assert.Equal(AppSettings.DefaultHotkey, result.Settings.LockHotkey);
        Assert.Equal(FilterOptions.DefaultHotkey, result.FilterOptions.LockHotkey);
        Assert.Contains(result.Warnings, w => w.StartsWith("lockHotkey:", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_NormalizesHotkeyText()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppSettings { UnlockHotkey = "shift + ctrl + alt + u" });

        Assert.Equal("Ctrl+Alt+Shift+U", store.Load().Settings.UnlockHotkey);
    }

    [Fact]
    public void Load_NullHotkey_FallsBackWithWarning()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "lockHotkey": null }""");

        var result = new SettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.DefaultHotkey, result.Settings.LockHotkey);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Load_NothingToBlock_Warns()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppSettings { BlockKeyboard = false, BlockMouse = false });

        Assert.Contains(store.Load().Warnings, w => w.Contains("não terá efeito", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        var store = new SettingsStore(SettingsPath);

        store.Save(new AppSettings());
        store.Save(new AppSettings { PlaySounds = false });

        Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(_directory).Select(f => Path.GetFileName(f)));
    }

    [Fact]
    public void DefaultHotkeyText_MatchesFilterDefault()
    {
        Assert.Equal(FilterOptions.DefaultHotkey, Hotkey.Parse(AppSettings.DefaultHotkey));
    }
}
