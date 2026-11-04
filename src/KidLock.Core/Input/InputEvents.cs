namespace KidLock.Core.Input;

public enum InputDecision
{
    /// <summary>Entregar o evento ao Windows normalmente.</summary>
    Pass,

    /// <summary>Engolir o evento: nenhum programa o recebe.</summary>
    Block,
}

/// <param name="VirtualKey">Código de tecla virtual reportado pelo gancho (lado esquerdo/direito distinto).</param>
/// <param name="IsKeyDown">true para pressionar (inclui auto-repetição), false para soltar.</param>
/// <param name="IsInjected">Evento sintético (SendInput, ferramentas de automação), não do hardware.</param>
public readonly record struct KeyboardInput(int VirtualKey, bool IsKeyDown, bool IsInjected = false);

/// <param name="Decision">O que fazer com o evento.</param>
/// <param name="RequestMenuMask">
/// O atalho foi reconhecido com Alt ou Win já entregues ao Windows: a camada nativa deve injetar
/// <see cref="VirtualKeys.MenuMask"/> para que a soltura de Alt/Win não abra menus.
/// </param>
public readonly record struct KeyboardResult(InputDecision Decision, bool RequestMenuMask = false)
{
    public static readonly KeyboardResult Pass = new(InputDecision.Pass);
    public static readonly KeyboardResult Block = new(InputDecision.Block);
}

public enum MouseAction
{
    Move,
    ButtonDown,
    ButtonUp,
    Wheel,
    HorizontalWheel,
}

public enum MouseButton
{
    None,
    Left,
    Right,
    Middle,
    X1,
    X2,
}

/// <summary>Evento de mouse ou touchpad (o Windows entrega ambos pelo mesmo gancho).</summary>
public readonly record struct MouseInput(MouseAction Action, MouseButton Button = MouseButton.None, bool IsInjected = false);
