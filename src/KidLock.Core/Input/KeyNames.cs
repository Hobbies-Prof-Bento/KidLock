using System.Globalization;

namespace KidLock.Core.Input;

/// <summary>Nomes legíveis de teclas usados no arquivo de configuração (ex.: "Ctrl+Alt+Shift+L").</summary>
public static class KeyNames
{
    private static readonly Dictionary<string, int> KeysByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> NamesByKey = [];

    static KeyNames()
    {
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            Register(letter.ToString(), letter);
        }

        for (var digit = 0; digit <= 9; digit++)
        {
            Register(digit.ToString(CultureInfo.InvariantCulture), VirtualKeys.D0 + digit);
            Register($"NumPad{digit}", VirtualKeys.NumPad0 + digit);
        }

        for (var function = 1; function <= 24; function++)
        {
            Register($"F{function}", VirtualKeys.F(function));
        }

        Register("Space", VirtualKeys.Space, "Espaco", "Espaço");
        Register("Enter", VirtualKeys.Enter, "Return");
        Register("Esc", VirtualKeys.Escape, "Escape");
        Register("Tab", VirtualKeys.Tab);
        Register("Backspace", VirtualKeys.Backspace);
        Register("Insert", VirtualKeys.Insert, "Ins");
        Register("Delete", VirtualKeys.Delete, "Del");
        Register("Home", VirtualKeys.Home);
        Register("End", VirtualKeys.End);
        Register("PageUp", VirtualKeys.PageUp, "PgUp");
        Register("PageDown", VirtualKeys.PageDown, "PgDn");
        Register("Left", VirtualKeys.Left);
        Register("Up", VirtualKeys.Up);
        Register("Right", VirtualKeys.Right);
        Register("Down", VirtualKeys.Down);
        Register("Pause", VirtualKeys.Pause);
        Register("CapsLock", VirtualKeys.CapsLock);
        Register("NumLock", VirtualKeys.NumLock);
        Register("ScrollLock", VirtualKeys.ScrollLock);
        Register("PrintScreen", VirtualKeys.PrintScreen, "PrtSc");
        Register("Multiply", VirtualKeys.Multiply);
        Register("Add", VirtualKeys.Add);
        Register("Subtract", VirtualKeys.Subtract);
        Register("Decimal", VirtualKeys.Decimal);
        Register("Divide", VirtualKeys.Divide);
    }

    /// <summary>Nome canônico de cada tecla conhecida (um por tecla).</summary>
    public static IReadOnlyCollection<string> CanonicalNames => NamesByKey.Values;

    public static bool TryGetKey(string name, out int virtualKey)
    {
        name = name.Trim();
        if (KeysByName.TryGetValue(name, out virtualKey))
        {
            return true;
        }

        // Teclas sem nome (símbolos que mudam conforme o layout, ex.: Ç no ABNT2) ficam como código: 0xBA.
        if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out virtualKey)
            && virtualKey is > 0 and <= 0xFE
            && !VirtualKeys.IsModifier(virtualKey))
        {
            return true;
        }

        virtualKey = 0;
        return false;
    }

    public static string GetName(int virtualKey) =>
        NamesByKey.TryGetValue(virtualKey, out var name) ? name : $"0x{virtualKey:X2}";

    private static void Register(string name, int virtualKey, params string[] aliases)
    {
        NamesByKey.Add(virtualKey, name);
        KeysByName.Add(name, virtualKey);
        foreach (var alias in aliases)
        {
            KeysByName.Add(alias, virtualKey);
        }
    }
}
