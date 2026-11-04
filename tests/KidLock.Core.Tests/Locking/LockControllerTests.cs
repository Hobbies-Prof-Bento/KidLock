using KidLock.Core.Locking;

namespace KidLock.Core.Tests.Locking;

public class LockControllerTests
{
    [Fact]
    public void StartsUnlocked()
    {
        Assert.False(new LockController().IsLocked);
    }

    [Fact]
    public void Lock_RaisesEventOnlyOnChange()
    {
        var controller = new LockController();
        var events = new List<LockStateChangedEventArgs>();
        controller.StateChanged += (_, e) => events.Add(e);

        Assert.True(controller.Lock(LockReason.UserCommand));
        Assert.False(controller.Lock(LockReason.Hotkey));

        var single = Assert.Single(events);
        Assert.True(single.IsLocked);
        Assert.Equal(LockReason.UserCommand, single.Reason);
    }

    [Fact]
    public void Unlock_WhenAlreadyUnlocked_IsNoOp()
    {
        var controller = new LockController();
        var raised = false;
        controller.StateChanged += (_, _) => raised = true;

        Assert.False(controller.Unlock(LockReason.SessionLocked));
        Assert.False(raised);
    }

    [Fact]
    public void Toggle_FlipsStateEachTime()
    {
        var controller = new LockController();

        controller.Toggle(LockReason.Hotkey);
        Assert.True(controller.IsLocked);
        controller.Toggle(LockReason.Hotkey);
        Assert.False(controller.IsLocked);
    }

    [Fact]
    public void ConcurrentToggles_AreNotLost()
    {
        var controller = new LockController();
        var raised = 0;
        controller.StateChanged += (_, _) => Interlocked.Increment(ref raised);

        Parallel.For(0, 10_000, _ => controller.Toggle(LockReason.UserCommand));

        Assert.False(controller.IsLocked); // número par de inversões
        Assert.Equal(10_000, raised);
    }
}
