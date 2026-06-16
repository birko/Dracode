---
id: FEATURE-002
created: 2026-05-31
owner: human
status: idea
---

# Plugin system for custom tools

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Right now, the only way for a third party to add their own custom tool to DraCode is to fork the whole codebase and rebuild it. That's a big barrier — it locks out community and partner extensions and makes every add-on a maintenance headache.

## Proposed shape

Let DraCode load plugins from a dedicated `plugins/` folder when it starts. Each plugin can register one or more new tools, which then show up in the normal tool list (clearly marked as coming from a plugin). Every plugin carries a small description file declaring its name, version, author, and what permissions it needs, and each plugin is loaded in isolation so it can be added or removed cleanly. Operators can switch a plugin off through configuration without deleting its files. A small sample plugin ships as a worked example.

## Out of scope (initial)

- Strict sandboxing (resource limits, system-call whitelisting)
- A public plugin marketplace or discovery service

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
