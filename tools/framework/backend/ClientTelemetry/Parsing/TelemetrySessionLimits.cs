namespace Game.ClientTelemetry;

// What only the known list of names of the session can tell, so it runs in the grain, after the parser.
public static class TelemetrySessionLimits
{
    public static bool FitsNames(TelemetryBatch batch, int knownNames)
    {
        return knownNames + batch.NameSlots <= ClientTelemetryLimits.MaxNamesPerSession;
    }

    public static bool FitsValues(TelemetryBatch batch, int names)
    {
        return batch.Samples.All(sample => sample.Values.Count <= names);
    }
}
