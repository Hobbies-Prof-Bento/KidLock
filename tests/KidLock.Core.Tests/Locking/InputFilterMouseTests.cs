using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Tests.TestSupport;
using static KidLock.Core.Input.InputDecision;

namespace KidLock.Core.Tests.Locking;

public class InputFilterMouseTests
{
    public static TheoryData<MouseAction, MouseButton> AllMouseEvents => new()
    {
        { MouseAction.Move, MouseButton.None },
        { MouseAction.Wheel, MouseButton.None },
        { MouseAction.HorizontalWheel, MouseButton.None },
        { MouseAction.ButtonDown, MouseButton.Left },
        { MouseAction.ButtonDown, MouseButton.Right },
        { MouseAction.ButtonDown, MouseButton.Middle },
        { MouseAction.ButtonDown, MouseButton.X1 },
        { MouseAction.ButtonDown, MouseButton.X2 },
    };

    [Theory]
    [MemberData(nameof(AllMouseEvents))]
    public void Unlocked_EverythingPasses(MouseAction action, MouseButton button)
    {
        var h = new FilterHarness();

        Assert.Equal(Pass, h.Mouse(action, button));
    }

    [Theory]
    [MemberData(nameof(AllMouseEvents))]
    public void Locked_EverythingIsBlocked(MouseAction action, MouseButton button)
    {
        var h = new FilterHarness();
        h.LockNow();

        Assert.Equal(Block, h.Mouse(action, button));
    }

    [Fact]
    public void ClickStartedBeforeLock_ReleasePasses()
    {
        var h = new FilterHarness();
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonDown, MouseButton.Left));

        h.LockNow();

        Assert.Equal(Block, h.Mouse(MouseAction.Move));
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonUp, MouseButton.Left));
    }

    [Fact]
    public void ClickStartedDuringLock_ReleaseIsSwallowedEvenAfterUnlock()
    {
        var h = new FilterHarness();
        h.LockNow();
        Assert.Equal(Block, h.Mouse(MouseAction.ButtonDown, MouseButton.Right));

        h.UnlockNow();

        Assert.Equal(Block, h.Mouse(MouseAction.ButtonUp, MouseButton.Right));
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonDown, MouseButton.Right));
    }

    [Fact]
    public void BlockMouseDisabled_PassesWhileLocked()
    {
        var h = new FilterHarness(new FilterOptions { BlockMouse = false });
        h.LockNow();

        Assert.Equal(Pass, h.Mouse(MouseAction.Move));
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonDown, MouseButton.Left));
        Assert.Equal(Pass, h.Mouse(MouseAction.ButtonUp, MouseButton.Left));
    }

    [Fact]
    public void InjectedMouse_PassesOnlyWhenAllowed()
    {
        var blocked = new FilterHarness();
        blocked.LockNow();
        var allowed = new FilterHarness(new FilterOptions { AllowInjectedInput = true });
        allowed.LockNow();

        Assert.Equal(Block, blocked.Mouse(MouseAction.Move, injected: true));
        Assert.Equal(Pass, allowed.Mouse(MouseAction.Move, injected: true));
        Assert.Equal(Block, allowed.Mouse(MouseAction.Move));
    }
}
