namespace KidLock.Core.Input;

/// <summary>
/// Códigos de tecla virtual do Windows (winuser.h) usados pelo núcleo.
/// Aqui eles são apenas números; o núcleo não chama nenhuma API do sistema.
/// </summary>
public static class VirtualKeys
{
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Pause = 0x13;
    public const int CapsLock = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int PrintScreen = 0x2C;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int D0 = 0x30;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int NumPad0 = 0x60;
    public const int Multiply = 0x6A;
    public const int Add = 0x6B;
    public const int Subtract = 0x6D;
    public const int Decimal = 0x6E;
    public const int Divide = 0x6F;
    public const int F1 = 0x70;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;

    /// <summary>
    /// Código sem atribuição. Injetado entre Alt/Win e sua soltura para que o Windows não abra
    /// a barra de menus ou o menu Iniciar quando engolimos a tecla final de um atalho.
    /// </summary>
    public const int MenuMask = 0xE8;

    /// <summary>Tecla de função F1..F24.</summary>
    public static int F(int number) =>
        number is >= 1 and <= 24 ? F1 + number - 1 : throw new ArgumentOutOfRangeException(nameof(number));

    public static ModifierKeys ToModifier(int virtualKey) => virtualKey switch
    {
        Control or LControl or RControl => ModifierKeys.Ctrl,
        Menu or LMenu or RMenu => ModifierKeys.Alt,
        Shift or LShift or RShift => ModifierKeys.Shift,
        LWin or RWin => ModifierKeys.Win,
        _ => ModifierKeys.None,
    };

    public static bool IsModifier(int virtualKey) => ToModifier(virtualKey) != ModifierKeys.None;
}
