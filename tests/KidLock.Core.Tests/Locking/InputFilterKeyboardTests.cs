using KidLock.Core.Hotkeys;
using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Tests.TestSupport;
using static KidLock.Core.Input.InputDecision;
using static KidLock.Core.Input.VirtualKeys;

namespace KidLock.Core.Tests.Locking;

public class InputFilterKeyboardTests
{
    private static readonly int[] DefaultChord = [LControl, LMenu, LShift, 'L'];

    [Fact]
    public void Unlocked_NormalTyping_PassesThrough()
    {
        var h = new FilterHarness();

        Assert.All(h.Chord(LShift, 'A'), d => Assert.Equal(Pass, d));
        Assert.All(h.Chord('B'), d => Assert.Equal(Pass, d));
        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void LockHotkey_Locks_SwallowsOnlyTheMainKey()
    {
        var h = new FilterHarness();

        var decisions = h.Chord(DefaultChord);

        Assert.True(h.Controller.IsLocked);
        // Ctrl, Alt, Shift descem antes do bloqueio: o Windows viu, então as solturas precisam passar.
        Assert.Equal(new[] { Pass, Pass, Pass, Block, Block, Pass, Pass, Pass }, decisions);
    }

    [Fact]
    public void Locked_EverythingIsSwallowed()
    {
        var h = new FilterHarness();
        h.LockNow();

        Assert.All(h.Chord('A'), d => Assert.Equal(Block, d));
        Assert.All(h.Chord(LWin), d => Assert.Equal(Block, d));
        Assert.All(h.Chord(LMenu, F(4)), d => Assert.Equal(Block, d));
        Assert.All(h.Chord(Escape), d => Assert.Equal(Block, d));
        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void UnlockHotkey_Unlocks_AndSwallowsWholeChord()
    {
        var h = new FilterHarness();
        h.LockNow();

        var decisions = h.Chord(DefaultChord);

        Assert.False(h.Controller.IsLocked);
        // Todas as descidas foram engolidas durante o bloqueio, então as solturas também são.
        Assert.All(decisions, d => Assert.Equal(Block, d));
        Assert.Equal(Pass, h.Down('A').Decision);
    }

    [Fact]
    public void RightSideModifiers_AlsoMatch()
    {
        var h = new FilterHarness();
        h.LockNow();

        h.Chord(RControl, RMenu, RShift, 'L');

        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void ExtraKeyHeld_DoesNotUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();

        h.Down('A');
        h.Chord(DefaultChord);

        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void ExtraModifierHeld_DoesNotUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();

        h.Chord(LControl, LMenu, LShift, LWin, 'L');

        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void MissingModifier_DoesNotUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();

        h.Chord(LControl, LShift, 'L');

        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void ModifiersOnly_NeverTrigger()
    {
        var h = new FilterHarness();

        h.Chord(LControl, LMenu, LShift);

        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void HoldingTheMainKey_DoesNotToggleBackAndForth()
    {
        var h = new FilterHarness();
        foreach (var key in DefaultChord)
        {
            h.Down(key);
        }

        Assert.True(h.Controller.IsLocked);
        for (var repeat = 0; repeat < 20; repeat++)
        {
            Assert.Equal(Block, h.Down('L').Decision);
        }

        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void KeyPressedBeforeLock_RepeatsAreBlocked_ButReleasePasses()
    {
        var h = new FilterHarness();
        Assert.Equal(Pass, h.Down('A').Decision);

        h.LockNow();

        Assert.Equal(Block, h.Down('A').Decision); // auto-repetição durante o bloqueio
        Assert.Equal(Pass, h.Up('A').Decision);    // o Windows viu a descida: a soltura precisa chegar
    }

    [Fact]
    public void KeyPressedDuringLock_StaysSwallowedUntilReleased_EvenAfterUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();
        Assert.Equal(Block, h.Down('A').Decision);

        h.UnlockNow();

        Assert.Equal(Block, h.Down('A').Decision); // auto-repetição de uma tecla engolida
        Assert.Equal(Block, h.Up('A').Decision);
        Assert.Equal(Pass, h.Down('A').Decision);  // novo pressionamento: normal
    }

    [Fact]
    public void SeparateHotkeys_EachOnlyWorksInItsState()
    {
        var lockHotkey = Hotkey.Parse("Ctrl+Alt+K");
        var unlockHotkey = Hotkey.Parse("Ctrl+Alt+Shift+U");
        var h = new FilterHarness(new FilterOptions { LockHotkey = lockHotkey, UnlockHotkey = unlockHotkey });

        Assert.All(h.Chord(LControl, LMenu, LShift, 'U'), d => Assert.Equal(Pass, d));
        Assert.False(h.Controller.IsLocked);

        h.Chord(LControl, LMenu, 'K');
        Assert.True(h.Controller.IsLocked);

        h.Chord(LControl, LMenu, 'K');
        Assert.True(h.Controller.IsLocked);

        h.Chord(LControl, LMenu, LShift, 'U');
        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void BlockKeyboardDisabled_KeysPassWhileLocked_HotkeyStillUnlocks()
    {
        var h = new FilterHarness(new FilterOptions { BlockKeyboard = false });
        h.LockNow();

        Assert.All(h.Chord('A'), d => Assert.Equal(Pass, d));
        h.Chord(DefaultChord);

        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void InjectedInput_IsBlockedByDefault()
    {
        var h = new FilterHarness();
        h.LockNow();

        Assert.Equal(Block, h.Down('A', injected: true).Decision);
    }

    [Fact]
    public void InjectedInput_PassesWhenAllowed_HardwareStillBlocked()
    {
        var h = new FilterHarness(new FilterOptions { AllowInjectedInput = true });
        h.LockNow();

        Assert.Equal(Pass, h.Down('A', injected: true).Decision);
        Assert.Equal(Pass, h.Up('A', injected: true).Decision);
        Assert.Equal(Block, h.Down('B').Decision);
    }

    [Fact]
    public void StaleKeysFromMissedReleases_StopCountingAfterTimeout()
    {
        // Ctrl+Alt+Del troca para a área de trabalho segura; o gancho nunca vê as solturas.
        var h = new FilterHarness();
        h.LockNow();
        h.Down(LControl);
        h.Down(LMenu);
        h.Down(Delete);

        h.Time.Advance(TimeSpan.FromSeconds(6));
        h.Chord(DefaultChord);

        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void RecentlyHeldExtraKey_StillPreventsUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();
        h.Down(Delete);

        h.Time.Advance(TimeSpan.FromSeconds(1));
        h.Chord(DefaultChord);

        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void ResetInputState_ForgetsGhostKeysImmediately()
    {
        var h = new FilterHarness();
        h.LockNow();
        h.Down(Delete);

        h.Filter.ResetInputState();
        h.Chord(DefaultChord);

        Assert.False(h.Controller.IsLocked);
    }

    [Fact]
    public void StaleKeyThatWindowsStillHolds_IsNotLeftStuck()
    {
        var h = new FilterHarness();
        Assert.Equal(Pass, h.Down('A').Decision);
        h.LockNow();

        h.Time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(Block, h.Down('A').Decision);

        Assert.Equal(Pass, h.Up('A').Decision);
    }

    [Fact]
    public void MenuMask_RequestedWhenLockingWithAltAlreadyDeliveredToWindows()
    {
        var h = new FilterHarness();
        h.Down(LControl);
        h.Down(LMenu);
        h.Down(LShift);

        var result = h.Down('L');

        Assert.Equal(new KeyboardResult(Block, RequestMenuMask: true), result);
    }

    [Fact]
    public void MenuMask_NotRequestedWhenUnlocking_BecauseAltWasSwallowed()
    {
        var h = new FilterHarness();
        h.LockNow();
        h.Down(LControl);
        h.Down(LMenu);
        h.Down(LShift);

        var result = h.Down('L');

        Assert.False(h.Controller.IsLocked);
        Assert.False(result.RequestMenuMask);
    }

    [Fact]
    public void MenuMask_NotRequestedWithoutAltOrWin()
    {
        var h = new FilterHarness(new FilterOptions { LockHotkey = Hotkey.Parse("Ctrl+Shift+F12") });
        h.Down(LControl);
        h.Down(LShift);

        var result = h.Down(F(12));

        Assert.True(h.Controller.IsLocked);
        Assert.False(result.RequestMenuMask);
    }

    [Fact]
    public void MenuMask_RequestedForWinBasedHotkey()
    {
        var h = new FilterHarness(new FilterOptions { LockHotkey = Hotkey.Parse("Win+Shift+K") });
        h.Down(LWin);
        h.Down(LShift);

        Assert.True(h.Down('K').RequestMenuMask);
    }

    [Fact]
    public void UpdateOptions_ChangesHotkeyAtRuntime()
    {
        var h = new FilterHarness();
        var newHotkey = Hotkey.Parse("Ctrl+Alt+F9");

        h.Filter.UpdateOptions(h.Filter.Options with { LockHotkey = newHotkey, UnlockHotkey = newHotkey });
        h.Chord(DefaultChord);
        Assert.False(h.Controller.IsLocked);

        h.Chord(LControl, LMenu, F(9));
        Assert.True(h.Controller.IsLocked);
    }

    [Fact]
    public void HotkeyRaisesStateChangedWithHotkeyReason()
    {
        var h = new FilterHarness();
        var events = new List<LockStateChangedEventArgs>();
        h.Controller.StateChanged += (_, e) => events.Add(e);

        h.Chord(DefaultChord);
        h.Chord(DefaultChord);

        Assert.Collection(
            events,
            e => Assert.Equal((true, LockReason.Hotkey), (e.IsLocked, e.Reason)),
            e => Assert.Equal((false, LockReason.Hotkey), (e.IsLocked, e.Reason)));
    }
}
