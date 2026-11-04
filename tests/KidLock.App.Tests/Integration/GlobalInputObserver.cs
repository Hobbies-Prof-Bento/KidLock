using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using KidLock.App.Hooks;
using KidLock.App.Native;

namespace KidLock.App.Tests.Integration;

/// <summary>
/// Gancho "testemunha": é instalado ANTES do KidLock, então o Windows o chama DEPOIS. Se o KidLock
/// engolir um evento, a cadeia para e a testemunha nunca o vê. Só registra eventos marcados pelos
/// testes (ou pelo próprio KidLock), ignorando o que o usuário digitar durante a execução.
/// </summary>
internal sealed class GlobalInputObserver : IDisposable
{
    public const nuint TestMarker = 0x54455354; // "TEST"

    private readonly ConcurrentQueue<ObservedEvent> _events = new();
    private readonly NativeMethods.LowLevelHookProc _keyboardProc;
    private readonly NativeMethods.LowLevelHookProc _mouseProc;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private uint _threadId;
    private Exception? _startError;

    public GlobalInputObserver()
    {
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "Test input observer" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(10));
        if (_startError is not null)
        {
            throw new InvalidOperationException("Falha ao instalar o gancho testemunha.", _startError);
        }
    }

    public IReadOnlyList<ObservedEvent> Events => [.. _events];

    public bool WaitFor(Func<ObservedEvent, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_events.Any(predicate))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return _events.Any(predicate);
    }

    public void Dispose()
    {
        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(5));
        _ready.Dispose();
    }

    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.PeekMessage(out _, 0, 0, 0, NativeMethods.PM_NOREMOVE);
        var module = NativeMethods.GetModuleHandle(null);
        var keyboard = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        var mouse = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);
        if (keyboard == 0 || mouse == 0)
        {
            _startError = new Win32Exception(Marshal.GetLastWin32Error());
            _ready.Set();
            return;
        }

        _ready.Set();
        try
        {
            while (NativeMethods.GetMessage(out var msg, 0, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            NativeMethods.UnhookWindowsHookEx(keyboard);
            NativeMethods.UnhookWindowsHookEx(mouse);
        }
    }

    private nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.dwExtraInfo is TestMarker or InputHookHost.SelfInjectionMarker)
            {
                var isUp = (int)wParam is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
                _events.Enqueue(new ObservedEvent(IsKeyboard: true, (int)data.vkCode, isUp, data.dwExtraInfo));
            }
        }

        return NativeMethods.CallNextHookEx(0, code, wParam, lParam);
    }

    private nint MouseProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if (data.dwExtraInfo is TestMarker or InputHookHost.SelfInjectionMarker)
            {
                _events.Enqueue(new ObservedEvent(IsKeyboard: false, VirtualKey: 0, IsKeyUp: false, data.dwExtraInfo));
            }
        }

        return NativeMethods.CallNextHookEx(0, code, wParam, lParam);
    }
}

internal sealed record ObservedEvent(bool IsKeyboard, int VirtualKey, bool IsKeyUp, nuint Marker);
