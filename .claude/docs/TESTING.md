# Testing — How to Run Tests & Read Their Logs

**TL;DR:** Backend tests use xUnit v3 on Microsoft Testing Platform. Logs are written in UTF-16LE — convert them via `tools/scripts/get-test-log.sh` before reading with Read tool.

---

## Test Project Layout

Backend tests live in `backend/Tools/Tests/`:

```text
backend/Tools/Tests/
├── Tests.csproj           # test project (Microsoft Testing Platform runner)
├── xunit.runner.json      # single-threaded runner config
├── Fixtures/              # Orleans test cluster fixtures
├── Execution/             # test execution helpers
├── Game/                  # game-system tests
├── Meta/                  # meta-system tests
├── Grains/                # grain-level tests
├── Messaging/             # messaging tests
└── State/                 # state tests
```

Benchmarks are in `backend/Tools/Benchmarks/` (separate project).

---

## Tests.csproj — required properties

For `dotnet test` to write log files Claude can read, the project must declare:

```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
</PropertyGroup>

<ItemGroup>
    <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest"/>
</ItemGroup>
```

Without `UseMicrosoftTestingPlatformRunner`, logs are NOT created and Claude cannot read failures.

### xunit.runner.json

Single-threaded execution keeps Orleans test cluster deterministic:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "diagnosticMessages": false,
  "maxParallelThreads": 1,
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false
}
```

---

## Running Tests

```bash
# All tests (background recommended — tests are slow):
dotnet test backend/Tools/Tests/Tests.csproj

# One class (xUnit v3 syntax — note the `--` separator):
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*BoardTests"

# One method:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-method "*BoardTests.RevealMine_ReturnsLoss"

# By namespace:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-namespace "Tests.Game.Board"

# Exclude class:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-not-class "*SlowTests"
```

xUnit v3 DROPPED the old `--filter "FullyQualifiedName~..."` syntax — it is silently ignored.

---

## Running everything locally

GitHub CI (`.github/workflows/ci.yml`) builds and tests only the backend, on pushes and pull requests that touch
`backend/`, `shared/` or the framework pointer. The container generator and the Unity EditMode tests run only
locally (Unity stays out of CI by the decision of the user). The full local run is one script, and `/deploy-client`
runs it before every WebGL build (a failure stops the deploy, `skip-tests` skips it only when the user passes it):

```bash
tools/scripts/run-tests.sh                  # format + backend + generator + Unity EditMode + client build
tools/scripts/run-tests.sh --skip-unity     # format + backend + generator + client build (fast)
tools/scripts/run-tests.sh --skip-backend   # format + generator + Unity + client build
```

The code gate (formatting, the client build with the analyzers, the FWR errors of the backend build) runs under every
option; `--skip-backend` drops the backend build and tests, so its FWR column of the report is 0.

What it does:

1. **Snapshot** of the working tree into `/var/tmp/mines-leader-tests.XXXXXX/repo` with `cp -a --reflink=always`
   (btrfs: `client/Library` included, seconds and ~0 disk). Skipped: `client/Temp` (lock file of the open
   editor), `Logs`, `build`/`Build*`, `MemoryCaptures`, `ProfilerCaptures`, `traces`, `obj`, `backend/.telemetry`
   and the player build caches of `client/Library` (`BuildCache`, `BuildHistory`, `BuildPlayerData`,
   `PlayerDataCache`, `com.unity.web.stripping-tool`, `ManagedCapture`).
   `/tmp` is tmpfs - reflink impossible there, hence `/var/tmp` (`TEST_COPY_DIR` overrides; must be the same
   btrfs). `backend/` is copied under `flock /tmp/claude-1000/mines-leader-backend-build.lock`. Deleted on exit
   (trap; `KEEP_COPY=1` keeps it).
2. **format** - `tools/scripts/check-format.sh` on the snapshot (see "Code gate" below).
3. **backend** - `dotnet build backend/backend.slnx --no-incremental` + `backend/Tools/Tests/bin/Debug/net10.0/Tests`,
   in the snapshot: the bins of a running Aspire cluster are not overwritten. Needs Docker (Testcontainers). The build
   fails on an FWR rule with severity error; its FWR warnings are counted (`--no-incremental`: a skipped compile would
   print none).
4. **generator** - `ContainerGenerator` Release build + `ContainerGenerator.Verify`.
5. **unity** - `/tools/unity/<ProjectVersion>/Editor/Unity -batchmode -nographics -projectPath <snapshot>/client
   -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`: every EditMode test assembly of the
   project. The NUnit XML is parsed (total / passed / failed / skipped, failed test names with the first line
   of the message). Guards: refuses with less than 8 GB `MemAvailable` (`UNITY_MIN_FREE_GB`), one batchmode
   Unity at a time (`flock /tmp/claude-1000/unity-batchmode.lock`), killed after `UNITY_TIMEOUT` (3600 s).
   `UNITY_TEST_FILTER` is passed as `-testFilter`, `UNITY_EDITOR` overrides the binary.
6. **client** - `dotnet build client/client.sln --no-incremental` on the copied client, after the Unity run (its `obj`
   and `Temp/Bin` stay out of the tree Unity tests). Needs the Unity-generated `client/*.csproj` and `client.sln`
   (gitignored, the open editor writes them; without them the step fails): their absolute paths of the source tree are
   rewritten to the snapshot. Fails on compile errors, broken registrations (`CINGR*`) and FWR errors; counts the FWR
   warnings. Without Unity (`--skip-unity`) the snapshot still holds the client and `Library/PackageCache` +
   `Library/ScriptAssemblies` that the csproj reference.

A failing step does not stop the next one. The end is a summary table (step, PASS/FAIL/SKIP, seconds,
totals), `Logs: <dir>` (default `/tmp/mines-leader-tests/<time>`, `TEST_LOG_DIR` overrides: build/test logs,
`unity.log`, `unity-results.xml`, converted backend `TestResults`), `Report: <dir>/report.md` and
`ALL TESTS PASSED` / `TESTS FAILED`; exit code 1 on any failure. `report.md` is the short report of the run: the steps
with result and seconds, the Unity tests per assembly (total, passed, failed, skipped, seconds) and the failed tests;
a run of `/deploy-client` leaves it in `<its logs>/tests/report.md`. The report also holds the table of the FWR warnings
per rule: unique findings (`file(line,col): rule`) of `client.sln` and `backend.slnx`, each split into the code of the
game (`client/Assets`, `shared`, `backend`) and `tools/framework`.

### Code gate: formatting and analyzers

- **Formatting** - `tools/scripts/check-format.sh`: `dotnet format whitespace --folder --verify-no-changes` over
  `client/Assets` (without `Plugins`), `shared`, `backend` against `.editorconfig` / `client/.editorconfig`: braces on
  a new line, indents, spaces, `charset = utf-8` (no BOM). Not checked: `tools/framework` (formatted in its own
  repository; `--framework` only counts its findings), `client/Packages`, `bin`/`obj`, generated code (`*.g.cs`, an
  `<auto-generated/>` header). Folder mode: the same files as through the solutions, without the Unity csproj and in
  seconds. Only whitespace: `dotnet format style` would change code. `insert_final_newline` is not set in the
  `.editorconfig` files: `false` makes `dotnet format` delete the last newline (Rider reads it as "do not add"), so
  the last newline is free. Exit 1 with a short list on violations; `--fix` formats in place, `--backend` checks only
  `backend` + `shared` (the CI scope).
- **Analyzers** - Framework.Analyzers (`FWR001`-`FWR006`, `tools/framework/client/core/Source~/Analyzers/README.md`)
  run in every compile of the client and the backend. Severities: `.editorconfig` (backend, shared, framework sources)
  and `client/.editorconfig` (client, `root = true`), now `FWR004`, `FWR005` = error, `FWR001`, `FWR002`, `FWR003`,
  `FWR006` = warning. They act only in `dotnet build`: the Unity compile reads no `.editorconfig` and shows every FWR
  rule as a warning in the console, an error rule does not stop the editor - the gate is `run-tests.sh` and CI.
- **Raising a rule to error**: when the warnings of a rule in the code of the game reach 0 in the report (both
  builds), set `dotnet_diagnostic.FWRxxx.severity = error` in both `.editorconfig` and `client/.editorconfig`, run
  `tools/scripts/run-tests.sh` and commit the two files. Findings left only in `tools/framework` block it too: the
  severity covers the framework sources compiled by the game, so they are fixed in the framework first.
- **Where it runs**: `run-tests.sh` (every option, so `/deploy-client` too), the CI of the backend
  (`check-format.sh --backend`, then `dotnet build` fails on FWR errors; the dll comes from the submodule), the skill
  `validate` (the findings of the changed files).

Client gameplay tests: the EditMode assembly `GamePlay.Tests` (`client/Assets/GamePlay/Tests`) checks the board
geometry, the card shapes, the board records and the queue of `SnapshotReceiver` on fakes of `IBoard` / `IBoardCell`
(no MonoBehaviour); `GamePlay.Metrics.Tests` checks the match metrics writers.

---

## Reading Test Logs (xUnit v3 UTF-16LE workflow)

xUnit v3 emits UTF-16LE logs into `**/TestResults/*.log`. The Read tool cannot parse UTF-16LE. Use this workflow:

```bash
# 1. After tests finish, convert every UTF-16LE log into UTF-8:
tools/scripts/get-test-log.sh

# 2. Find converted logs:
# Glob: **/TestResults/*.utf8.log

# 3. Open with Read tool:
# Read ./backend/Tools/Tests/bin/Debug/net10.0/TestResults/*.utf8.log
```

| File | Encoding | Readable by Claude |
|------|----------|--------------------|
| `TestResults/*.log` | UTF-16LE | no (original) |
| `TestResults/*.utf8.log` | UTF-8 | yes (converted) |

Useful bash searches on converted logs:

```bash
grep "^failed" TestResults/*.utf8.log
grep -A 10 "^failed" TestResults/*.utf8.log     # 10 lines of error detail
file -bi TestResults/*.utf8.log                 # verify encoding
```

---

## Capturing Output Inside a Test

Use `ITestOutputHelper` to emit debugging data that ends up in the log file:

```csharp
public class BoardTests
{
    private readonly ITestOutputHelper _output;

    public BoardTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RevealMine_Loses()
    {
        _output.WriteLine($"board state: {board}");
        // ...
    }
}
```

---

## Debugging Hanging / Timing-Out Tests

1. If a test hangs — the problem is in code (deadlock, infinite loop, bad SQL), NOT in the test framework. Do not increase timeouts — find the root cause.
2. Never delete a failing test to "make it pass". Find the root cause.
3. Never use `Task.Delay` as a synchronization primitive — use proper waits / pumps.
4. Check Orleans application logs in `/tmp/orleans-test-*.log` (already UTF-8) if tests time out.

---

## Reference

- `backend/Tools/Tests/Tests.csproj` — canonical test project layout
- `tools/scripts/get-test-log.sh` — UTF-16LE → UTF-8 conversion
- `.claude/docs/ERRORS.md` — error lookup table (includes test-log errors)
