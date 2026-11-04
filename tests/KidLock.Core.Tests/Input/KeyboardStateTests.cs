using KidLock.Core.Input;
using static KidLock.Core.Input.VirtualKeys;

namespace KidLock.Core.Tests.Input;

public class KeyboardStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Stale = TimeSpan.FromSeconds(5);

    [Fact]
    public void PressAndRelease_TrackKeys()
    {
        var state = new KeyboardState();

        state.Press('A', T0);
        Assert.True(state.IsTracked('A'));
        Assert.True(state.IsActive('A', T0, Stale));

        state.Release('A');
        Assert.False(state.IsTracked('A'));
    }

    [Fact]
    public void ActiveModifiers_MergesLeftAndRightVariants()
    {
        var state = new KeyboardState();
        state.Press(RControl, T0);
        state.Press(LMenu, T0);
        state.Press(RShift, T0);
        state.Press(LWin, T0);

        Assert.Equal(
            ModifierKeys.Ctrl | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Win,
            state.ActiveModifiers(T0, Stale));
    }

    [Fact]
    public void StaleKeys_StayTrackedButStopBeingActive()
    {
        var state = new KeyboardState();
        state.Press(LControl, T0);
        state.Press('Q', T0);
        var later = T0 + TimeSpan.FromSeconds(6);

        Assert.True(state.IsTracked('Q'));
        Assert.False(state.IsActive('Q', later, Stale));
        Assert.Equal(ModifierKeys.None, state.ActiveModifiers(later, Stale));
        Assert.False(state.HasOtherActiveKey('L', later, Stale));
    }

    [Fact]
    public void HasOtherActiveKey_IgnoresModifiersAndTheKeyItself()
    {
        var state = new KeyboardState();
        state.Press(LControl, T0);
        state.Press('L', T0);

        Assert.False(state.HasOtherActiveKey('L', T0, Stale));

        state.Press('A', T0);
        Assert.True(state.HasOtherActiveKey('L', T0, Stale));
    }

    [Theory]
    [InlineData(Control, ModifierKeys.Ctrl)]
    [InlineData(Menu, ModifierKeys.Alt)]
    [InlineData(Shift, ModifierKeys.Shift)]
    [InlineData(RWin, ModifierKeys.Win)]
    [InlineData(0x41, ModifierKeys.None)]
    public void ToModifier_MapsGenericAndSidedCodes(int virtualKey, ModifierKeys expected)
    {
        Assert.Equal(expected, VirtualKeys.ToModifier(virtualKey));
    }
}
