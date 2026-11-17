namespace Game.ClientTelemetry;

public static class ClientTelemetryLimits
{
    public const int MaxBatchBytes = 64 * 1024;
    public const int MaxNamesPerSession = 200;
    public const int MaxTagsPerSession = 8;
    public const int MaxSamplesPerBatch = 300;
    public const int MaxErrorsPerBatch = 32;
    public const int MaxMarksPerBatch = 128;
    public const int MaxMessageLength = 2 * 1024;
    public const int MaxStackLength = 8 * 1024;
    public const int MaxNameLength = 64;
    public const int MaxUnitLength = 16;

    // The detail of a mark is a key of the code (the group or the scene of a load), cut like a name of a startup span.
    public const int MaxMarkDetailLength = 128;

    // The last marks a client keeps of a session that did not close, and the times of it: hours at most, a larger one is garbage.
    public const int MaxPreviousMarks = 8;
    public const long MaxPreviousMs = 7 * 24 * 3600 * 1000L;

    // The last samples of it with the main numbers of a frame: the client sends 5 of them by about 7 names.
    public const int MaxPreviousSamples = 8;
    public const int MaxPreviousSampleNames = 16;

    // A start of a client is about 365 spans with names to 44 characters, about 30 KB on the wire. The spans at names of 20 characters
    // are about 250 KB, so the two limits meet; what they bound is one row of a record, kept for the retention days.
    public const int MaxStartupSpans = 4096;
    public const int MaxStartupNameLength = 128;
    public const int MaxStartupBytes = 256 * 1024;

    // A file of the page: the last segment of its address, as a build names it ("Build.wasm.unityweb", "telegram-web-app.js").
    public const int MaxStartupFileLength = 64;

    // A start has hours at most on the scale of its trace: a larger time is garbage.
    public const double MaxStartupMs = 7 * 24 * 3600 * 1000d;

    // A socket makes a record per trace of its client and two writes of each (the snapshot, the final one), with room for several traces.
    public const int MaxStartupsPerSocket = 8;
    public const int MaxStartupWritesPerSocket = 16;
}
