using Microsoft.AspNetCore.Mvc;
using SensorReadings.Api.Contracts;
using SensorReadings.Domain;
using SensorReadings.Domain.Aggregation;

namespace SensorReadings.Api.Controllers;

[ApiController]
[Route("api/aggregations")]
public sealed class AggregationsController : ControllerBase
{
    private readonly IReadingRepository _repository;

    public AggregationsController(IReadingRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Aggregates the readings of one device/metric into fixed-size time buckets.
    /// </summary>
    /// <remarks>
    /// The range is half-open: readings with from &lt;= ts &lt; to are included.
    /// Buckets are aligned to 'from', and empty buckets are returned with count = 0
    /// so the series is always contiguous.
    ///
    /// Example: /api/aggregations?deviceId=PUMP-01&amp;metric=temperature&amp;from=2025-06-01T08:00:00Z&amp;to=2025-06-01T08:35:00Z&amp;bucketSeconds=300
    /// </remarks>
    /// <param name="deviceId">Device identifier, e.g. PUMP-01.</param>
    /// <param name="metric">Metric name, e.g. temperature.</param>
    /// <param name="from">Start of the range, inclusive (ISO-8601).</param>
    /// <param name="to">End of the range, exclusive (ISO-8601).</param>
    /// <param name="bucketSeconds">Bucket size in seconds.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AggregationResponse> Get(
        [FromQuery] string deviceId,
        [FromQuery] string metric,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int bucketSeconds = 60)
    {
        IReadOnlyList<AggregationBucket> buckets;
        try
        {
            var readings = _repository.GetRange(deviceId, metric, from, to);
            buckets = TimeBucketAggregator.Aggregate(readings, from, to, TimeSpan.FromSeconds(bucketSeconds));
        }
        catch (ArgumentException ex)
        {
            // The domain rejected the query (bad range, non-positive bucket, too many buckets).
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return new AggregationResponse(
            deviceId,
            metric,
            from,
            to,
            bucketSeconds,
            buckets.Select(b => new AggregationBucketDto(b.BucketStart, b.Count, b.Average, b.Min, b.Max)).ToList());
    }
}
