namespace SensorReadings.Domain;

/// <summary>
/// Summary of one ingestion run: how many lines came in and what happened to them.
/// TotalLines = StoredReadings + DuplicatesRemoved + InvalidRejected.
/// </summary>
public sealed record IngestionReport(
    long TotalLines,
    long StoredReadings,
    long DuplicatesRemoved,
    long InvalidRejected,
    IReadOnlyDictionary<RejectionReason, long> RejectionsByReason);
