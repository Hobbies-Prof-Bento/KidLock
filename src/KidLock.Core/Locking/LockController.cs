namespace KidLock.Core.Locking;

public enum LockReason
{
    Hotkey,
    UserCommand,
    SessionLocked,
}

public sealed class LockStateChangedEventArgs(bool isLocked, LockReason reason) : EventArgs
{
    public bool IsLocked { get; } = isLocked;

    public LockReason Reason { get; } = reason;
}

/// <summary>
/// Estado bloqueado/desbloqueado, seguro para uso simultâneo pela thread do gancho e pela UI.
/// </summary>
/// <remarks>
/// <see cref="StateChanged"/> é disparado na thread que fez a mudança e fora do lock interno.
/// Assinantes devem ser rápidos (normalmente só um Post para a UI) e, ao atualizar a tela,
/// ler <see cref="IsLocked"/> em vez de confiar na ordem de chegada dos eventos.
/// </remarks>
public sealed class LockController
{
    private readonly object _gate = new();
    private bool _isLocked;

    public event EventHandler<LockStateChangedEventArgs>? StateChanged;

    public bool IsLocked
    {
        get
        {
            lock (_gate)
            {
                return _isLocked;
            }
        }
    }

    /// <returns>true se o estado mudou.</returns>
    public bool Lock(LockReason reason) => TrySet(locked: true, reason);

    /// <returns>true se o estado mudou.</returns>
    public bool Unlock(LockReason reason) => TrySet(locked: false, reason);

    public void Toggle(LockReason reason)
    {
        bool locked;
        lock (_gate)
        {
            locked = !_isLocked;
            _isLocked = locked;
        }

        StateChanged?.Invoke(this, new LockStateChangedEventArgs(locked, reason));
    }

    private bool TrySet(bool locked, LockReason reason)
    {
        lock (_gate)
        {
            if (_isLocked == locked)
            {
                return false;
            }

            _isLocked = locked;
        }

        StateChanged?.Invoke(this, new LockStateChangedEventArgs(locked, reason));
        return true;
    }
}
