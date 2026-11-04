using KidLock.App.Infrastructure;

namespace KidLock.App.Tests.Infrastructure;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"KidLock.Tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Dispose_FlushesAllQueuedLines()
    {
        string logFile;
        using (var logger = new FileLogger(_directory))
        {
            logFile = logger.CurrentLogFile;
            for (var i = 0; i < 100; i++)
            {
                logger.Info($"linha {i}");
            }

            logger.Error("falhou", new InvalidOperationException("detalhe"));
        }

        var lines = File.ReadAllLines(logFile);
        Assert.Equal(101, lines.Length);
        Assert.Contains("[INFO] linha 0", lines[0], StringComparison.Ordinal);
        Assert.Contains("[ERROR] falhou: System.InvalidOperationException: detalhe", lines[100], StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DeletesLogsOlderThanRetention()
    {
        Directory.CreateDirectory(_directory);
        var oldLog = Path.Combine(_directory, "kidlock-20200101.log");
        var recentLog = Path.Combine(_directory, "kidlock-recent.log");
        File.WriteAllText(oldLog, "velho");
        File.WriteAllText(recentLog, "novo");
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-30));

        using (new FileLogger(_directory, retentionDays: 14))
        {
        }

        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(recentLog));
    }

    [Fact]
    public void LoggingAfterDispose_IsIgnored()
    {
        var logger = new FileLogger(_directory);
        logger.Dispose();

        logger.Info("depois do fim");
    }
}
