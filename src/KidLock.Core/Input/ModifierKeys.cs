namespace KidLock.Core.Input;

/// <summary>Modificadores de um atalho, sem distinguir lado esquerdo/direito.</summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}
