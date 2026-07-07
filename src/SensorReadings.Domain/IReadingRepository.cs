namespace SensorReadings.Domain;

/// <summary>
/// Storage for cleaned readings. Deliberately small: the domain only ever needs
/// to append validated readings and read back one device/metric series in a
/// time range. Deduplication is a business rule and happens before this point,
/// so the repository stays a dumb store.
/// </summary>
public interface IReadingRepository
{
    void Add(SensorReading reading);

    /// <summary>
    /// Returns the readings of one device/metric whose timestamp falls in
    /// [fromInclusive, toExclusive). No ordering is guaranteed.
    /// </summary>
    IReadOnlyList<SensorReading> GetRange(
        string deviceId,
        string metric,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive);
}
