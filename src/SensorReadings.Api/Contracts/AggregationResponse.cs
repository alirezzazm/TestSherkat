namespace SensorReadings.Api.Contracts;

/// <summary>
/// Result of an aggregation query. Echoes the query parameters back so the
/// response is self-describing.
/// </summary>
public sealed record AggregationResponse(
    string DeviceId,
    string Metric,
    DateTimeOffset From,
    DateTimeOffset To,
    int BucketSeconds,
    IReadOnlyList<AggregationBucketDto> Buckets);

/// <summary>
/// One time bucket. For buckets without readings, count is 0 and the statistics
/// are null.
/// </summary>
public sealed record AggregationBucketDto(
    DateTimeOffset BucketStart,
    int Count,
    double? Average,
    double? Min,
    double? Max);
