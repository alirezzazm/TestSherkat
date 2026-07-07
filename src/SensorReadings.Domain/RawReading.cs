namespace SensorReadings.Domain;

/// <summary>
/// One reading exactly as it appeared in the input, before any validation.
/// Every field is nullable because the input may be missing fields or carry
/// values of the wrong type. <see cref="SensorReading.TryCreate"/> decides
/// whether a raw reading is acceptable.
/// </summary>
public sealed record RawReading(
    string? DeviceId,
    string? Metric,
    string? Timestamp,
    double? Value,
    long? Seq);
