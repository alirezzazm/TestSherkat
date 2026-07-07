namespace SensorReadings.Domain;

/// <summary>
/// Duplicate policy: the first occurrence of a <see cref="ReadingKey"/> wins and
/// every later occurrence is dropped, even when it carries a different value.
/// We remember the accepted value so that conflicting duplicates (same key,
/// different value) can be reported to the caller and logged.
/// </summary>
public sealed class ReadingDeduplicator
{
    private readonly Dictionary<ReadingKey, double> _acceptedValues = [];

    /// <summary>
    /// Returns true when the reading is new and should be stored.
    /// Returns false when a reading with the same key was already accepted;
    /// <paramref name="conflictsWithAccepted"/> then tells whether the new
    /// reading carried a different value than the one we kept.
    /// </summary>
    public bool TryAccept(SensorReading reading, out bool conflictsWithAccepted)
    {
        if (_acceptedValues.TryGetValue(reading.Key, out var acceptedValue))
        {
            conflictsWithAccepted = acceptedValue != reading.Value;
            return false;
        }

        _acceptedValues.Add(reading.Key, reading.Value);
        conflictsWithAccepted = false;
        return true;
    }
}
