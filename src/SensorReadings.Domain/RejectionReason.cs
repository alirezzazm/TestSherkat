namespace SensorReadings.Domain;

/// <summary>
/// Why a raw reading was rejected during ingestion. Used for the count report,
/// so we can tell exactly what kind of garbage the input contained.
/// </summary>
public enum RejectionReason
{
    /// <summary>The line was not a valid JSON object at all (e.g. truncated).</summary>
    MalformedLine,

    /// <summary>deviceId was missing, null or empty.</summary>
    MissingDeviceId,

    /// <summary>metric was missing, null or empty.</summary>
    MissingMetric,

    /// <summary>ts was missing, not ISO-8601 UTC, or not a real calendar date (e.g. June 31).</summary>
    InvalidTimestamp,

    /// <summary>value was missing, null, or not a number (e.g. the string "NaN").</summary>
    MissingOrNonNumericValue,

    /// <summary>value was numeric but clearly a sentinel/error marker (e.g. -9999 or 1000000).</summary>
    ImplausibleValue,

    /// <summary>seq was missing, null or negative.</summary>
    MissingSeq,
}
