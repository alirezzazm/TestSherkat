namespace SensorReadings.Domain.Aggregation;

/// <summary>
/// Splits a half-open time range [from, to) into consecutive buckets of equal
/// size and computes count/average/min/max of the readings in each bucket.
/// </summary>
public static class TimeBucketAggregator
{
    /// <summary>
    /// Upper bound on the number of buckets a single query may produce, so that a
    /// huge range with a tiny bucket size cannot blow up memory. 10k buckets is
    /// far more than any reasonable chart needs.
    /// </summary>
    public const int MaxBucketsPerQuery = 10_000;

    /// <summary>
    /// Buckets are aligned to <paramref name="from"/>: the first bucket starts
    /// exactly at 'from', the next one bucketSize later, and so on. The last
    /// bucket may be logically cut short by 'to'. Readings outside [from, to)
    /// are ignored. Empty buckets are returned with Count = 0 so the caller
    /// always gets a contiguous series.
    /// </summary>
    public static IReadOnlyList<AggregationBucket> Aggregate(
        IEnumerable<SensorReading> readings,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeSpan bucketSize)
    {
        if (to <= from)
            throw new ArgumentException("'to' must be after 'from'.");
        if (bucketSize <= TimeSpan.Zero)
            throw new ArgumentException("Bucket size must be positive.");

        // Integer arithmetic on ticks, so readings sitting exactly on a bucket
        // boundary always land in the right bucket (no floating point edges).
        var bucketCount = (long)Math.Ceiling((to - from).Ticks / (double)bucketSize.Ticks);
        if (bucketCount > MaxBucketsPerQuery)
            throw new ArgumentException(
                $"The query would produce {bucketCount} buckets (maximum is {MaxBucketsPerQuery}). " +
                "Use a bigger bucket size or a smaller time range.");

        var counts = new int[bucketCount];
        var sums = new double[bucketCount];
        var mins = new double[bucketCount];
        var maxs = new double[bucketCount];

        // Single pass over the readings; order does not matter, which also takes
        // care of the input file not being sorted by time.
        foreach (var reading in readings)
        {
            if (reading.Timestamp < from || reading.Timestamp >= to)
                continue;

            var index = (reading.Timestamp - from).Ticks / bucketSize.Ticks;

            if (counts[index] == 0)
            {
                mins[index] = reading.Value;
                maxs[index] = reading.Value;
            }
            else
            {
                mins[index] = Math.Min(mins[index], reading.Value);
                maxs[index] = Math.Max(maxs[index], reading.Value);
            }

            counts[index]++;
            sums[index] += reading.Value;
        }

        var buckets = new AggregationBucket[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            var start = from + i * bucketSize;
            buckets[i] = counts[i] == 0
                ? new AggregationBucket(start, 0, null, null, null)
                : new AggregationBucket(start, counts[i], sums[i] / counts[i], mins[i], maxs[i]);
        }

        return buckets;
    }
}
