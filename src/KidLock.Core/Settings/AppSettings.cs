namespace KidLock.Core.Settings;

/// <summary>Configurações persistidas em settings.json (pensado para ser editável à mão).</summary>
public sealed record AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+Shift+L";

    public string LockHotkey { get; init; } = DefaultHotkey;

    public string UnlockHotkey { get; init; } = DefaultHotkey;

    public bool BlockKeyboard { get; init; } = true;

    public bool BlockMouse { get; init; } = true;

    public bool AllowInjectedInput { get; init; }

    /// <summary>Mostra um aviso discreto no topo da tela enquanto bloqueado.</summary>
    public bool ShowOverlay { get; init; } = true;

    /// <summary>Inclui o atalho de desbloqueio no aviso (desligado: crianças maiores sabem ler).</summary>
    public bool ShowHotkeyHintInOverlay { get; init; }

    public bool PlaySounds { get; init; } = true;

    /// <summary>
    /// Ao bloquear a sessão do Windows (Win+L ou Ctrl+Alt+Del → Bloquear), libera a entrada.
    /// É a saída de emergência: quem desbloqueia a sessão precisou digitar a senha.
    /// </summary>
    public bool UnlockOnSessionLock { get; init; } = true;
}
