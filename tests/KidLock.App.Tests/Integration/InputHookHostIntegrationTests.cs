using KidLock.App.Hooks;
using KidLock.Core.Hotkeys;
using KidLock.Core.Input;
using KidLock.Core.Locking;

namespace KidLock.App.Tests.Integration;

/// <summary>
/// Testes ponta a ponta com ganchos reais do Windows e entrada injetada via SendInput.
/// Precisam de uma sessão de desktop interativa; no CI rodam só sob demanda (Category=Integration).
/// Enquanto um teste está "bloqueado" (frações de segundo), o teclado/mouse físico também fica.
/// </summary>
[Collection(GlobalInputCollection.Name)]
[Trait("Category", "Integration")]
public sealed class InputHookHostIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly int ProbeKey = VirtualKeys.F(24);
    private static readonly int HotkeyKey = VirtualKeys.F(23);

    [Fact]
    public void Unlocked_KeyboardAndMouseReachOtherPrograms()
    {
        using var env = new HookTestEnvironment(new FilterOptions());

        TestInput.Tap(ProbeKey);
        TestInput.MouseNudge();

        Assert.True(env.Observer.WaitFor(e => e.IsKeyboard && e.VirtualKey == ProbeKey && e.IsKeyUp, Timeout));
        Assert.True(env.Observer.WaitFor(e => !e.IsKeyboard, Timeout));
    }

    [Fact]
    public void Locked_KeyboardIsSwallowed()
    {
        using var env = new HookTestEnvironment(new FilterOptions { BlockMouse = false });
        env.Controller.Lock(LockReason.UserCommand);

        TestInput.Tap(ProbeKey);
        TestInput.MouseNudge(); // sentinela: o mouse passa e chega depois da tecla

        Assert.True(env.Observer.WaitFor(e => !e.IsKeyboard, Timeout), "sentinela não chegou");
        Assert.DoesNotContain(env.Observer.Events, e => e.IsKeyboard);
    }

    [Fact]
    public void Locked_MouseAndTouchpadAreSwallowed()
    {
        using var env = new HookTestEnvironment(new FilterOptions { BlockKeyboard = false });
        env.Controller.Lock(LockReason.UserCommand);

        TestInput.MouseNudge();
        TestInput.Tap(ProbeKey); // sentinela

        Assert.True(env.Observer.WaitFor(e => e.IsKeyboard && e.IsKeyUp, Timeout), "sentinela não chegou");
        Assert.DoesNotContain(env.Observer.Events, e => !e.IsKeyboard);
    }

    [Fact]
    public void Hotkey_LocksAndUnlocks_WithoutLeakingOrStickingKeys()
    {
        var hotkey = new Hotkey(ModifierKeys.Shift, HotkeyKey);
        using var env = new HookTestEnvironment(new FilterOptions { LockHotkey = hotkey, UnlockHotkey = hotkey, BlockMouse = false });

        TestInput.Chord(VirtualKeys.LShift, HotkeyKey);
        Assert.True(HookTestEnvironment.WaitUntil(() => env.Controller.IsLocked, Timeout), "não bloqueou");

        TestInput.Tap(ProbeKey);
        TestInput.MouseNudge();
        Assert.True(env.Observer.WaitFor(e => !e.IsKeyboard, Timeout));
        Assert.DoesNotContain(env.Observer.Events, e => e.VirtualKey == ProbeKey);

        TestInput.Chord(VirtualKeys.LShift, HotkeyKey);
        Assert.True(HookTestEnvironment.WaitUntil(() => !env.Controller.IsLocked, Timeout), "não desbloqueou");

        TestInput.Tap(ProbeKey);
        Assert.True(env.Observer.WaitFor(e => e.VirtualKey == ProbeKey && e.IsKeyUp, Timeout));

        var events = env.Observer.Events;
        // A tecla principal do atalho nunca chega aos programas.
        Assert.DoesNotContain(events, e => e.VirtualKey == HotkeyKey);
        // Shift: o par do bloqueio passa (descida + soltura); o par do desbloqueio é engolido inteiro.
        Assert.Equal(
            new[] { false, true },
            events.Where(e => e.VirtualKey == VirtualKeys.LShift).Select(e => e.IsKeyUp));
        Assert.Empty(env.Errors);
    }

    [Fact]
    public void HotkeyCapture_ReceivesTheCombination_AndNothingReachesOtherPrograms()
    {
        using var env = new HookTestEnvironment(new FilterOptions { BlockMouse = false });
        Hotkey? captured = null;
        var done = false;
        env.Filter.BeginHotkeyCapture(h =>
        {
            captured = h;
            Volatile.Write(ref done, true);
        });

        TestInput.Chord(VirtualKeys.LShift, HotkeyKey);
        TestInput.MouseNudge(); // sentinela

        Assert.True(HookTestEnvironment.WaitUntil(() => Volatile.Read(ref done), Timeout), "captura não terminou");
        Assert.Equal(new Hotkey(ModifierKeys.Shift, HotkeyKey), captured);
        Assert.True(env.Observer.WaitFor(e => !e.IsKeyboard, Timeout));
        Assert.DoesNotContain(env.Observer.Events, e => e.IsKeyboard);
        Assert.False(env.Controller.IsLocked);
    }

    [Fact]
    public void MenuMaskRequest_InjectsUnassignedKeyThatTheFilterIgnores()
    {
        var calls = 0;
        using var env = new HookTestEnvironment(
            new FilterOptions(),
            input =>
            {
                Interlocked.Increment(ref calls);
                return input.VirtualKey == ProbeKey && input.IsKeyDown
                    ? new KeyboardResult(InputDecision.Pass, RequestMenuMask: true)
                    : KeyboardResult.Pass;
            });

        TestInput.Key(ProbeKey, keyUp: false);

        Assert.True(env.Observer.WaitFor(e => e.VirtualKey == VirtualKeys.MenuMask && e.IsKeyUp, Timeout), "máscara não injetada");
        var mask = env.Observer.Events.Where(e => e.VirtualKey == VirtualKeys.MenuMask).ToList();
        Assert.All(mask, e => Assert.Equal(InputHookHost.SelfInjectionMarker, e.Marker));
        Assert.Equal(1, calls); // o filtro só viu a tecla do teste, não a máscara

        TestInput.Key(ProbeKey, keyUp: true);
    }

    [Fact]
    public void FilterException_FailsOpen()
    {
        using var env = new HookTestEnvironment(
            new FilterOptions(),
            _ => throw new InvalidOperationException("bug simulado"));
        env.Controller.Lock(LockReason.UserCommand);

        TestInput.Tap(ProbeKey);

        Assert.True(env.Observer.WaitFor(e => e.VirtualKey == ProbeKey && e.IsKeyUp, Timeout), "evento deveria passar");
        Assert.Contains(env.Errors, e => e.Message == "bug simulado");
    }

    [Fact]
    public void Rearm_ReinstallsHooks_AndKeepsBlocking()
    {
        using var env = new HookTestEnvironment(new FilterOptions { BlockMouse = false });
        env.Controller.Lock(LockReason.UserCommand);

        env.Host.Rearm();
        Assert.True(HookTestEnvironment.WaitUntil(() => env.Host.RearmCount == 1, Timeout), "rearm não concluiu");

        TestInput.Tap(ProbeKey);
        TestInput.MouseNudge();
        Assert.True(env.Observer.WaitFor(e => !e.IsKeyboard, Timeout));
        Assert.DoesNotContain(env.Observer.Events, e => e.IsKeyboard);
    }

    [Fact]
    public void Dispose_RemovesHooks_InputFlowsAgain()
    {
        var observer = new GlobalInputObserver();
        try
        {
            var controller = new LockController();
            var filter = new InputFilter(controller, new FilterOptions());
            var host = new InputHookHost(filter.ProcessKeyboard, filter.ProcessMouse, _ => { });
            host.Start();
            controller.Lock(LockReason.UserCommand);

            host.Dispose();
            TestInput.Tap(ProbeKey);

            Assert.True(observer.WaitFor(e => e.VirtualKey == ProbeKey && e.IsKeyUp, Timeout));
        }
        finally
        {
            observer.Dispose();
        }
    }
}
