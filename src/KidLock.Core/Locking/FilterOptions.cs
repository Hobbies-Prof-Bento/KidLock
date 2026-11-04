using KidLock.Core.Hotkeys;
using KidLock.Core.Input;

namespace KidLock.Core.Locking;

/// <summary>Parâmetros já validados que o <see cref="InputFilter"/> usa para decidir cada evento.</summary>
public sealed record FilterOptions
{
    public static readonly Hotkey DefaultHotkey = new(ModifierKeys.Ctrl | ModifierKeys.Alt | ModifierKeys.Shift, 'L');

    /// <summary>Atalho que bloqueia (só é verificado enquanto desbloqueado).</summary>
    public Hotkey LockHotkey { get; init; } = DefaultHotkey;

    /// <summary>Atalho que desbloqueia (só é verificado enquanto bloqueado). Igual ao de bloqueio = alternar.</summary>
    public Hotkey UnlockHotkey { get; init; } = DefaultHotkey;

    public bool BlockKeyboard { get; init; } = true;

    /// <summary>Mouse e touchpad: o Windows entrega os dois pelo mesmo gancho.</summary>
    public bool BlockMouse { get; init; } = true;

    /// <summary>Deixa passar entrada sintética (automação, acesso remoto) mesmo com o bloqueio ativo.</summary>
    public bool AllowInjectedInput { get; init; }

    /// <summary>
    /// Tempo sem eventos após o qual uma tecla dada como pressionada deixa de contar para o atalho.
    /// Protege contra solturas que o gancho nunca vê (ex.: Ctrl+Alt+Del).
    /// </summary>
    public TimeSpan StaleKeyTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
