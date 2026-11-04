namespace KidLock.App.Infrastructure;

internal static class AppPaths
{
    /// <summary>Se definida, guarda configurações e logs nesta pasta (modo portátil / testes).</summary>
    public const string HomeEnvironmentVariable = "KIDLOCK_HOME";

    public static string SettingsFile => Path.Combine(Root(Environment.SpecialFolder.ApplicationData), "settings.json");

    public static string LogDirectory => Path.Combine(Root(Environment.SpecialFolder.LocalApplicationData), "logs");

    private static string Root(Environment.SpecialFolder folder) =>
        Environment.GetEnvironmentVariable(HomeEnvironmentVariable) is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(folder), "KidLock");
}
