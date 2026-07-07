using SensorReadings.Domain;

namespace SensorReadings.Api;

/// <summary>
/// Holds the report of the startup ingestion run so the API can expose it later.
/// </summary>
public sealed class IngestionReportStore
{
    private IngestionReport? _report;

    public void Set(IngestionReport report) => _report = report;

    public IngestionReport Get() =>
        _report ?? throw new InvalidOperationException("Ingestion has not run yet.");
}
