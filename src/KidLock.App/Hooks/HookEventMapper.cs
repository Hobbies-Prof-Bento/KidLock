using KidLock.App.Native;
using KidLock.Core.Input;

namespace KidLock.App.Hooks;

/// <summary>Traduz as mensagens cruas dos ganchos de baixo nível para os eventos do núcleo.</summary>
internal static class HookEventMapper
{
    public static bool TryMapKeyboard(int message, uint virtualKey, uint flags, out KeyboardInput input)
    {
        bool isKeyDown;
        switch (message)
        {
            case NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN:
                isKeyDown = true;
                break;
            case NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP:
                isKeyDown = false;
                break;
            default:
                input = default;
                return false;
        }

        input = new KeyboardInput((int)virtualKey, isKeyDown, (flags & NativeMethods.LLKHF_INJECTED) != 0);
        return true;
    }

    public static bool TryMapMouse(int message, uint mouseData, uint flags, out MouseInput input)
    {
        (MouseAction Action, MouseButton Button)? mapped = message switch
        {
            NativeMethods.WM_MOUSEMOVE => (MouseAction.Move, MouseButton.None),
            NativeMethods.WM_LBUTTONDOWN => (MouseAction.ButtonDown, MouseButton.Left),
            NativeMethods.WM_LBUTTONUP => (MouseAction.ButtonUp, MouseButton.Left),
            NativeMethods.WM_RBUTTONDOWN => (MouseAction.ButtonDown, MouseButton.Right),
            NativeMethods.WM_RBUTTONUP => (MouseAction.ButtonUp, MouseButton.Right),
            NativeMethods.WM_MBUTTONDOWN => (MouseAction.ButtonDown, MouseButton.Middle),
            NativeMethods.WM_MBUTTONUP => (MouseAction.ButtonUp, MouseButton.Middle),
            NativeMethods.WM_XBUTTONDOWN => (MouseAction.ButtonDown, XButton(mouseData)),
            NativeMethods.WM_XBUTTONUP => (MouseAction.ButtonUp, XButton(mouseData)),
            NativeMethods.WM_MOUSEWHEEL => (MouseAction.Wheel, MouseButton.None),
            NativeMethods.WM_MOUSEHWHEEL => (MouseAction.HorizontalWheel, MouseButton.None),
            _ => null,
        };

        if (mapped is not { } value)
        {
            input = default;
            return false;
        }

        input = new MouseInput(value.Action, value.Button, (flags & NativeMethods.LLMHF_INJECTED) != 0);
        return true;
    }

    // Para WM_XBUTTON*, a palavra alta de mouseData diz qual botão lateral (1 = X1, 2 = X2).
    private static MouseButton XButton(uint mouseData) => (mouseData >> 16) == 2 ? MouseButton.X2 : MouseButton.X1;
}
