using System.Text.Json;
using uWidgets.Core.Interfaces;

namespace uWidgets.Core.Services;

/// <inheritdoc />
public class JsonParser<T>(string filePath) : IDataProvider<T>
{
    private T? data;
    /// <inheritdoc />
    public event DataChangedEvent<T>? DataChanging;
    /// <inheritdoc />
    public event DataChangedEvent<T>? DataChanged;

    /// <inheritdoc />
    public T Get()
    {
        if (data != null) return data;

        var json = File.ReadAllText(filePath);

        data = JsonSerializer.Deserialize<T>(json)
               ?? throw new FormatException($"Can't deserialize {typeof(T).Name}");

        return data = Normalize(data);
    }

    /// <inheritdoc />
    public void Save(T newData)
    {
        var oldData = data;
        DataChanging?.Invoke(this, data, newData);
        var json = JsonSerializer.Serialize(data = newData);
        
        File.WriteAllText(filePath, json);
        DataChanged?.Invoke(this, oldData, newData);
    }

    /// <summary>
    /// Hook for normalizing deserialized data (e.g. filling in defaults for
    /// settings added in newer versions). Called once after the first read.
    /// </summary>
    protected virtual T Normalize(T value) => value;
}
