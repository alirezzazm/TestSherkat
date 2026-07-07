using SensorReadings.Domain;

namespace SensorReadings.Infrastructure;

/// <summary>
/// Keeps the cleaned readings in memory, grouped by device and metric so a query
/// only ever scans the series it asks for. Good enough for this dataset (a few
/// thousand rows) — see the README for the storage discussion.
///
/// Not synchronized: ingestion runs once at startup before the API starts
/// serving requests, and after that the store is only read.
/// </summary>
public sealed class InMemoryReadingRepository : IReadingRepository
{
    private readonly Dictionary<(string DeviceId, string Metric), List<SensorReading>> _series = [];

    public void Add(SensorReading reading)
    {
        var key = (reading.DeviceId, reading.Metric);
        if (!_series.TryGetValue(key, out var readings))
        {
            readings = [];
            _series[key] = readings;
        }

        readings.Add(reading);
    }

    public IReadOnlyList<SensorReading> GetRange(
        string deviceId,
        string metric,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive)
    {
        if (!_series.TryGetValue((deviceId, metric), out var readings))
            return [];

        return readings
            .Where(r => r.Timestamp >= fromInclusive && r.Timestamp < toExclusive)
            .ToList();
    }
}
