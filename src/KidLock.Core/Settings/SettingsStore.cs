using System.Text.Encodings.Web;
using System.Text.Json;

namespace KidLock.Core.Settings;

/// <summary>Lê e grava settings.json. Arquivo ausente ou corrompido nunca impede o programa de abrir.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // O padrão escapa "+" como +; o arquivo é local e editado à mão, então mantemos legível.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TimeProvider _time;

    public SettingsStore(string filePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _time = timeProvider ?? TimeProvider.System;
    }

    public string FilePath { get; }

    public SettingsLoadResult Load()
    {
        var warnings = new List<string>();
        AppSettings settings;

        if (!File.Exists(FilePath))
        {
            settings = new AppSettings();
            Save(settings);
        }
        else
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(ReadWithRetry(), JsonOptions) ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                var backupPath = $"{FilePath}.invalido-{_time.GetLocalNow():yyyyMMdd-HHmmss}";
                File.Copy(FilePath, backupPath, overwrite: true);
                settings = new AppSettings();
                Save(settings);
                warnings.Add($"settings.json estava inválido ({ex.Message}). Cópia salva em \"{backupPath}\"; valores padrão restaurados.");
            }
        }

        var validated = SettingsValidator.Validate(settings);
        return validated with { Warnings = [.. warnings, .. validated.Warnings] };
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        // Grava ao lado e troca de uma vez: quem lê nunca encontra um arquivo pela metade.
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    private string ReadWithRetry()
    {
        // Editores costumam manter o arquivo aberto por alguns milissegundos ao salvar.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(FilePath);
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }
}
