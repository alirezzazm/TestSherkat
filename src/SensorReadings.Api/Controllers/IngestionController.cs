using Microsoft.AspNetCore.Mvc;
using SensorReadings.Api.Contracts;

namespace SensorReadings.Api.Controllers;

[ApiController]
[Route("api/ingestion")]
public sealed class IngestionController : ControllerBase
{
    private readonly IngestionReportStore _reportStore;

    public IngestionController(IngestionReportStore reportStore)
    {
        _reportStore = reportStore;
    }

    /// <summary>
    /// Count report of the startup ingestion run: how many lines were read and
    /// what happened to each of them.
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IngestionReportResponse> GetReport()
    {
        var report = _reportStore.Get();

        return new IngestionReportResponse(
            report.TotalLines,
            report.StoredReadings,
            report.DuplicatesRemoved,
            report.InvalidRejected,
            report.RejectionsByReason.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
    }
}
