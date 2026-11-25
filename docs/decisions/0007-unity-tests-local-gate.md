# 0007. Unity tests: a local gate before every deploy instead of CI

Status: accepted.

## Problem

The client has several hundred EditMode tests and the container generator has its own suite. Running them on GitHub
Actions means a Unity editor on the runner:
- the project is on Unity 7000.0.0a10 ([0006](0006-unity-7000-alpha.md)), and GameCI publishes no editor image for it:
  on 2026-11-25 the Docker Hub repository `unityci/editor` had no tag matching `7000` (nor `6000.7`, the previous
  editor), while it has thousands for `6000.0`;
- a Unity licence has to be activated on the runner;
- a hosted runner starts from an empty `Library`, so every run pays a full import of the project on a small VM.

## Decision

- GitHub CI (`.github/workflows/ci.yml`) builds and tests the backend on every push and pull request that touches
  `backend/`, `shared/` or the framework pointer: the unit tests and the Orleans test cluster, Postgres from
  Testcontainers. The CI of the framework repository builds the container generator and runs its harness
  (`ContainerGenerator.Verify`).
- Everything else runs locally in one script, `tools/scripts/run-tests.sh`: the backend again, the container
  generator (`ContainerGenerator.Verify`) and every Unity EditMode assembly in batchmode. It works on a reflink snapshot
  of the working tree (`cp --reflink` on btrfs, `Library` included), so the open editor and a running cluster are not
  touched, and it writes `report.md` with the result per assembly.
- `/deploy-client` runs that script in parallel with the WebGL build and uploads only when every step passed: no
  client reaches players without the EditMode tests green.

## Numbers

Full local run of 2026-11-25 09:51 (`run-tests.sh`, AMD Ryzen 9 7900X, other agents working on the same machine):

| Step | Tests | Time |
|---|---:|---:|
| backend | 1,629 passed | 184 s |
| container generator | all passed | 25 s |
| Unity EditMode | 877: 830 passed, 47 skipped (opt-in benchmarks and explicit tests) | 171 s |
| **Total** | | **384 s** |

The backend job on GitHub took 3-4 minutes in the last five runs (2026-11-17 to 2026-11-25).

Not measured: a cold Unity import and test run on a GitHub runner; without an editor image it was not tried.

## Cost

- A pull request shows no Unity test result: a reviewer has to trust the report of the author or run the script.
- Nothing stops a commit to `main` with a red EditMode test; the gate is the deploy, not the merge.
- The gate depends on one machine with the editor, btrfs and Docker; the script refuses to start Unity with less than
  8 GB free memory and runs one batchmode Unity at a time.
