---
id: TASK-076
parent: null
feature: null
# status: todo | in-progress | review (code done, sign-off pending) | blocked | done | cancelled
status: review
priority: P2
assignee: ai
created: 2026-09-19
depends-on: []
blocks: []
related: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Birko-owned package versions were below the framework, and one pin was holding a High advisory open

## Context

Reported by `audit-consumer-versions.ps1` in Birko.Framework (its TASK-473), enforcing a rule settled
2026-09-19: **a consumer must never declare a package version lower than the framework declares.
Higher is allowed and the consumer owns the breakage; equal means do not declare it at all.**

It needs a script because NuGet cannot see it. "Consumer older than its dependency" is `NU1605`, an
error by default — but that compares across a *package dependency edge*, and a Birko `.projitems` is
compiled into our own assembly with no package identity. Its declaration and ours are two items in one
project file: `NU1504`, a warning about duplication, silent on which is older.

## The three declarations

All three were duplicates of a `.projitems` this repo already imports, so all three are now comments:

| Project | Was | Owner |
|---|---|---|
| `DraCode.Birko` | `Microsoft.Data.Sqlite` **9.0.4** vs framework `10.*` | `Birko.Data.SQL.SqLite` |
| `DraCode.Birko` | `System.IdentityModel.Tokens.Jwt` **8.7.0** exact, vs `8.*` | `Birko.Security.Jwt` |
| `DraCode.KoboldLair.Server` | `Microsoft.AspNetCore.Authentication.JwtBearer` **10.0.0** exact, vs `10.*` | `Birko.Security.AspNetCore` |

The JwtBearer comment said the package *"is NOT in the ASP.NET shared framework — required by the
source-compiled Birko.Security.AspNetCore"*. Still true; what changed is **who declares it**. The
`.projitems` now does, and it is imported here, so ours was a second copy.

The two exact pins were not violations — the floors matched — but they froze what the framework
deliberately floats, which loses the advisory self-healing that float exists for.

## ⚠ The interesting part: raising DraCode.Birko broke three other projects

After the first edit, restore failed with **`NU1605` in `DraCode.KoboldLair`, `.Server` and `.Tests`**:

```
Detected package downgrade: Microsoft.Data.Sqlite from 10.0 to 9.0.4
  DraCode.KoboldLair -> DraCode.Birko -> Microsoft.Data.Sqlite (>= 10.0.0)
  DraCode.KoboldLair -> Microsoft.Data.Sqlite (>= 9.0.4)
```

`DraCode.KoboldLair` declares its own `Microsoft.Data.Sqlite 9.0.4` and imports no Birko sources — it
reaches the package through a **`ProjectReference`** to `DraCode.Birko`. That *is* a package
dependency edge, so NuGet sees it and fails the build, which is precisely what it cannot do for the
`.projitems` case. The pin had been latent for as long as `DraCode.Birko` agreed with it.

**This is the audit's documented blind spot arriving in practice** — it checks only projects that both
import a `.projitems` and declare the package, on the stated grounds that a `ProjectReference` sibling
is NuGet's job. It was, and NuGet did it.

Raised to `10.*` rather than deleted. No `.cs` in `DraCode.KoboldLair` references the package, but a
SQLite provider's native assets are not something a grep for type names can see, so the explicit
reference stays and merely stops being a downgrade.

## ⚠ The SQLite pin was holding a High advisory open

Birko's TASK-230 set the floor at `Microsoft.Data.Sqlite >= 9.0.19 or >= 10.0.11` to clear a **High**
`SQLitePCLRaw.lib.e_sqlite3` 2.1.10, and recorded *"DraCode ×5"* among the projects still carrying it.
**9.0.4 is below both thresholds.** After this change `10.*` resolves **10.0.12 → SQLitePCLRaw
2.1.12**, and all five of those rows are gone.

## Verification

- `dotnet restore` (whole solution) — exit 0, no `NU1504`, no `NU1605`.
- `Microsoft.Data.Sqlite → 10.0.12`, `System.IdentityModel.Tokens.Jwt → 8.23.0`, `SQLitePCLRaw 2.1.12`.
- `dotnet list package --vulnerable --include-transitive`, solution-wide: `DraCode.Birko`,
  `DraCode.KoboldLair`, `.Client`, `.ServiceDefaults` and `DraCode` all report **none**.
- Framework audit re-run: DraCode's three findings gone.

## Out of scope

- **`Microsoft.OpenApi` 2.0.0 (High) in `.Server` and `.Tests`, `MessagePack` 2.5.192 (High) in
  `.AppHost`.** Both pre-existing, both already recorded by Birko's TASK-230 as consumer-owned rows,
  neither touched or affected by this change. They want their own id — the OpenAPI one especially,
  since it is a direct declaration rather than an Aspire transitive.
- **Whether `DraCode.KoboldLair` needs its `Microsoft.Data.Sqlite` reference at all.** It is unused by
  every `.cs` in that project. Deleting it is probably right and is deliberately not bundled with a
  fix whose point was to stop the build failing.

## Human test plan

N/A — restore and the resolved-version listing are mechanical.

## Implementation plan

_Populated by `/tasks plan TASK-076` — leave empty until then._
