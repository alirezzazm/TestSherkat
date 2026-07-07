using Microsoft.Extensions.Logging;

namespace SensorReadings.Domain;

/// <summary>
/// Runs the ingestion pipeline: read raw lines from the source, validate them,
/// drop duplicates and store what survives. All the business rules live here
/// and in the types this service uses; the source and the store are abstractions.
/// </summary>
public sealed class IngestionService
{
    private readonly IReadingSource _source;
    private readonly IReadingRepository _repository;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IReadingSource source,
        IReadingRepository repository,
        ILogger<IngestionService> logger)
    {
        _source = source;
        _repository = repository;
        _logger = logger;
    }

    public IngestionReport Run()
    {
        _logger.LogInformation("Ingestion started.");

        var deduplicator = new ReadingDeduplicator();
        var rejections = new Dictionary<RejectionReason, long>();
        long totalLines = 0;
        long stored = 0;
        long duplicates = 0;

        foreach (var line in _source.ReadAll())
        {
            totalLines++;

            if (line.Reading is null)
            {
                Reject(rejections, RejectionReason.MalformedLine, line.LineNumber);
                continue;
            }

            if (!SensorReading.TryCreate(line.Reading, out var reading, out var reason))
            {
                Reject(rejections, reason, line.LineNumber);
                continue;
            }

            if (!deduplicator.TryAccept(reading, out var conflicts))
            {
                duplicates++;
                if (conflicts)
                {
                    _logger.LogWarning(
                        "Line {LineNumber}: duplicate of an earlier reading but with a different value; " +
                        "keeping the first one ({DeviceId}/{Metric} at {Timestamp:O}, seq {Seq}).",
                        line.LineNumber, reading.DeviceId, reading.Metric, reading.Timestamp, reading.Seq);
                }
                continue;
            }

            _repository.Add(reading);
            stored++;
        }

        var report = new IngestionReport(
            totalLines,
            stored,
            duplicates,
            rejections.Values.Sum(),
            rejections);

        _logger.LogInformation(
            "Ingestion finished: {TotalLines} lines read, {Stored} readings stored, " +
            "{Duplicates} duplicates removed, {Invalid} invalid records rejected.",
            report.TotalLines, report.StoredReadings, report.DuplicatesRemoved, report.InvalidRejected);

        return report;
    }

    private void Reject(Dictionary<RejectionReason, long> rejections, RejectionReason reason, long lineNumber)
    {
        rejections[reason] = rejections.GetValueOrDefault(reason) + 1;
        _logger.LogWarning("Line {LineNumber}: rejected ({Reason}).", lineNumber, reason);
    }
}
