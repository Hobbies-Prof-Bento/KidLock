using System.ComponentModel;
using System.Runtime.InteropServices;
using KidLock.App.Native;

namespace KidLock.App.Tests.Integration;

/// <summary>
/// Injeta entrada real via SendInput. Usa só teclas inofensivas (F23/F24 não existem na maioria dos
/// teclados, Shift sozinho não faz nada) e movimento de mouse de ida e volta de 1 pixel.
/// </summary>
internal static class TestInput
{
    public static void Key(int virtualKey, bool keyUp, nuint marker = GlobalInputObserver.TestMarker) =>
        Send(NativeMethods.KeyboardInput(virtualKey, keyUp, marker));

    public static void Tap(int virtualKey, nuint marker = GlobalInputObserver.TestMarker) =>
        Send(
            NativeMethods.KeyboardInput(virtualKey, keyUp: false, marker),
            NativeMethods.KeyboardInput(virtualKey, keyUp: true, marker));

    /// <summary>Pressiona em ordem e solta em ordem inversa.</summary>
    public static void Chord(params int[] keys)
    {
        foreach (var key in keys)
        {
            Key(key, keyUp: false);
        }

        foreach (var key in keys.Reverse())
        {
            Key(key, keyUp: true);
        }
    }

    /// <summary>Vai e volta 1 pixel. (Movimento de 0 pixel é descartado pelo Windows antes dos ganchos.)</summary>
    public static void MouseNudge() =>
        Send(
            NativeMethods.RelativeMouseMove(1, 0, GlobalInputObserver.TestMarker),
            NativeMethods.RelativeMouseMove(-1, 0, GlobalInputObserver.TestMarker));

    private static void Send(params NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput falhou.");
        }
    }
}
