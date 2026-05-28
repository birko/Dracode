---
id: TASK-003
parent: EPIC-002
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Plugin system for custom tools

## Context

External plugin loading from `plugins/` folder. Each plugin can register one or more `Tool` subclasses. AssemblyLoadContext for unloadability.

## Acceptance criteria

- [ ] `IPlugin` interface with `Register(IPluginRegistry registry)` method
- [ ] Plugin discovery scans `plugins/*.dll` at startup
- [ ] Each plugin loaded in its own `AssemblyLoadContext` (unloadable)
- [ ] Registered tools appear in the standard tool catalogue, with `Plugin:` prefix on name
- [ ] Plugin metadata file (`plugin.json`) declares: name, version, author, required permissions
- [ ] Disable plugin via config without removing file
- [ ] Unit tests with a sample plugin assembly
- [ ] Sample plugin in `samples/HelloWorldPlugin/` for documentation

## Out of scope

- Sandboxing (resource limits, syscall whitelisting) — separate trust-boundary concern
- Plugin marketplace / discovery service (future)

## Implementation plan

_Populated by `/tasks plan TASK-003` — leave empty until then._
