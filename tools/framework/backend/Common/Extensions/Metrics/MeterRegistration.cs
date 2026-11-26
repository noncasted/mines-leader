namespace Common.Extensions;

// A meter a host registered (AddBackendMeter), kept in its container: the meter provider of a monolith process is not
// built from the container of a host and reads the names from here.
public record MeterRegistration(string Name);
