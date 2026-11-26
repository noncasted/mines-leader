# Client telemetry test host

`tools/scripts/test-telemetry-transport.sh` runs apart from the suite: the real client of `modules/telemetry` in PlayMode over a real
websocket against `backend/ClientTelemetry.TestHost` (Kestrel, a silo, its own PostgreSQL container, the real switch), started and
stopped by the script. Without the host the tests `TelemetryTransportEndToEndTests` are ignored, so `test-all.sh` counts them as
skipped; their union over `INetworkContext` is dynamic and registered only with the host, for their namespace, so without it they change nothing for `network` or for the union of a game. The host alone, with sessions to read (the sockets `/test-host/meta`, and `/test-host/mute` that answers nothing, without a token; `/test-host/flag/{bool}` and `/test-host/stats` next to the API under the
token): `.build/bin/ClientTelemetry.TestHost/Debug/net10.0/Framework.ClientTelemetry.TestHost --token <t> --port <n> --seed`; a session open on a quiet socket closes as `lost` after `--lost-after <seconds>`, 15 here. `--startup-file <path>` (with `--seed`) sends a file of the Startup Profiler as the startup trace of the seeded sessions.
