namespace SensorReadings.Domain;

/// <summary>
/// The identity of a reading. Two readings with the same key are the same
/// reading as far as the business is concerned, regardless of their value.
/// </summary>
public readonly record struct ReadingKey(
    string DeviceId,
    string Metric,
    DateTimeOffset Timestamp,
    long Seq);
