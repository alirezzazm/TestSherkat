namespace SensorReadings.Api.Contracts;

/// <summary>
/// Count report of the startup ingestion run.
/// totalLines = storedReadings + duplicatesRemoved + invalidRejected.
/// </summary>
public sealed record IngestionReportResponse(
    long TotalLines,
    long StoredReadings,
    long DuplicatesRemoved,
    long InvalidRejected,
    IReadOnlyDictionary<string, long> RejectionsByReason);
