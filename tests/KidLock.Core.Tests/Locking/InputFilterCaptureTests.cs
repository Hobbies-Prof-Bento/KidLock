using KidLock.Core.Hotkeys;
using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Tests.TestSupport;
using static KidLock.Core.Input.InputDecision;
using static KidLock.Core.Input.VirtualKeys;

namespace KidLock.Core.Tests.Locking;

public class InputFilterCaptureTests
{
    [Fact]
    public void Capture_ReturnsTheCombination_AndSwallowsIt()
    {
        var h = new FilterHarness();
        var results = new List<Hotkey?>();
        h.Filter.BeginHotkeyCapture(results.Add);

        var decisions = h.Chord(LControl, LMenu, 'K');

        Assert.Equal(Hotkey.Parse("Ctrl+Alt+K"), Assert.Single(results));
        Assert.All(decisions, d => Assert.Equal(Block, d)); // nada chega aos programas
        Assert.False(h.Filter.IsCapturingHotkey);
        Assert.Equal(Pass, h.Down('K').Decision); // depois da captura, tudo normal
    }

    [Fact]
    public void Capture_WorksWithWinKey()
    {
        var h = new FilterHarness();
        Hotkey? captured = null;
        h.Filter.BeginHotkeyCapture(k => captured = k);

        h.Chord(LWin, LShift, 'P');

        Assert.Equal(Hotkey.Parse("Win+Shift+P"), captured);
    }

    [Fact]
    public void Capture_EscapeAlone_Cancels()
    {
        var h = new FilterHarness();
        var results = new List<Hotkey?>();
        h.Filter.BeginHotkeyCapture(results.Add);

        Assert.All(h.Chord(Escape), d => Assert.Equal(Block, d));

        Assert.Null(Assert.Single(results));
        Assert.False(h.Filter.IsCapturingHotkey);
    }

    [Fact]
    public void Capture_EscapeWithModifiers_IsAValidCombination()
    {
        var h = new FilterHarness();
        Hotkey? captured = null;
        h.Filter.BeginHotkeyCapture(k => captured = k);

        h.Chord(LControl, LMenu, Escape);

        Assert.Equal(Hotkey.Parse("Ctrl+Alt+Esc"), captured);
    }

    [Fact]
    public void Capture_SuspendsTheCurrentHotkey()
    {
        var h = new FilterHarness();
        Hotkey? captured = null;
        h.Filter.BeginHotkeyCapture(k => captured = k);

        h.Chord(LControl, LMenu, LShift, 'L');

        Assert.False(h.Controller.IsLocked); // o atalho atual não bloqueia enquanto se escolhe outro
        Assert.Equal(FilterOptions.DefaultHotkey, captured);
    }

    [Fact]
    public void Capture_ModifierReleasedBeforeMainKey_IsNotCounted()
    {
        var h = new FilterHarness();
        Hotkey? captured = null;
        h.Filter.BeginHotkeyCapture(k => captured = k);

        h.Down(LControl);
        h.Up(LControl);
        h.Down(LMenu);
        h.Down(LShift);
        h.Down('J');

        Assert.Equal(Hotkey.Parse("Alt+Shift+J"), captured);
    }

    [Fact]
    public void Capture_KeyHeldBeforeCaptureStarted_StillReleasesInWindows()
    {
        var h = new FilterHarness();
        Assert.Equal(Pass, h.Down(LControl).Decision);
        h.Filter.BeginHotkeyCapture(_ => { });

        h.Down('Q');
        h.Up('Q');

        Assert.Equal(Pass, h.Up(LControl).Decision); // o Windows viu Ctrl descer: a soltura passa
    }

    [Fact]
    public void Cancel_InvokesCallbackWithNull_AndStopsSwallowing()
    {
        var h = new FilterHarness();
        var results = new List<Hotkey?>();
        h.Filter.BeginHotkeyCapture(results.Add);

        h.Filter.CancelHotkeyCapture();

        Assert.Null(Assert.Single(results));
        Assert.Equal(Pass, h.Down('A').Decision);
    }

    [Fact]
    public void BeginAgain_CancelsThePreviousCapture()
    {
        var h = new FilterHarness();
        var first = new List<Hotkey?>();
        var second = new List<Hotkey?>();

        h.Filter.BeginHotkeyCapture(first.Add);
        h.Filter.BeginHotkeyCapture(second.Add);
        h.Chord(LControl, LMenu, 'M');

        Assert.Null(Assert.Single(first));
        Assert.Equal(Hotkey.Parse("Ctrl+Alt+M"), Assert.Single(second));
    }

    [Fact]
    public void Capture_ExpiresAfterTimeout_SoTheKeyboardNeverStaysCaptured()
    {
        var h = new FilterHarness();
        var results = new List<Hotkey?>();
        h.Filter.BeginHotkeyCapture(results.Add);

        h.Time.Advance(InputFilter.HotkeyCaptureTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(Pass, h.Down('A').Decision);
        Assert.Null(Assert.Single(results));
    }

    [Fact]
    public void Capture_DoesNotAffectMouse()
    {
        var h = new FilterHarness();
        h.Filter.BeginHotkeyCapture(_ => { });

        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonDown, MouseButton.Left));
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonUp, MouseButton.Left));
    }

    [Fact]
    public void Capture_ModifierOnlyPresses_DoNotComplete()
    {
        var h = new FilterHarness();
        h.Filter.BeginHotkeyCapture(_ => { });

        h.Chord(LControl, LShift);

        Assert.True(h.Filter.IsCapturingHotkey);
    }
}
