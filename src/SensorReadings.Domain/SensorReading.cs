using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SensorReadings.Domain;

/// <summary>
/// A validated sensor reading. Instances can only be created through
/// <see cref="TryCreate"/>, so any <see cref="SensorReading"/> in the system
/// is guaranteed to satisfy the domain rules below.
/// </summary>
public sealed class SensorReading
{
    /// <summary>
    /// Coarse sanity bound for values. The metrics in this dataset live in small
    /// ranges (temperature ~60-80, pressure ~0-15, vibration ~0-7), while broken
    /// records carry sentinel values like -9999 and 1000000. Anything outside
    /// this bound is treated as a device error marker, not a measurement.
    /// In a real system this would be configured per metric.
    /// </summary>
    public const double MaxPlausibleAbsoluteValue = 1000;

    // The task defines ts as ISO-8601 UTC, so we require an explicit 'Z' suffix.
    // A timestamp without a timezone is ambiguous and gets rejected.
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public string DeviceId { get; }
    public string Metric { get; }
    public DateTimeOffset Timestamp { get; }
    public double Value { get; }
    public long Seq { get; }

    public ReadingKey Key => new(DeviceId, Metric, Timestamp, Seq);

    private SensorReading(string deviceId, string metric, DateTimeOffset timestamp, double value, long seq)
    {
        DeviceId = deviceId;
        Metric = metric;
        Timestamp = timestamp;
        Value = value;
        Seq = seq;
    }

    /// <summary>
    /// Validates a raw reading and turns it into a proper <see cref="SensorReading"/>.
    /// Returns false with a <see cref="RejectionReason"/> when the input breaks a rule.
    /// </summary>
    public static bool TryCreate(
        RawReading raw,
        [NotNullWhen(true)] out SensorReading? reading,
        out RejectionReason reason)
    {
        reading = null;

        if (string.IsNullOrWhiteSpace(raw.DeviceId))
        {
            reason = RejectionReason.MissingDeviceId;
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw.Metric))
        {
            reason = RejectionReason.MissingMetric;
            return false;
        }

        if (raw.Timestamp is null || !DateTimeOffset.TryParseExact(
                raw.Timestamp, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            reason = RejectionReason.InvalidTimestamp;
            return false;
        }

        if (raw.Value is not { } value || !double.IsFinite(value))
        {
            reason = RejectionReason.MissingOrNonNumericValue;
            return false;
        }

        if (Math.Abs(value) > MaxPlausibleAbsoluteValue)
        {
            reason = RejectionReason.ImplausibleValue;
            return false;
        }

        if (raw.Seq is not { } seq || seq < 0)
        {
            reason = RejectionReason.MissingSeq;
            return false;
        }

        reading = new SensorReading(raw.DeviceId, raw.Metric, timestamp, value, seq);
        reason = default;
        return true;
    }
}
