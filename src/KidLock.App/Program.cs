using KidLock.App.Infrastructure;
using KidLock.App.UI;

namespace KidLock.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\KidLock.SingleInstance.6F1C2A4E";

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "O KidLock já está em execução. Procure o ícone de cadeado perto do relógio.",
                "KidLock",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 1;
        }

        using var logger = new FileLogger(AppPaths.LogDirectory);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => logger.Error("Exceção não tratada na interface", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => logger.Error("Exceção fatal", e.ExceptionObject as Exception);

        var ui = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(ui);

        TrayApplicationContext context;
        try
        {
            context = new TrayApplicationContext(logger, ui);
        }
        catch (Exception ex)
        {
            logger.Error("Falha ao iniciar", ex);
            MessageBox.Show(
                $"Não foi possível iniciar o KidLock:\n\n{ex.Message}",
                "KidLock",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        using (context)
        {
            Application.Run(context);
        }

        logger.Info("KidLock encerrado.");
        return 0;
    }
}
