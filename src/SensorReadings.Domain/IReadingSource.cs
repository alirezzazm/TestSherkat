namespace SensorReadings.Domain;

/// <summary>
/// One line coming from a reading source. <see cref="Reading"/> is null when the
/// line could not be parsed at all (e.g. truncated JSON), so the ingestion can
/// still count it as rejected instead of crashing.
/// </summary>
public sealed record SourceLine(long LineNumber, RawReading? Reading);

/// <summary>
/// Where raw readings come from. The current implementation reads a .jsonl file;
/// a message broker or an HTTP feed would just be another implementation of this
/// interface, with no changes to the ingestion logic.
/// </summary>
public interface IReadingSource
{
    IEnumerable<SourceLine> ReadAll();
}
