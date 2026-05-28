---
id: EPIC-002
status: planned
created: 2026-05-28
owner: ai
---

# Plugin & extensibility

## Area of concern

External plugin loading so third parties can ship custom tools without forking DraCode. Assembly loading + tool marketplace concept.

## Success criteria

- Plugin discovery from a `plugins/` folder at runtime
- Each plugin can register one or more tools
- Sandboxing where practical (separate AppDomain or AssemblyLoadContext)
- Tool marketplace metadata format defined (open to consider later — packaging only in v1)
