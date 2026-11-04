using System.Numerics;
using KidLock.Core.Input;

namespace KidLock.Core.Hotkeys;

/// <summary>Regras para que um atalho seja seguro contra "teclado amassado" e não colida com o Windows.</summary>
public static class HotkeyPolicy
{
    public const int MinimumModifiers = 2;

    public static IReadOnlyList<string> Validate(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        var problems = new List<string>();

        if (BitOperations.PopCount((uint)hotkey.Modifiers) < MinimumModifiers)
        {
            problems.Add($"Use pelo menos {MinimumModifiers} modificadores (Ctrl, Alt, Shift, Win) para uma criança não acionar por acaso.");
        }

        if (IsReservedBySystem(hotkey))
        {
            problems.Add($"\"{hotkey}\" é reservado pelo Windows.");
        }

        return problems;
    }

    private static bool IsReservedBySystem(Hotkey hotkey)
    {
        var modifiers = hotkey.Modifiers;
        return (hotkey.Key == VirtualKeys.Delete && modifiers.HasFlag(ModifierKeys.Ctrl | ModifierKeys.Alt))
            || (hotkey.Key == 'L' && modifiers.HasFlag(ModifierKeys.Win))
            || (hotkey.Key == VirtualKeys.Escape && modifiers == (ModifierKeys.Ctrl | ModifierKeys.Shift));
    }
}
