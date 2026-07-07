namespace SensorReadings.Domain.Aggregation;

/// <summary>
/// One time bucket of an aggregation result. For empty buckets Count is 0 and
/// the statistics are null, because an average of nothing is not a number.
/// </summary>
public sealed record AggregationBucket(
    DateTimeOffset BucketStart,
    int Count,
    double? Average,
    double? Min,
    double? Max);
