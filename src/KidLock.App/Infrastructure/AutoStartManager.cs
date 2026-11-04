using Microsoft.Win32;

namespace KidLock.App.Infrastructure;

/// <summary>"Iniciar com o Windows" via chave Run do usuário atual (não exige administrador).</summary>
internal sealed class AutoStartManager(string valueName, string executablePath, string keyPath = AutoStartManager.RunKeyPath)
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private string Command => $"\"{executablePath}\"";

    /// <summary>Ligado só se a entrada aponta para este executável (cópias antigas em outro lugar não contam).</summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return string.Equals(key?.GetValue(valueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        if (enabled)
        {
            key.SetValue(valueName, Command);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
