using Microsoft.Extensions.Logging.Abstractions;
using SensorReadings.Domain;

namespace SensorReadings.Domain.Tests;

/// <summary>
/// Assumptions under test:
/// - the identity of a reading is the (deviceId, metric, ts, seq) quadruple;
/// - the first occurrence of a key wins, later occurrences are dropped even
///   when they carry a different value (but the conflict is surfaced);
/// - readings that differ in any part of the key are different readings;
/// - the ingestion pipeline counts every input line exactly once, so
///   total = stored + duplicates + invalid.
/// </summary>
public class DeduplicationTests
{
    [Fact]
    public void Same_reading_arriving_twice_is_accepted_only_once()
    {
        var deduplicator = new ReadingDeduplicator();
        var reading = TestReadings.Reading();

        Assert.True(deduplicator.TryAccept(reading, out _));
        Assert.False(deduplicator.TryAccept(reading, out var conflicts));
        Assert.False(conflicts); // same value, plain resend
    }

    [Fact]
    public void Duplicate_with_different_value_is_dropped_and_reported_as_conflict()
    {
        var deduplicator = new ReadingDeduplicator();

        Assert.True(deduplicator.TryAccept(TestReadings.Reading(value: 20.0), out _));
        Assert.False(deduplicator.TryAccept(TestReadings.Reading(value: 99.0), out var conflicts));
        Assert.True(conflicts);
    }

    [Fact]
    public void Readings_differing_only_in_seq_are_not_duplicates()
    {
        var deduplicator = new ReadingDeduplicator();

        Assert.True(deduplicator.TryAccept(TestReadings.Reading(seq: 1), out _));
        Assert.True(deduplicator.TryAccept(TestReadings.Reading(seq: 2), out _));
    }

    [Fact]
    public void Ingestion_stores_unique_readings_and_counts_the_rest()
    {
        var duplicate = new RawReading("PUMP-01", "temperature", "2025-06-01T08:00:00Z", 20.0, 1);
        var source = new ListSource(
            duplicate,
            new RawReading("PUMP-01", "temperature", "2025-06-01T08:00:10Z", 21.0, 2),
            duplicate,                                                          // exact duplicate
            new RawReading("PUMP-01", "temperature", "2025-06-01T08:00:00Z", 55.0, 1), // conflicting duplicate
            new RawReading(null, "temperature", "2025-06-01T08:00:20Z", 22.0, 3),      // invalid
            null);                                                              // malformed line

        var repository = new ListRepository();
        var service = new IngestionService(source, repository, NullLogger<IngestionService>.Instance);

        var report = service.Run();

        Assert.Equal(6, report.TotalLines);
        Assert.Equal(2, report.StoredReadings);
        Assert.Equal(2, report.DuplicatesRemoved);
        Assert.Equal(2, report.InvalidRejected);
        Assert.Equal(1, report.RejectionsByReason[RejectionReason.MissingDeviceId]);
        Assert.Equal(1, report.RejectionsByReason[RejectionReason.MalformedLine]);
        Assert.Equal(2, repository.Stored.Count);
        Assert.Equal(report.TotalLines,
            report.StoredReadings + report.DuplicatesRemoved + report.InvalidRejected);
    }

    [Fact]
    public void Conflicting_duplicate_keeps_the_first_value()
    {
        var source = new ListSource(
            new RawReading("PUMP-01", "temperature", "2025-06-01T08:00:00Z", 20.0, 1),
            new RawReading("PUMP-01", "temperature", "2025-06-01T08:00:00Z", 99.0, 1));

        var repository = new ListRepository();
        new IngestionService(source, repository, NullLogger<IngestionService>.Instance).Run();

        var stored = Assert.Single(repository.Stored);
        Assert.Equal(20.0, stored.Value);
    }

    // Minimal hand-rolled fakes; a null raw reading stands for a malformed line.

    private sealed class ListSource : IReadingSource
    {
        private readonly RawReading?[] _readings;

        public ListSource(params RawReading?[] readings) => _readings = readings;

        public IEnumerable<SourceLine> ReadAll()
            => _readings.Select((raw, index) => new SourceLine(index + 1, raw));
    }

    private sealed class ListRepository : IReadingRepository
    {
        public List<SensorReading> Stored { get; } = [];

        public void Add(SensorReading reading) => Stored.Add(reading);

        public IReadOnlyList<SensorReading> GetRange(
            string deviceId, string metric, DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
            => Stored
                .Where(r => r.DeviceId == deviceId && r.Metric == metric
                            && r.Timestamp >= fromInclusive && r.Timestamp < toExclusive)
                .ToList();
    }
}
