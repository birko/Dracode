---
id: TASK-104
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
findings: [FIELD-018]
pr: null
github-issue: null
jira-key: null
---

# The API test host loads the developer's appsettings.local.json, so tests depend on the machine they run on

## Context

Found 2026-10-07 during TASK-093. A `ResourceEndpointsTests` test that built a Wyvern through the
`WebApplicationFactory<Program>` host failed with `Provider 'pi-zai' not found for agent type 'wyvern'`. `Program.cs:47`
adds `appsettings.local.json` (git-ignored, `.gitignore:82`) unconditionally, and on this machine it sets
`KoboldLair:DefaultProvider: pi-zai`. The test host points `ProjectsPath` and the database at a temp folder, so the
provider list is empty while the default provider still comes from the developer's local file. Today's API tests pass
only because none of them resolves a provider; any test that does gets a result that depends on whose machine runs it.
CLAUDE.md § Testing requires tests to run against a throwaway host, never the developer's or shared settings.

## Acceptance criteria

- [x] The test host does not read `appsettings.local.json` — e.g. `Program.cs` skips it in the `Testing` environment, or
      the test harness removes that source — and the choice is stated where the file is added — `Program.cs` skips the file in the `Testing` environment, with the reason beside it
- [x] A test proves the test host's `KoboldLair:DefaultProvider` is not taken from `appsettings.local.json` (fails while
      a local file with a different value is present and loaded) — `The_test_host_does_not_read_the_developers_appsettings_local_json` asserts no configuration source is that file, so it fails whether or not the file exists on the machine (proven: fails with the load restored)
- [x] A REST test can define its own provider and build a Wyvern; add the one TASK-093 dropped: `DELETE /api/v1/projects/{id}` releases the project's Wyvern — `Delete_project_releases_its_Wyvern` (its own `test-ollama` provider via in-memory config; it passes with or without the fix, since its provider overrides the local default — the config test above is the guard)

## Out of scope

- What REST delete removes besides agents (task rows, files) — TASK-074

## Human test plan

N/A — configuration loading and the REST delete are covered by automated tests.

## Implementation plan

Done 2026-10-07.

1. `Program.cs`: add `appsettings.local.json` only outside the `Testing` environment.
2. `ResourceEndpointsTests`: `CreateFactory` takes extra config; one test asserts the configuration sources, one restores
   TASK-093's REST delete test with the test's own provider.

## Close notes

- Closed 2026-10-07. Suite 241/241. A server started with `ASPNETCORE_ENVIRONMENT=Testing` (the UI E2E profile) no longer
  reads the local file either — intended: that environment is the isolated test host (`appsettings.Testing.json`).
- The legacy provider mode also reads `./user-settings.json` from the process working directory; under the test runner
  that is the test output folder, which holds none, so it does not leak today.
