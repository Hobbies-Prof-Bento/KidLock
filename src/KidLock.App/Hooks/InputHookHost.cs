using System.ComponentModel;
using System.Runtime.InteropServices;
using KidLock.App.Native;
using KidLock.Core.Input;

namespace KidLock.App.Hooks;

/// <summary>
/// Instala os ganchos globais de baixo nível (WH_KEYBOARD_LL e WH_MOUSE_LL) numa thread dedicada,
/// com laço de mensagens próprio e prioridade alta.
/// </summary>
/// <remarks>
/// <para>
/// Por que uma thread só para isso: o Windows chama o gancho na thread que o instalou e, se a
/// resposta demorar mais que LowLevelHooksTimeout (~1 s), remove o gancho em silêncio. Separando da
/// UI, nada que a interface faça (desenhar, abrir menu, ler arquivo) consegue atrasar a decisão.
/// </para>
/// <para>
/// Política de falha aberta: qualquer exceção ao processar um evento deixa o evento passar. Um bug
/// nunca pode deixar o computador sem teclado.
/// </para>
/// </remarks>
internal sealed class InputHookHost : IDisposable
{
    /// <summary>Marca os eventos que o próprio KidLock injeta, para o filtro ignorá-los.</summary>
    public const nuint SelfInjectionMarker = 0x4B4C4F43; // "KLOC"

    private const uint WmRearm = NativeMethods.WM_APP + 1;
    private const uint WmSendMenuMask = NativeMethods.WM_APP + 2;

    private readonly Func<KeyboardInput, KeyboardResult> _onKeyboard;
    private readonly Func<MouseInput, InputDecision> _onMouse;
    private readonly Action<Exception> _onError;

    // Referências vivas obrigatórias: se o GC coletar o delegate, o Windows chama memória inválida.
    private readonly NativeMethods.LowLevelHookProc _keyboardProc;
    private readonly NativeMethods.LowLevelHookProc _mouseProc;

    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private uint _threadId;
    private nint _keyboardHook;
    private nint _mouseHook;
    private Exception? _startError;
    private int _rearmCount;
    private bool _disposed;

    public InputHookHost(
        Func<KeyboardInput, KeyboardResult> onKeyboard,
        Func<MouseInput, InputDecision> onMouse,
        Action<Exception> onError)
    {
        _onKeyboard = onKeyboard ?? throw new ArgumentNullException(nameof(onKeyboard));
        _onMouse = onMouse ?? throw new ArgumentNullException(nameof(onMouse));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    /// <summary>Quantas reinstalações já foram concluídas (útil para diagnóstico e testes).</summary>
    public int RearmCount => Volatile.Read(ref _rearmCount);

    /// <summary>Inicia a thread e só retorna depois que os dois ganchos estão ativos.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is not null)
        {
            throw new InvalidOperationException("Os ganchos já foram iniciados.");
        }

        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "KidLock input hooks",
            Priority = ThreadPriority.Highest,
        };
        _thread.Start();

        if (!_started.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Os ganchos de entrada não responderam a tempo.");
        }

        if (_startError is not null)
        {
            throw new InvalidOperationException("Não foi possível instalar os ganchos de teclado e mouse.", _startError);
        }
    }

    /// <summary>
    /// Reinstala os ganchos (assíncrono). Usado após voltar da suspensão ou desbloquear a sessão,
    /// momentos em que o Windows pode ter descartado ganchos sem avisar.
    /// </summary>
    public void Rearm() => Post(WmRearm);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_thread is { IsAlive: true })
        {
            Post(NativeMethods.WM_QUIT);
            _thread.Join(TimeSpan.FromSeconds(5));
        }

        _started.Dispose();
    }

    private void Post(uint message)
    {
        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, message, 0, 0);
        }
    }

    private void MessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        // Força a criação da fila de mensagens antes de alguém chamar PostThreadMessage.
        NativeMethods.PeekMessage(out _, 0, 0, 0, NativeMethods.PM_NOREMOVE);

        try
        {
            (_keyboardHook, _mouseHook) = InstallHooks();
        }
        catch (Win32Exception ex)
        {
            _startError = ex;
            _started.Set();
            return;
        }

        _started.Set();
        try
        {
            while (NativeMethods.GetMessage(out var msg, 0, 0, 0) > 0)
            {
                if (msg.hwnd == 0 && msg.message == WmRearm)
                {
                    Reinstall();
                    continue;
                }

                if (msg.hwnd == 0 && msg.message == WmSendMenuMask)
                {
                    SendMenuMask();
                    continue;
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
        }
    }

    private (nint Keyboard, nint Mouse) InstallHooks()
    {
        var module = NativeMethods.GetModuleHandle(null);
        var keyboard = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (keyboard == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) falhou.");
        }

        var mouse = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);
        if (mouse == 0)
        {
            var error = Marshal.GetLastWin32Error();
            NativeMethods.UnhookWindowsHookEx(keyboard);
            throw new Win32Exception(error, "SetWindowsHookEx(WH_MOUSE_LL) falhou.");
        }

        return (keyboard, mouse);
    }

    private void Reinstall()
    {
        try
        {
            // Instala os novos antes de remover os antigos: não existe janela de tempo sem proteção.
            var (keyboard, mouse) = InstallHooks();
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _keyboardHook = keyboard;
            _mouseHook = mouse;
            Interlocked.Increment(ref _rearmCount);
        }
        catch (Win32Exception ex)
        {
            ReportError(ex);
        }
    }

    private void SendMenuMask()
    {
        NativeMethods.INPUT[] inputs =
        [
            NativeMethods.KeyboardInput(VirtualKeys.MenuMask, keyUp: false, SelfInjectionMarker),
            NativeMethods.KeyboardInput(VirtualKeys.MenuMask, keyUp: true, SelfInjectionMarker),
        ];

        if (NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>()) != inputs.Length)
        {
            ReportError(new Win32Exception(Marshal.GetLastWin32Error(), "SendInput (máscara de menu) falhou."));
        }
    }

    private nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (data.dwExtraInfo != SelfInjectionMarker
                    && HookEventMapper.TryMapKeyboard((int)wParam, data.vkCode, data.flags, out var input))
                {
                    var result = _onKeyboard(input);
                    if (result.RequestMenuMask)
                    {
                        // Não injetamos de dentro do callback: a injeção roda logo depois, no laço de mensagens.
                        Post(WmSendMenuMask);
                    }

                    if (result.Decision == InputDecision.Block)
                    {
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        return NativeMethods.CallNextHookEx(0, code, wParam, lParam);
    }

    private nint MouseProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                if (data.dwExtraInfo != SelfInjectionMarker
                    && HookEventMapper.TryMapMouse((int)wParam, data.mouseData, data.flags, out var input)
                    && _onMouse(input) == InputDecision.Block)
                {
                    return 1;
                }
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        return NativeMethods.CallNextHookEx(0, code, wParam, lParam);
    }

    private void ReportError(Exception exception)
    {
        try
        {
            _onError(exception);
        }
        catch (Exception)
        {
            // O relato de erro nunca pode derrubar o gancho.
        }
    }
}
