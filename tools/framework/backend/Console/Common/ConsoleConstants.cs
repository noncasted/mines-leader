namespace Console;

public static class ConsoleConstants
{
    public static class Pages
    {
        public const string Home = "/";
        public const string Login = "/login";
        public const string Infrastructure = "/infrastructure";
        public const string InfrastructureOptions = "/infrastructure-options";
        public const string Features = "/features";
        public const string SideEffects = "/side-effects";
        public const string StateExplorer = "/debug/state";
        public const string AuditLog = "/audit";
        public const string Discovery = "/discovery";
        public const string Deploy = "/deploy";
        public const string Monitoring = "/monitoring";
        public const string Online = "/online";
        public const string ClientTelemetry = "/client-telemetry";
        public const string ClientTelemetryErrors = "/client-telemetry/errors";
        public const string ClientTelemetryStartups = "/client-telemetry/startups";

        public static string ClientTelemetrySession(Guid id) => $"{ClientTelemetry}/sessions/{id}";
        public static string ClientTelemetryStartup(Guid id) => $"{ClientTelemetryStartups}/{id}";
        public static string ClientTelemetryErrorGroup(string fingerprint) => $"{ClientTelemetryErrors}/{fingerprint}";
    }
}