using KidLock.Core.Hotkeys;
using KidLock.Core.Input;

namespace KidLock.Core.Locking;

/// <summary>
/// Decide, evento a evento, se a entrada chega ao Windows ou é engolida, e reconhece os atalhos
/// de bloqueio/desbloqueio.
/// </summary>
/// <remarks>
/// <para>
/// O atalho exige correspondência exata: os modificadores precisam ser exatamente os configurados e
/// nenhuma outra tecla comum pode estar pressionada. Uma criança apertando várias teclas ao mesmo
/// tempo não destrava por acaso.
/// </para>
/// <para>
/// Invariante central: para cada tecla rastreada, "não está em _swallowedKeys" equivale a "o Windows
/// viu a tecla descer e ainda não viu subir". Com isso nunca entregamos uma soltura órfã e nunca
/// deixamos uma tecla presa no Windows (ex.: Ctrl que desceu antes do bloqueio sobe normalmente).
/// </para>
/// <para>Todos os métodos são seguros para chamadas concorrentes.</para>
/// </remarks>
public sealed class InputFilter
{
    // Teclas que, soltas "sozinhas", abrem a barra de menus (Alt) ou o menu Iniciar (Win).
    private static readonly int[] MenuActivatingKeys =
        [VirtualKeys.LMenu, VirtualKeys.RMenu, VirtualKeys.Menu, VirtualKeys.LWin, VirtualKeys.RWin];

    private readonly object _gate = new();
    private readonly LockController _controller;
    private readonly TimeProvider _time;
    private readonly KeyboardState _keys = new();
    private readonly HashSet<int> _swallowedKeys = [];
    private readonly HashSet<MouseButton> _swallowedButtons = [];
    private FilterOptions _options;
    private Action<Hotkey?>? _captureCallback;
    private DateTimeOffset _captureStartedAt;

    public InputFilter(LockController controller, FilterOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(options);
        _controller = controller;
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    private enum HotkeyAction
    {
        None,
        Lock,
        Unlock,
    }

    /// <summary>Se a interface sumir no meio de uma captura, o teclado volta ao normal depois deste tempo.</summary>
    public static TimeSpan HotkeyCaptureTimeout { get; } = TimeSpan.FromSeconds(30);

    public bool IsCapturingHotkey
    {
        get
        {
            lock (_gate)
            {
                return _captureCallback is not null;
            }
        }
    }

    public FilterOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options;
            }
        }
    }

    public void UpdateOptions(FilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_gate)
        {
            _options = options;
        }
    }

    /// <summary>
    /// A próxima combinação (modificadores + uma tecla) é capturada em vez de chegar aos programas,
    /// inclusive combinações com Win que o Windows trataria antes de qualquer janela. Enquanto captura,
    /// os atalhos de bloqueio/desbloqueio não agem.
    /// </summary>
    /// <param name="onCompleted">
    /// Recebe a combinação, ou null se a pessoa apertar Esc, se a captura for cancelada ou se o tempo
    /// esgotar. É chamado fora do lock, na thread do gancho (ou na de quem cancelou).
    /// </param>
    public void BeginHotkeyCapture(Action<Hotkey?> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(onCompleted);
        Action<Hotkey?>? replaced;
        lock (_gate)
        {
            replaced = _captureCallback;
            _captureCallback = onCompleted;
            _captureStartedAt = _time.GetUtcNow();
        }

        replaced?.Invoke(null);
    }

    public void CancelHotkeyCapture()
    {
        Action<Hotkey?>? cancelled;
        lock (_gate)
        {
            cancelled = _captureCallback;
            _captureCallback = null;
        }

        cancelled?.Invoke(null);
    }

    public KeyboardResult ProcessKeyboard(KeyboardInput input)
    {
        KeyboardResult result;
        var action = HotkeyAction.None;
        Action? captureCompletion = null;
        lock (_gate)
        {
            if (input.IsKeyDown)
            {
                (result, action, captureCompletion) = OnKeyDown(input);
            }
            else
            {
                result = OnKeyUp(input.VirtualKey);
            }
        }

        // Fora do lock: assinantes e callbacks nunca atrasam o processamento do próximo evento.
        captureCompletion?.Invoke();
        switch (action)
        {
            case HotkeyAction.Lock:
                _controller.Lock(LockReason.Hotkey);
                break;
            case HotkeyAction.Unlock:
                _controller.Unlock(LockReason.Hotkey);
                break;
        }

        return result;
    }

    public InputDecision ProcessMouse(MouseInput input)
    {
        lock (_gate)
        {
            var block = _controller.IsLocked && ShouldBlock(_options.BlockMouse, input.IsInjected);
            switch (input.Action)
            {
                case MouseAction.ButtonDown:
                    if (!block)
                    {
                        _swallowedButtons.Remove(input.Button);
                        return InputDecision.Pass;
                    }

                    _swallowedButtons.Add(input.Button);
                    return InputDecision.Block;

                case MouseAction.ButtonUp:
                    // Mesmo princípio do teclado: a soltura acompanha o destino do clique.
                    return _swallowedButtons.Remove(input.Button) ? InputDecision.Block : InputDecision.Pass;

                default:
                    return block ? InputDecision.Block : InputDecision.Pass;
            }
        }
    }

    /// <summary>
    /// Esquece todas as teclas/botões rastreados. Usado quando o Windows troca de área de trabalho
    /// (bloqueio de sessão, Ctrl+Alt+Del, suspensão), situações em que o gancho perde eventos.
    /// </summary>
    public void ResetInputState()
    {
        lock (_gate)
        {
            _keys.Clear();
            _swallowedKeys.Clear();
            _swallowedButtons.Clear();
        }
    }

    private KeyboardResult OnKeyUp(int virtualKey)
    {
        _keys.Release(virtualKey);
        return _swallowedKeys.Remove(virtualKey) ? KeyboardResult.Block : KeyboardResult.Pass;
    }

    private (KeyboardResult Result, HotkeyAction Action, Action? CaptureCompletion) OnKeyDown(KeyboardInput input)
    {
        var virtualKey = input.VirtualKey;
        var now = _time.GetUtcNow();
        var locked = _controller.IsLocked;
        var windowsHoldsKey = _keys.IsTracked(virtualKey) && !_swallowedKeys.Contains(virtualKey);
        var isRepeat = _keys.IsActive(virtualKey, now, _options.StaleKeyTimeout);
        _keys.Press(virtualKey, now);

        var captureCompletion = ExpireStaleCapture(now);
        var capturing = _captureCallback is not null;
        if (capturing && !isRepeat && !VirtualKeys.IsModifier(virtualKey))
        {
            captureCompletion = CompleteCapture(virtualKey, now);
        }

        var action = isRepeat || capturing ? HotkeyAction.None : MatchHotkey(virtualKey, locked, now);
        var block = capturing
            || action != HotkeyAction.None
            || (locked && ShouldBlock(_options.BlockKeyboard, input.IsInjected))
            || (isRepeat && _swallowedKeys.Contains(virtualKey)); // auto-repetição segue o pressionamento original

        if (!block)
        {
            _swallowedKeys.Remove(virtualKey);
            return (KeyboardResult.Pass, HotkeyAction.None, captureCompletion);
        }

        // Se o Windows já viu esta tecla descer (pressionada antes do bloqueio), a soltura precisa passar.
        if (!windowsHoldsKey)
        {
            _swallowedKeys.Add(virtualKey);
        }

        var requestMenuMask = (action != HotkeyAction.None || capturing) && WindowsHoldsMenuActivatingKey();
        return (new KeyboardResult(InputDecision.Block, requestMenuMask), action, captureCompletion);
    }

    private Action? ExpireStaleCapture(DateTimeOffset now)
    {
        if (_captureCallback is not { } callback || now - _captureStartedAt <= HotkeyCaptureTimeout)
        {
            return null;
        }

        _captureCallback = null;
        return () => callback(null);
    }

    private Action CompleteCapture(int virtualKey, DateTimeOffset now)
    {
        var callback = _captureCallback!;
        _captureCallback = null;
        var modifiers = _keys.ActiveModifiers(now, _options.StaleKeyTimeout);
        var cancelled = (virtualKey == VirtualKeys.Escape && modifiers == ModifierKeys.None)
            || virtualKey is <= 0 or > 0xFE;
        var captured = cancelled ? null : new Hotkey(modifiers, virtualKey);
        return () => callback(captured);
    }

    private HotkeyAction MatchHotkey(int virtualKey, bool locked, DateTimeOffset now)
    {
        var target = locked ? _options.UnlockHotkey : _options.LockHotkey;
        if (virtualKey != target.Key)
        {
            return HotkeyAction.None;
        }

        var timeout = _options.StaleKeyTimeout;
        if (_keys.ActiveModifiers(now, timeout) != target.Modifiers || _keys.HasOtherActiveKey(virtualKey, now, timeout))
        {
            return HotkeyAction.None;
        }

        return locked ? HotkeyAction.Unlock : HotkeyAction.Lock;
    }

    private bool WindowsHoldsMenuActivatingKey()
    {
        foreach (var key in MenuActivatingKeys)
        {
            if (_keys.IsTracked(key) && !_swallowedKeys.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldBlock(bool categoryEnabled, bool isInjected) =>
        categoryEnabled && !(isInjected && _options.AllowInjectedInput);
}
