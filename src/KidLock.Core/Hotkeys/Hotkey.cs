using System.Diagnostics.CodeAnalysis;
using KidLock.Core.Input;

namespace KidLock.Core.Hotkeys;

/// <summary>Combinação de modificadores + uma tecla principal, ex.: Ctrl+Alt+Shift+L.</summary>
public sealed record Hotkey
{
    private static readonly (ModifierKeys Flag, string Name)[] ModifierOrder =
    [
        (ModifierKeys.Ctrl, "Ctrl"),
        (ModifierKeys.Alt, "Alt"),
        (ModifierKeys.Shift, "Shift"),
        (ModifierKeys.Win, "Win"),
    ];

    private static readonly Dictionary<string, ModifierKeys> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = ModifierKeys.Ctrl,
        ["Control"] = ModifierKeys.Ctrl,
        ["Alt"] = ModifierKeys.Alt,
        ["Shift"] = ModifierKeys.Shift,
        ["Win"] = ModifierKeys.Win,
        ["Windows"] = ModifierKeys.Win,
        ["Meta"] = ModifierKeys.Win,
    };

    public Hotkey(ModifierKeys modifiers, int key)
    {
        if (key is <= 0 or > 0xFE)
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Código de tecla virtual inválido.");
        }

        if (VirtualKeys.IsModifier(key))
        {
            throw new ArgumentException("A tecla principal não pode ser um modificador.", nameof(key));
        }

        Modifiers = modifiers;
        Key = key;
    }

    public ModifierKeys Modifiers { get; }

    public int Key { get; }

    public static Hotkey Parse(string text) =>
        TryParse(text, out var hotkey, out var error) ? hotkey : throw new FormatException(error);

    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out Hotkey? hotkey,
        [NotNullWhen(false)] out string? error)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Atalho vazio.";
            return false;
        }

        var modifiers = ModifierKeys.None;
        int? key = null;
        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                error = $"Atalho mal formado: \"{text}\".";
                return false;
            }

            if (ModifierAliases.TryGetValue(part, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    error = $"Modificador repetido: \"{part}\".";
                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            if (!KeyNames.TryGetKey(part, out var virtualKey))
            {
                error = $"Tecla desconhecida: \"{part}\".";
                return false;
            }

            if (key is not null)
            {
                error = "O atalho deve ter só uma tecla principal além de Ctrl/Alt/Shift/Win.";
                return false;
            }

            key = virtualKey;
        }

        if (key is null)
        {
            error = "O atalho precisa de uma tecla principal, ex.: Ctrl+Alt+Shift+L.";
            return false;
        }

        hotkey = new Hotkey(modifiers, key.Value);
        error = null;
        return true;
    }

    /// <summary>Formato do arquivo de configuração: "Ctrl+Alt+Shift+L".</summary>
    public override string ToString() => string.Join('+', Parts());

    /// <summary>Formato para mostrar na tela: "Ctrl + Alt + Shift + L".</summary>
    public string ToDisplayString() => string.Join(" + ", Parts());

    private IEnumerable<string> Parts() =>
        ModifierOrder
            .Where(m => Modifiers.HasFlag(m.Flag))
            .Select(m => m.Name)
            .Append(KeyNames.GetName(Key));
}
