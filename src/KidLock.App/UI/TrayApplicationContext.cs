using System.Diagnostics;
using System.Media;
using System.Security;
using KidLock.App.Hooks;
using KidLock.App.Infrastructure;
using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Settings;
using Microsoft.Win32;

namespace KidLock.App.UI;

/// <summary>
/// Raiz de composição: liga configurações, núcleo de bloqueio, ganchos de entrada e a interface
/// de bandeja. Roda na thread de UI; os callbacks dos ganchos só fazem Post para cá.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan OverlayDuration = TimeSpan.FromSeconds(3);
    private const long BlockedFeedbackIntervalMs = 1000;
    private const int MaxBalloonLength = 250;

    private readonly FileLogger _logger;
    private readonly SynchronizationContext _ui;
    private readonly SettingsStore _store;
    private readonly LockController _controller = new();
    private readonly InputFilter _filter;
    private readonly InputHookHost _hooks;
    private readonly AutoStartManager _autoStart;
    private readonly LockOverlayForm _overlay = new();
    private readonly Icon _lockedIcon;
    private readonly Icon _unlockedIcon;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _lockItem;
    private readonly ToolStripMenuItem _unlockItem;
    private readonly ToolStripMenuItem _blockKeyboardItem;
    private readonly ToolStripMenuItem _blockMouseItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly FileSystemWatcher _settingsWatcher;
    private readonly System.Windows.Forms.Timer _reloadDebounce = new() { Interval = 400 };
    private AppSettings _settings;
    private bool? _displayedLocked;
    private long _lastBlockedFeedback;
    private HotkeySettingsForm? _hotkeyForm;

    public TrayApplicationContext(FileLogger logger, SynchronizationContext ui)
    {
        _logger = logger;
        _ui = ui;
        _store = new SettingsStore(AppPaths.SettingsFile);
        var firstRun = !File.Exists(_store.FilePath);
        var loaded = _store.Load();
        _settings = loaded.Settings;
        _filter = new InputFilter(_controller, loaded.FilterOptions);
        _autoStart = new AutoStartManager("KidLock", Environment.ProcessPath ?? Application.ExecutablePath);

        _controller.StateChanged += OnLockStateChanged;

        // Ganchos primeiro: se falharem, o construtor lança antes de criar qualquer ícone na bandeja.
        _hooks = new InputHookHost(OnKeyboard, OnMouse, ex => _logger.Error("Erro ao processar evento de entrada", ex));
        _hooks.Start();

        var iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
        _lockedIcon = IconFactory.CreateIcon(locked: true, iconSize);
        _unlockedIcon = IconFactory.CreateIcon(locked: false, iconSize);

        _lockItem = new ToolStripMenuItem("Bloquear agora", null, (_, _) => _controller.Lock(LockReason.UserCommand));
        _unlockItem = new ToolStripMenuItem("Desbloquear", null, (_, _) => _controller.Unlock(LockReason.UserCommand));
        _blockKeyboardItem = new ToolStripMenuItem("Bloquear teclado", null, (_, _) => UpdateSettings(s => s with { BlockKeyboard = !s.BlockKeyboard }));
        _blockMouseItem = new ToolStripMenuItem("Bloquear mouse e touchpad", null, (_, _) => UpdateSettings(s => s with { BlockMouse = !s.BlockMouse }));
        _autoStartItem = new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => ToggleAutoStart());

        var menu = new ContextMenuStrip();
        var hotkeysItem = new ToolStripMenuItem("Atalhos de teclado...", null, (_, _) => ShowHotkeyWindow());
        hotkeysItem.Font = new Font(hotkeysItem.Font, FontStyle.Bold); // ação do duplo clique
        menu.Items.AddRange(
        [
            hotkeysItem,
            new ToolStripSeparator(),
            _lockItem,
            _unlockItem,
            new ToolStripSeparator(),
            _blockKeyboardItem,
            _blockMouseItem,
            _autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Editar configurações...", null, (_, _) => OpenSettingsFile()),
            new ToolStripMenuItem("Abrir pasta de logs", null, (_, _) => OpenFolder(_logger.LogDirectory)),
            new ToolStripMenuItem("Sobre o KidLock", null, (_, _) => ShowAbout()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Sair", null, (_, _) => ExitThread()),
        ]);
        menu.Opening += (_, _) => _autoStartItem.Checked = SafeIsAutoStartEnabled();

        _tray = new NotifyIcon { ContextMenuStrip = menu, Icon = _unlockedIcon, Visible = true };

        // Duplo clique mostra os atalhos (em vez de bloquear): ninguém bloqueia sem saber como sair.
        _tray.DoubleClick += (_, _) => ShowHotkeyWindow();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        _settingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(_store.FilePath)!)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _settingsWatcher.Changed += OnSettingsFileEvent;
        _settingsWatcher.Created += OnSettingsFileEvent;
        _settingsWatcher.Renamed += OnSettingsFileEvent;
        _settingsWatcher.EnableRaisingEvents = true;
        _reloadDebounce.Tick += (_, _) =>
        {
            _reloadDebounce.Stop();
            ReloadSettings();
        };

        ApplySettings(loaded, announceChanges: false);
        RefreshLockState();
        _logger.Info($"KidLock iniciado. Bloquear: {_settings.LockHotkey}. Desbloquear: {_settings.UnlockHotkey}.");
        if (firstRun)
        {
            ShowHotkeyWindow();
        }
        else if (loaded.Warnings.Count == 0)
        {
            ShowBalloon("KidLock está ativo", HotkeySummary() + " Duplo clique no cadeado mostra os atalhos.", ToolTipIcon.Info);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Ganchos primeiro: ao sair, o teclado volta na hora, aconteça o que acontecer depois.
            _hooks.Dispose();
            _hotkeyForm?.Dispose();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _settingsWatcher.Dispose();
            _reloadDebounce.Dispose();
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _overlay.Dispose();
            _lockedIcon.Dispose();
            _unlockedIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    // ---- Thread do gancho: apenas trabalho mínimo e sem bloqueio ----

    private KeyboardResult OnKeyboard(KeyboardInput input)
    {
        var result = _filter.ProcessKeyboard(input);
        if (result.Decision == InputDecision.Block)
        {
            SignalBlockedInput();
        }

        return result;
    }

    private InputDecision OnMouse(MouseInput input)
    {
        var decision = _filter.ProcessMouse(input);
        if (decision == InputDecision.Block)
        {
            SignalBlockedInput();
        }

        return decision;
    }

    private void SignalBlockedInput()
    {
        // Um movimento de touchpad gera centenas de eventos: avisamos a UI no máximo 1x por segundo.
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastBlockedFeedback);
        if (now - last < BlockedFeedbackIntervalMs
            || Interlocked.CompareExchange(ref _lastBlockedFeedback, now, last) != last)
        {
            return;
        }

        _ui.Post(_ => OnBlockedInput(), null);
    }

    private void OnLockStateChanged(object? sender, LockStateChangedEventArgs e)
    {
        _logger.Info($"{(e.IsLocked ? "Bloqueado" : "Desbloqueado")} (motivo: {e.Reason}).");
        _ui.Post(_ => RefreshLockState(), null);
    }

    // ---- Thread de UI ----

    private void OnBlockedInput()
    {
        if (_controller.IsLocked && _settings.ShowOverlay)
        {
            _overlay.Flash(OverlayDuration);
        }
    }

    private void RefreshLockState()
    {
        var locked = _controller.IsLocked;
        if (_displayedLocked == locked)
        {
            return;
        }

        var isFirstRefresh = _displayedLocked is null;
        _displayedLocked = locked;
        _tray.Icon = locked ? _lockedIcon : _unlockedIcon;
        UpdateTrayText();
        _lockItem.Enabled = !locked;
        _unlockItem.Enabled = locked;

        if (locked && _settings.ShowOverlay)
        {
            _overlay.Flash(OverlayDuration);
        }
        else
        {
            _overlay.HideOverlay();
        }

        if (!isFirstRefresh && _settings.PlaySounds)
        {
            (locked ? SystemSounds.Exclamation : SystemSounds.Asterisk).Play();
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                if (_settings.UnlockOnSessionLock)
                {
                    _controller.Unlock(LockReason.SessionLocked);
                }

                _filter.ResetInputState();
                break;

            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                _filter.ResetInputState();
                _hooks.Rearm();
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _filter.ResetInputState();
            _hooks.Rearm();
        }
    }

    private void OnSettingsFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(e.FullPath, _store.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Editores disparam vários eventos por gravação: esperamos assentar antes de recarregar.
        _ui.Post(_ =>
        {
            _reloadDebounce.Stop();
            _reloadDebounce.Start();
        }, null);
    }

    private void ReloadSettings()
    {
        SettingsLoadResult loaded;
        try
        {
            loaded = _store.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Falha ao recarregar configurações", ex);
            return;
        }

        if (loaded.Settings != _settings || loaded.Warnings.Count > 0)
        {
            ApplySettings(loaded, announceChanges: true);
        }
    }

    private void ApplySettings(SettingsLoadResult loaded, bool announceChanges)
    {
        _settings = loaded.Settings;
        _filter.UpdateOptions(loaded.FilterOptions);

        _lockItem.ShortcutKeyDisplayString = _settings.LockHotkey;
        _unlockItem.ShortcutKeyDisplayString = _settings.UnlockHotkey;
        UpdateTrayText();
        _blockKeyboardItem.Checked = _settings.BlockKeyboard;
        _blockMouseItem.Checked = _settings.BlockMouse;
        _overlay.Hint = _settings.ShowHotkeyHintInOverlay ? $"Para desbloquear: {_settings.UnlockHotkey}" : null;
        if (!_settings.ShowOverlay)
        {
            _overlay.HideOverlay();
        }

        foreach (var warning in loaded.Warnings)
        {
            _logger.Warn(warning);
        }

        if (loaded.Warnings.Count > 0)
        {
            ShowBalloon("KidLock: verifique as configurações", string.Join(Environment.NewLine, loaded.Warnings), ToolTipIcon.Warning);
        }
        else if (announceChanges)
        {
            ShowBalloon("KidLock", "Configurações atualizadas. " + HotkeySummary(), ToolTipIcon.Info);
        }
    }

    private void ShowHotkeyWindow()
    {
        if (_hotkeyForm is { IsDisposed: false })
        {
            _hotkeyForm.Activate();
            return;
        }

        _hotkeyForm = new HotkeySettingsForm(
            _settings,
            _filter,
            _ui,
            save: saved => UpdateSettings(_ => saved, announce: true),
            lockNow: () => _controller.Lock(LockReason.UserCommand));
        _hotkeyForm.FormClosed += (_, _) => _hotkeyForm = null; // janela não modal: o Close já descarta
        _hotkeyForm.Show();
    }

    // Passar o mouse sobre o cadeado já ensina o atalho.
    private void UpdateTrayText() =>
        _tray.Text = _controller.IsLocked
            ? $"KidLock: BLOQUEADO. Para desbloquear: {_settings.UnlockHotkey}"
            : $"KidLock: livre. Para bloquear: {_settings.LockHotkey}";

    private void UpdateSettings(Func<AppSettings, AppSettings> change, bool announce = false)
    {
        var updated = change(_settings);
        try
        {
            _store.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Falha ao salvar configurações", ex);
            MessageBox.Show($"Não foi possível salvar as configurações:\n{ex.Message}", "KidLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ApplySettings(SettingsValidator.Validate(updated), announceChanges: announce);
    }

    private bool SafeIsAutoStartEnabled()
    {
        try
        {
            return _autoStart.IsEnabled;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.Error("Falha ao ler o início automático", ex);
            return false;
        }
    }

    private void ToggleAutoStart()
    {
        try
        {
            var enable = !_autoStart.IsEnabled;
            _autoStart.SetEnabled(enable);
            _logger.Info(enable ? "Início automático ligado." : "Início automático desligado.");
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.Error("Falha ao alterar o início automático", ex);
            MessageBox.Show($"Não foi possível alterar o início automático:\n{ex.Message}", "KidLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettingsFile() =>
        StartProcess("notepad.exe", $"\"{_store.FilePath}\"");

    private void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        StartProcess("explorer.exe", $"\"{path}\"");
    }

    private void StartProcess(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.Error($"Falha ao abrir {fileName}", ex);
        }
    }

    private void ShowAbout()
    {
        var version = typeof(TrayApplicationContext).Assembly.GetName().Version?.ToString(3) ?? "?";
        MessageBox.Show(
            $"KidLock {version}\n\n{HotkeySummary()}\n\n" +
            "Esqueceu o atalho? Pressione Ctrl+Alt+Del, escolha \"Bloquear\" e entre de novo com sua senha: " +
            "o teclado e o touchpad são liberados automaticamente.\n\n" +
            $"Configurações: {_store.FilePath}",
            "Sobre o KidLock",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private string HotkeySummary() =>
        _settings.LockHotkey == _settings.UnlockHotkey
            ? $"{_settings.LockHotkey} bloqueia e desbloqueia teclado e touchpad."
            : $"{_settings.LockHotkey} bloqueia; {_settings.UnlockHotkey} desbloqueia.";

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        if (text.Length > MaxBalloonLength)
        {
            text = text[..(MaxBalloonLength - 1)] + "…";
        }

        _tray.ShowBalloonTip(5000, title, text, icon);
    }
}
