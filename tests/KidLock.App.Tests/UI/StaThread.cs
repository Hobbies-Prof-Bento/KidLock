namespace KidLock.App.Tests.UI;

/// <summary>WinForms espera STA; o xUnit roda os testes em threads MTA.</summary>
internal static class StaThread
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new AggregateException(failure);
        }
    }
}
