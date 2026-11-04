using System.Collections.Concurrent;
using KidLock.App.Hooks;
using KidLock.Core.Input;
using KidLock.Core.Locking;

namespace KidLock.App.Tests.Integration;

/// <summary>Testemunha + KidLock real (InputHookHost + InputFilter), na ordem certa da cadeia de ganchos.</summary>
internal sealed class HookTestEnvironment : IDisposable
{
    public HookTestEnvironment(FilterOptions options, Func<KeyboardInput, KeyboardResult>? keyboardOverride = null)
    {
        Filter = new InputFilter(Controller, options);
        Observer = new GlobalInputObserver();
        Host = new InputHookHost(keyboardOverride ?? Filter.ProcessKeyboard, Filter.ProcessMouse, Errors.Enqueue);
        Host.Start();
    }

    public LockController Controller { get; } = new();

    public InputFilter Filter { get; }

    public GlobalInputObserver Observer { get; }

    public InputHookHost Host { get; }

    public ConcurrentQueue<Exception> Errors { get; } = new();

    public static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            Thread.Sleep(10);
        }

        return true;
    }

    public void Dispose()
    {
        // KidLock sai primeiro: qualquer bloqueio do teste termina imediatamente.
        Host.Dispose();
        Observer.Dispose();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalInputCollection
{
    public const string Name = "Entrada global do Windows";
}
