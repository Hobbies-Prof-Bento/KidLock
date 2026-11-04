using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Tests.TestSupport;
using static KidLock.Core.Input.InputDecision;
using static KidLock.Core.Input.VirtualKeys;

namespace KidLock.Core.Tests.Locking;

/// <summary>
/// Testes baseados em propriedades: milhares de eventos aleatórios (sementes fixas, reprodutíveis)
/// verificando garantias que precisam valer para qualquer sequência.
/// </summary>
public class InputFilterPropertyTests
{
    private const int Steps = 20_000;

    public static TheoryData<int> Seeds => new() { 1, 7, 42, 1337, 2026, 31337 };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ChildMashingWhileLocked_NeverUnlocks_AndNothingReachesWindows(int seed)
    {
        var random = new Random(seed);
        var keys = MashableKeys(includeTriggerKey: false);
        var h = new FilterHarness();
        h.LockNow();
        var heldKeys = new List<int>();
        var heldButtons = new List<MouseButton>();

        for (var step = 0; step < Steps; step++)
        {
            if (random.Next(3) == 0)
            {
                Assert.Equal(Block, RandomMouseEvent(h, random, heldButtons));
            }
            else
            {
                Assert.Equal(Block, RandomKeyEvent(h, random, keys, heldKeys).Decision);
            }

            Assert.True(h.Controller.IsLocked, $"desbloqueou no passo {step} (semente {seed})");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomUseWithLockToggles_NeverLeavesKeysStuckInWindows(int seed)
    {
        var random = new Random(seed);
        var keys = MashableKeys(includeTriggerKey: true);
        var h = new FilterHarness();
        var held = new List<int>();
        var windowsDown = new HashSet<int>(); // o que o Windows acredita estar pressionado

        void Send(int key, bool down)
        {
            var decision = (down ? h.Down(key) : h.Up(key)).Decision;
            if (decision != Pass)
            {
                return;
            }

            if (down)
            {
                windowsDown.Add(key);
            }
            else
            {
                Assert.True(windowsDown.Remove(key), $"soltura órfã de 0x{key:X2} (semente {seed})");
            }
        }

        for (var step = 0; step < Steps; step++)
        {
            switch (random.Next(25))
            {
                case 0:
                    h.Controller.Toggle(LockReason.UserCommand);
                    continue;

                case 1:
                    // Um adulto solta tudo e faz o atalho completo: o estado precisa inverter.
                    foreach (var key in held)
                    {
                        Send(key, down: false);
                    }

                    held.Clear();
                    var before = h.Controller.IsLocked;
                    foreach (var key in new[] { LControl, LMenu, LShift, 'L' })
                    {
                        Send(key, down: true);
                    }

                    foreach (var key in new[] { 'L', LShift, LMenu, LControl })
                    {
                        Send(key, down: false);
                    }

                    Assert.NotEqual(before, h.Controller.IsLocked);
                    continue;
            }

            var next = keys[random.Next(keys.Length)];
            if (held.Contains(next) && random.Next(3) == 0)
            {
                held.Remove(next);
                Send(next, down: false);
            }
            else
            {
                if (!held.Contains(next))
                {
                    held.Add(next);
                }

                Send(next, down: true);
            }
        }

        foreach (var key in held)
        {
            Send(key, down: false);
        }

        Assert.Empty(windowsDown);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomClicksWithLockToggles_NeverLeaveButtonsStuckInWindows(int seed)
    {
        var random = new Random(seed);
        var h = new FilterHarness();
        var buttons = new[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1, MouseButton.X2 };
        var held = new HashSet<MouseButton>();
        var windowsDown = new HashSet<MouseButton>();

        for (var step = 0; step < Steps; step++)
        {
            if (random.Next(10) == 0)
            {
                h.Controller.Toggle(LockReason.UserCommand);
                continue;
            }

            var button = buttons[random.Next(buttons.Length)];
            var down = !held.Contains(button);
            var decision = h.Mouse(down ? MouseAction.ButtonDown : MouseAction.ButtonUp, button);
            if (down)
            {
                held.Add(button);
            }
            else
            {
                held.Remove(button);
            }

            if (decision == Pass && down)
            {
                windowsDown.Add(button);
            }
            else if (decision == Pass)
            {
                Assert.True(windowsDown.Remove(button), $"soltura órfã de {button} (semente {seed})");
            }
        }

        foreach (var button in held)
        {
            if (h.Mouse(MouseAction.ButtonUp, button) == Pass)
            {
                windowsDown.Remove(button);
            }
        }

        Assert.Empty(windowsDown);
    }

    private static int[] MashableKeys(bool includeTriggerKey)
    {
        var keys = new List<int>();
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            if (includeTriggerKey || letter != 'L')
            {
                keys.Add(letter);
            }
        }

        for (var digit = 0; digit <= 9; digit++)
        {
            keys.Add(D0 + digit);
        }

        for (var function = 1; function <= 12; function++)
        {
            keys.Add(F(function));
        }

        keys.AddRange(
        [
            LControl, RControl, LMenu, RMenu, LShift, RShift, LWin, RWin,
            Space, Enter, Escape, Tab, Delete, Backspace, Up, Down, Left, Right,
        ]);
        return [.. keys];
    }

    private static KeyboardResult RandomKeyEvent(FilterHarness h, Random random, int[] keys, List<int> held)
    {
        // Mãos pequenas: no máximo 6 teclas ao mesmo tempo, soltando a mais antiga.
        if (held.Count >= 6)
        {
            var oldest = held[0];
            held.RemoveAt(0);
            return h.Up(oldest);
        }

        var key = keys[random.Next(keys.Length)];
        if (held.Contains(key) && random.Next(3) == 0)
        {
            held.Remove(key);
            return h.Up(key);
        }

        if (!held.Contains(key))
        {
            held.Add(key);
        }

        return h.Down(key);
    }

    private static InputDecision RandomMouseEvent(FilterHarness h, Random random, List<MouseButton> held)
    {
        switch (random.Next(4))
        {
            case 0:
                return h.Mouse(MouseAction.Wheel);
            case 1:
                var button = (MouseButton)random.Next(1, 6);
                if (held.Remove(button))
                {
                    return h.Mouse(MouseAction.ButtonUp, button);
                }

                held.Add(button);
                return h.Mouse(MouseAction.ButtonDown, button);
            default:
                return h.Mouse(MouseAction.Move);
        }
    }
}
