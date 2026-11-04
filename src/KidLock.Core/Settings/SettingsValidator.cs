using KidLock.Core.Hotkeys;
using KidLock.Core.Locking;

namespace KidLock.Core.Settings;

/// <param name="Settings">Configurações efetivas (valores inválidos já trocados pelo padrão, atalhos normalizados).</param>
/// <param name="FilterOptions">Opções prontas para o <see cref="InputFilter"/>.</param>
/// <param name="Warnings">Problemas encontrados, em texto para o usuário.</param>
public sealed record SettingsLoadResult(AppSettings Settings, FilterOptions FilterOptions, IReadOnlyList<string> Warnings);

public static class SettingsValidator
{
    /// <summary>Nunca falha: o que estiver inválido volta ao padrão e vira aviso.</summary>
    public static SettingsLoadResult Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var warnings = new List<string>();

        var lockHotkey = ResolveHotkey(settings.LockHotkey, "lockHotkey", warnings);
        var unlockHotkey = ResolveHotkey(settings.UnlockHotkey, "unlockHotkey", warnings);

        if (!settings.BlockKeyboard && !settings.BlockMouse)
        {
            warnings.Add("blockKeyboard e blockMouse estão desligados: o bloqueio não terá efeito.");
        }

        var effective = settings with
        {
            LockHotkey = lockHotkey.ToString(),
            UnlockHotkey = unlockHotkey.ToString(),
        };

        var options = new FilterOptions
        {
            LockHotkey = lockHotkey,
            UnlockHotkey = unlockHotkey,
            BlockKeyboard = settings.BlockKeyboard,
            BlockMouse = settings.BlockMouse,
            AllowInjectedInput = settings.AllowInjectedInput,
        };

        return new SettingsLoadResult(effective, options, warnings);
    }

    private static Hotkey ResolveHotkey(string? text, string field, List<string> warnings)
    {
        var fallback = FilterOptions.DefaultHotkey;
        if (!Hotkey.TryParse(text, out var hotkey, out var error))
        {
            warnings.Add($"{field}: {error} Usando o padrão {fallback}.");
            return fallback;
        }

        var problems = HotkeyPolicy.Validate(hotkey);
        if (problems.Count > 0)
        {
            warnings.Add($"{field}: {string.Join(" ", problems)} Usando o padrão {fallback}.");
            return fallback;
        }

        return hotkey;
    }
}
