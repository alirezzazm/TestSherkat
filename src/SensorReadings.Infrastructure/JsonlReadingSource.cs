using System.Text.Json;
using SensorReadings.Domain;

namespace SensorReadings.Infrastructure;

/// <summary>
/// Reads raw readings from a .jsonl file (one JSON object per line). The file is
/// streamed line by line, so memory usage does not depend on the file size.
/// Lines that are not valid JSON are passed on with a null reading so the
/// ingestion can count them instead of crashing.
/// </summary>
public sealed class JsonlReadingSource : IReadingSource
{
    private readonly string _filePath;

    public JsonlReadingSource(string filePath)
    {
        _filePath = filePath;
    }

    public IEnumerable<SourceLine> ReadAll()
    {
        long lineNumber = 0;
        foreach (var line in File.ReadLines(_filePath))
        {
            lineNumber++;
            yield return new SourceLine(lineNumber, ParseLine(line));
        }
    }

    private static RawReading? ParseLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new RawReading(
                ReadString(root, "deviceId"),
                ReadString(root, "metric"),
                ReadString(root, "ts"),
                ReadNumber(root, "value"),
                ReadInteger(root, "seq"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    // Anything that is not a JSON number (null, or strings like "NaN") comes back
    // as null; deciding whether that is acceptable is the domain's job.
    private static double? ReadNumber(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : null;

    private static long? ReadInteger(JsonElement root, string name)
        => root.TryGetProperty(name, out var property)
           && property.ValueKind == JsonValueKind.Number
           && property.TryGetInt64(out var value)
            ? value
            : null;
}
