using System.Collections.Concurrent;

namespace KidLock.App.Infrastructure;

/// <summary>
/// Log diário em arquivo. Gravar nunca bloqueia quem chama (pode ser a thread do gancho):
/// as linhas vão para uma fila e uma thread de fundo escreve no disco.
/// </summary>
internal sealed class FileLogger : IDisposable
{
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly BlockingCollection<string> _queue = new(boundedCapacity: 4096);
    private readonly Thread _writer;

    public FileLogger(string directory, TimeProvider? timeProvider = null, int retentionDays = 14)
    {
        _directory = directory;
        _time = timeProvider ?? TimeProvider.System;
        Directory.CreateDirectory(directory);
        DeleteOldLogs(TimeSpan.FromDays(retentionDays));
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "KidLock logger" };
        _writer.Start();
    }

    public string LogDirectory => _directory;

    public string CurrentLogFile => Path.Combine(_directory, $"kidlock-{_time.GetLocalNow():yyyyMMdd}.log");

    public void Info(string message) => Enqueue("INFO", message);

    public void Warn(string message) => Enqueue("WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Enqueue("ERROR", exception is null ? message : $"{message}: {exception}");

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_writer.Join(TimeSpan.FromSeconds(2)))
        {
            _queue.Dispose();
        }
    }

    private void Enqueue(string level, string message)
    {
        try
        {
            _queue.TryAdd($"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
        }
        catch (InvalidOperationException)
        {
            // Logger já encerrado: descarta.
        }
    }

    private void WriteLoop()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disco cheio ou arquivo bloqueado não podem derrubar o programa.
            }
        }
    }

    private void DeleteOldLogs(TimeSpan retention)
    {
        var limit = _time.GetUtcNow().UtcDateTime - retention;
        foreach (var file in Directory.EnumerateFiles(_directory, "kidlock-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < limit)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Tenta de novo na próxima inicialização.
            }
        }
    }
}
