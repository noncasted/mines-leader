namespace Common.Extensions;

// A trace source a host registered (AddTraceSource), kept in its container: the tracer provider of a monolith process
// is not built from the container of a host and reads the names from here.
public record TraceSourceRegistration(string Name);
