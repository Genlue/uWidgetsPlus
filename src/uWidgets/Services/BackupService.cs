using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace uWidgets.Services;

/// <summary>
/// Serialization of the whole app state into a single JSON backup file and back
/// (<see cref="BackupData"/>). The file is a plain, human-readable JSON document
/// so the user can inspect it before importing.
/// </summary>
public static class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Create a backup snapshot of the current app state.</summary>
    public static BackupData Create(IAppSettingsProvider appSettings, ILayoutProvider layout) =>
        new(BackupData.CurrentVersion, appSettings.Get(), layout.Get());

    /// <summary>Serialize a backup to JSON text.</summary>
    public static string ToJson(BackupData backup) => JsonSerializer.Serialize(backup, JsonOptions);

    /// <summary>
    /// Parse and validate backup JSON.
    /// </summary>
    /// <exception cref="FormatException">Unsupported version or missing/invalid sections.</exception>
    public static BackupData Parse(string json)
    {
        BackupData? backup;
        try
        {
            backup = JsonSerializer.Deserialize<BackupData>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("The backup file is not valid JSON.", ex);
        }

        if (backup?.Version != BackupData.CurrentVersion)
            throw new FormatException($"Unsupported backup version: {backup?.Version}.");
        if (backup.AppSettings == null || backup.Layout == null || backup.AppSettings.Theme == null)
            throw new FormatException("The backup file is missing the settings or layout section.");

        return backup;
    }

    /// <summary>Write a backup to a stream (UTF-8 JSON text).</summary>
    public static async Task WriteAsync(Stream stream, BackupData backup)
    {
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(ToJson(backup));
        await writer.FlushAsync();
    }

    /// <summary>Read and validate a backup from a stream.</summary>
    public static async Task<BackupData> ReadAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(await reader.ReadToEndAsync());
    }
}