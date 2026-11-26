# Backend

`Cluster`, `Infrastructure` (telemetry defaults), `Console`, `Session`, `Hosting`, `ClientTelemetry` and `ClientTelemetry.TestHost` keep their contract in their own
`CLAUDE.md`; `MatchMetrics` in `.claude/docs/MATCH_METRICS.md` and `MatchMetrics/Contract/CONTRACT.md`.

## Persisted type names

States are stored with `$type`; `MovedTypesBinder` finds a type moved to another
assembly by its full name, so keep namespaces when moving persisted types between the framework and a project.
