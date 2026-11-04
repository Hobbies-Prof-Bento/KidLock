namespace KidLock.Core.Input;

/// <summary>
/// Teclas fisicamente pressionadas, reconstruídas a partir dos eventos do gancho.
/// </summary>
/// <remarks>
/// GetAsyncKeyState não serve aqui: teclas engolidas pelo gancho nunca chegam ao estado do sistema.
/// Cada tecla guarda o instante do último evento. Assim, uma soltura perdida (por exemplo, Ctrl+Alt+Del
/// troca para a área de trabalho segura e o gancho não vê as solturas) não impede o atalho para sempre:
/// a tecla "fantasma" deixa de contar depois de alguns segundos sem eventos.
/// </remarks>
internal sealed class KeyboardState
{
    private readonly Dictionary<int, DateTimeOffset> _lastSeen = [];

    public void Press(int virtualKey, DateTimeOffset now) => _lastSeen[virtualKey] = now;

    public void Release(int virtualKey) => _lastSeen.Remove(virtualKey);

    public void Clear() => _lastSeen.Clear();

    /// <summary>A tecla foi pressionada e ainda não vimos a soltura (mesmo que esteja "velha").</summary>
    public bool IsTracked(int virtualKey) => _lastSeen.ContainsKey(virtualKey);

    /// <summary>A tecla está pressionada e teve evento recente.</summary>
    public bool IsActive(int virtualKey, DateTimeOffset now, TimeSpan staleAfter) =>
        _lastSeen.TryGetValue(virtualKey, out var seen) && now - seen <= staleAfter;

    public ModifierKeys ActiveModifiers(DateTimeOffset now, TimeSpan staleAfter)
    {
        var modifiers = ModifierKeys.None;
        foreach (var (key, seen) in _lastSeen)
        {
            if (now - seen <= staleAfter)
            {
                modifiers |= VirtualKeys.ToModifier(key);
            }
        }

        return modifiers;
    }

    /// <summary>Existe outra tecla comum (não modificadora) ativa além de <paramref name="exceptVirtualKey"/>?</summary>
    public bool HasOtherActiveKey(int exceptVirtualKey, DateTimeOffset now, TimeSpan staleAfter)
    {
        foreach (var (key, seen) in _lastSeen)
        {
            if (key != exceptVirtualKey && !VirtualKeys.IsModifier(key) && now - seen <= staleAfter)
            {
                return true;
            }
        }

        return false;
    }
}
