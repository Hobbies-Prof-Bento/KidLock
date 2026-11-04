using KidLock.Core.Input;
using KidLock.Core.Locking;

namespace KidLock.Core.Tests.TestSupport;

/// <summary>Monta controller + filtro com relógio falso e oferece atalhos para simular digitação.</summary>
internal sealed class FilterHarness
{
    /// <summary>Intervalo entre eventos consecutivos (digitação rápida, mas realista).</summary>
    public static readonly TimeSpan EventInterval = TimeSpan.FromMilliseconds(15);

    public FilterHarness(FilterOptions? options = null)
    {
        Filter = new InputFilter(Controller, options ?? new FilterOptions(), Time);
    }

    public FakeTimeProvider Time { get; } = new();

    public LockController Controller { get; } = new();

    public InputFilter Filter { get; }

    public KeyboardResult Down(int virtualKey, bool injected = false)
    {
        Time.Advance(EventInterval);
        return Filter.ProcessKeyboard(new KeyboardInput(virtualKey, IsKeyDown: true, injected));
    }

    public KeyboardResult Up(int virtualKey, bool injected = false)
    {
        Time.Advance(EventInterval);
        return Filter.ProcessKeyboard(new KeyboardInput(virtualKey, IsKeyDown: false, injected));
    }

    /// <summary>Pressiona as teclas em ordem e solta em ordem inversa. Devolve as decisões na ordem dos eventos.</summary>
    public List<InputDecision> Chord(params int[] keys)
    {
        var decisions = new List<InputDecision>();
        foreach (var key in keys)
        {
            decisions.Add(Down(key).Decision);
        }

        foreach (var key in keys.Reverse())
        {
            decisions.Add(Up(key).Decision);
        }

        return decisions;
    }

    public InputDecision Mouse(MouseAction action, MouseButton button = MouseButton.None, bool injected = false) =>
        Filter.ProcessMouse(new MouseInput(action, button, injected));

    public void LockNow() => Controller.Lock(LockReason.UserCommand);

    public void UnlockNow() => Controller.Unlock(LockReason.UserCommand);
}
