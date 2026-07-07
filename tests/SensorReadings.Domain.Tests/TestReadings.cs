using SensorReadings.Domain;

namespace SensorReadings.Domain.Tests;

internal static class TestReadings
{
    /// <summary>
    /// Builds a valid reading, letting each test override only the fields it
    /// cares about. Fails the test immediately if the combination is invalid.
    /// </summary>
    public static SensorReading Reading(
        string deviceId = "PUMP-01",
        string metric = "temperature",
        string ts = "2025-06-01T08:00:00Z",
        double value = 20.0,
        long seq = 1)
    {
        var raw = new RawReading(deviceId, metric, ts, value, seq);
        if (!SensorReading.TryCreate(raw, out var reading, out var reason))
            throw new InvalidOperationException($"Test data is not a valid reading: {reason}");

        return reading;
    }
}
