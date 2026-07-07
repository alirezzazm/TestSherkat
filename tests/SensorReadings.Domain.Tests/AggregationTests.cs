using SensorReadings.Domain.Aggregation;

namespace SensorReadings.Domain.Tests;

/// <summary>
/// Assumptions under test:
/// - the query range is half-open [from, to): a reading exactly at 'from' is
///   included, a reading exactly at 'to' is not;
/// - buckets are aligned to 'from' and a reading exactly on a bucket boundary
///   belongs to the bucket that starts there;
/// - empty buckets are returned with count 0 and null statistics;
/// - the input does not need to be sorted by time;
/// - nonsensical queries (inverted range, non-positive bucket, absurdly many
///   buckets) are rejected with an ArgumentException.
/// </summary>
public class AggregationTests
{
    private static readonly DateTimeOffset From = new(2025, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Statistics_are_computed_per_bucket()
    {
        var readings = new[]
        {
            TestReadings.Reading(ts: "2025-06-01T08:00:10Z", value: 10, seq: 1),
            TestReadings.Reading(ts: "2025-06-01T08:00:30Z", value: 30, seq: 2),
            TestReadings.Reading(ts: "2025-06-01T08:01:10Z", value: 5, seq: 3),
        };

        var buckets = TimeBucketAggregator.Aggregate(
            readings, From, From.AddMinutes(2), TimeSpan.FromSeconds(60));

        Assert.Equal(2, buckets.Count);

        Assert.Equal(From, buckets[0].BucketStart);
        Assert.Equal(2, buckets[0].Count);
        Assert.Equal(20, buckets[0].Average);
        Assert.Equal(10, buckets[0].Min);
        Assert.Equal(30, buckets[0].Max);

        Assert.Equal(From.AddSeconds(60), buckets[1].BucketStart);
        Assert.Equal(1, buckets[1].Count);
        Assert.Equal(5, buckets[1].Average);
    }

    [Fact]
    public void Range_is_half_open_from_inclusive_to_exclusive()
    {
        var readings = new[]
        {
            TestReadings.Reading(ts: "2025-06-01T08:00:00Z", value: 1, seq: 1), // exactly 'from'
            TestReadings.Reading(ts: "2025-06-01T08:02:00Z", value: 2, seq: 2), // exactly 'to'
        };

        var buckets = TimeBucketAggregator.Aggregate(
            readings, From, From.AddMinutes(2), TimeSpan.FromSeconds(60));

        Assert.Equal(1, buckets[0].Count);
        Assert.Equal(0, buckets[1].Count);
    }

    [Fact]
    public void Reading_on_a_bucket_boundary_falls_into_the_bucket_that_starts_there()
    {
        var readings = new[]
        {
            TestReadings.Reading(ts: "2025-06-01T08:01:00Z", value: 7, seq: 1),
        };

        var buckets = TimeBucketAggregator.Aggregate(
            readings, From, From.AddMinutes(2), TimeSpan.FromSeconds(60));

        Assert.Equal(0, buckets[0].Count);
        Assert.Equal(1, buckets[1].Count);
    }

    [Fact]
    public void Empty_buckets_are_returned_with_count_zero_and_null_statistics()
    {
        var buckets = TimeBucketAggregator.Aggregate(
            [], From, From.AddMinutes(3), TimeSpan.FromSeconds(60));

        Assert.Equal(3, buckets.Count);
        Assert.All(buckets, b =>
        {
            Assert.Equal(0, b.Count);
            Assert.Null(b.Average);
            Assert.Null(b.Min);
            Assert.Null(b.Max);
        });
    }

    [Fact]
    public void Input_order_does_not_change_the_result()
    {
        var readings = new[]
        {
            TestReadings.Reading(ts: "2025-06-01T08:01:30Z", value: 3, seq: 3),
            TestReadings.Reading(ts: "2025-06-01T08:00:10Z", value: 1, seq: 1),
            TestReadings.Reading(ts: "2025-06-01T08:00:50Z", value: 2, seq: 2),
        };

        var shuffled = TimeBucketAggregator.Aggregate(
            readings, From, From.AddMinutes(2), TimeSpan.FromSeconds(60));
        var sorted = TimeBucketAggregator.Aggregate(
            readings.OrderBy(r => r.Timestamp), From, From.AddMinutes(2), TimeSpan.FromSeconds(60));

        Assert.Equal(sorted, shuffled);
    }

    [Fact]
    public void Range_not_divisible_by_bucket_size_still_covers_the_whole_range()
    {
        // 150 seconds with 60-second buckets -> three buckets, the last one short.
        var readings = new[]
        {
            TestReadings.Reading(ts: "2025-06-01T08:02:20Z", value: 4, seq: 1), // second 140
        };

        var buckets = TimeBucketAggregator.Aggregate(
            readings, From, From.AddSeconds(150), TimeSpan.FromSeconds(60));

        Assert.Equal(3, buckets.Count);
        Assert.Equal(1, buckets[2].Count);
    }

    [Theory]
    [InlineData(0)]     // 'to' == 'from'
    [InlineData(-60)]   // 'to' before 'from'
    public void Inverted_or_empty_range_is_rejected(int rangeSeconds)
    {
        Assert.Throws<ArgumentException>(() => TimeBucketAggregator.Aggregate(
            [], From, From.AddSeconds(rangeSeconds), TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Non_positive_bucket_size_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TimeBucketAggregator.Aggregate(
            [], From, From.AddMinutes(1), TimeSpan.Zero));
    }

    [Fact]
    public void Query_producing_too_many_buckets_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TimeBucketAggregator.Aggregate(
            [], From, From.AddDays(365), TimeSpan.FromSeconds(1)));
    }
}
