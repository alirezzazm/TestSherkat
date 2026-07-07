using SensorReadings.Domain;

namespace SensorReadings.Domain.Tests;

/// <summary>
/// Assumptions under test:
/// - a reading needs a non-empty deviceId and metric, an ISO-8601 UTC timestamp
///   (explicit 'Z'), a finite numeric value and a non-negative seq;
/// - a timestamp without a timezone is ambiguous and therefore invalid;
/// - impossible calendar dates (June 31) are invalid even if they look ISO-ish;
/// - sentinel values such as -9999 and 1000000 are device error markers, not
///   measurements, and must be rejected.
/// </summary>
public class ValidationTests
{
    private static RawReading ValidRaw(
        string? deviceId = "PUMP-01",
        string? metric = "temperature",
        string? ts = "2025-06-01T08:33:00Z",
        double? value = 67.21,
        long? seq = 1199)
        => new(deviceId, metric, ts, value, seq);

    [Fact]
    public void Valid_reading_is_accepted_with_all_fields_parsed()
    {
        var accepted = SensorReading.TryCreate(ValidRaw(), out var reading, out _);

        Assert.True(accepted);
        Assert.Equal("PUMP-01", reading!.DeviceId);
        Assert.Equal("temperature", reading.Metric);
        Assert.Equal(new DateTimeOffset(2025, 6, 1, 8, 33, 0, TimeSpan.Zero), reading.Timestamp);
        Assert.Equal(67.21, reading.Value);
        Assert.Equal(1199, reading.Seq);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_empty_device_id_is_rejected(string? deviceId)
    {
        var accepted = SensorReading.TryCreate(ValidRaw(deviceId: deviceId), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.MissingDeviceId, reason);
    }

    [Fact]
    public void Missing_metric_is_rejected()
    {
        var accepted = SensorReading.TryCreate(ValidRaw(metric: null), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.MissingMetric, reason);
    }

    [Theory]
    [InlineData(null)]                     // missing
    [InlineData("2025-06-01T08:00:05")]    // no timezone
    [InlineData("2025-06-31T08:04:10Z")]   // June has 30 days
    [InlineData("not-a-date")]
    public void Invalid_timestamp_is_rejected(string? ts)
    {
        var accepted = SensorReading.TryCreate(ValidRaw(ts: ts), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.InvalidTimestamp, reason);
    }

    [Fact]
    public void Missing_value_is_rejected()
    {
        var accepted = SensorReading.TryCreate(ValidRaw(value: null), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.MissingOrNonNumericValue, reason);
    }

    [Theory]
    [InlineData(-9999)]
    [InlineData(1_000_000)]
    public void Sentinel_value_is_rejected_as_implausible(double value)
    {
        var accepted = SensorReading.TryCreate(ValidRaw(value: value), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.ImplausibleValue, reason);
    }

    [Fact]
    public void Missing_seq_is_rejected()
    {
        var accepted = SensorReading.TryCreate(ValidRaw(seq: null), out _, out var reason);

        Assert.False(accepted);
        Assert.Equal(RejectionReason.MissingSeq, reason);
    }
}
