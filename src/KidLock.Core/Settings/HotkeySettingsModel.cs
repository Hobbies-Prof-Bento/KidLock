using KidLock.Core.Hotkeys;
using KidLock.Core.Locking;

namespace KidLock.Core.Settings;

/// <summary>
/// Regras da janela "Atalhos de teclado", separadas da interface para serem testáveis:
/// atalhos pendentes, validação e o que salvar.
/// </summary>
public sealed class HotkeySettingsModel
{
    private readonly Hotkey _savedLock;
    private readonly Hotkey _savedUnlock;

    /// <param name="settings">Configurações já validadas (as que estão em uso).</param>
    public HotkeySettingsModel(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _savedLock = ParseOrDefault(settings.LockHotkey);
        _savedUnlock = ParseOrDefault(settings.UnlockHotkey);
        LockHotkey = _savedLock;
        UnlockHotkey = _savedUnlock;
        SeparateUnlock = _savedLock != _savedUnlock;
    }

    public Hotkey LockHotkey { get; private set; }

    /// <summary>Atalho escolhido para desbloquear; só vale quando <see cref="SeparateUnlock"/> está ligado.</summary>
    public Hotkey UnlockHotkey { get; private set; }

    /// <summary>Usar um atalho diferente para desbloquear (desligado: o mesmo atalho alterna).</summary>
    public bool SeparateUnlock { get; set; }

    public Hotkey EffectiveUnlockHotkey => SeparateUnlock ? UnlockHotkey : LockHotkey;

    /// <summary>Problema que impede salvar, ou null.</summary>
    public string? ValidationError =>
        SeparateUnlock && UnlockHotkey == LockHotkey
            ? "O atalho de desbloquear está igual ao de bloquear: escolha outro ou desmarque a opção."
            : null;

    public bool HasChanges => LockHotkey != _savedLock || EffectiveUnlockHotkey != _savedUnlock;

    public bool CanSave => HasChanges && ValidationError is null;

    /// <returns>null se aceito; senão, o motivo da recusa (o atalho anterior continua).</returns>
    public string? TrySetLockHotkey(Hotkey hotkey) => TrySet(hotkey, h => LockHotkey = h);

    /// <returns>null se aceito; senão, o motivo da recusa (o atalho anterior continua).</returns>
    public string? TrySetUnlockHotkey(Hotkey hotkey) => TrySet(hotkey, h => UnlockHotkey = h);

    public AppSettings ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            LockHotkey = LockHotkey.ToString(),
            UnlockHotkey = EffectiveUnlockHotkey.ToString(),
        };
    }

    private static string? TrySet(Hotkey hotkey, Action<Hotkey> assign)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        var problems = HotkeyPolicy.Validate(hotkey);
        if (problems.Count > 0)
        {
            return $"\"{hotkey.ToDisplayString()}\" não serve: {string.Join(" ", problems)}";
        }

        assign(hotkey);
        return null;
    }

    private static Hotkey ParseOrDefault(string text) =>
        Hotkey.TryParse(text, out var hotkey, out _) ? hotkey : FilterOptions.DefaultHotkey;
}
