---
id: STORY-022
parent: EPIC-013
status: planned
created: 2026-05-28
---

# Python SDK

## User story

As a data scientist or scripting developer, I want a typed Python client so I can drive KoboldLair from notebooks and scripts the way I drive any other API.

## Behaviour

- New repo (or monorepo subfolder) `koboldlair-python/` — separate from .NET solution
- Package name on PyPI: `koboldlair`

### Surface

```python
from koboldlair import Client

client = Client(server="http://localhost:17091", token="...")
# or: client = Client.from_local_daemon()

proj = client.projects.create(name="my-app", specification="...")
proj.wait_for_status("Analyzed", timeout=600)
for task in proj.tasks:
    run = task.run()
    async for event in run.stream():
        print(event)
```

- Sync + async APIs (`Client` + `AsyncClient`)
- Async streaming via `httpx-sse`
- Pydantic models matching the OpenAPI spec from STORY-016 (auto-generated where possible)
- `from_local_daemon()` reads `~/.koboldlair/daemon.json` like the CLI does

## Distribution

- Publish to PyPI via GitHub Actions workflow
- Versioning matches the server API version (`koboldlair==1.x.y` targets `/api/v1`)

## Risks

- OpenAPI → Pydantic codegen drift: pin a generator (`datamodel-code-generator`) and re-run on each server release; track schema-hash in the SDK to fail fast on mismatch
- Async streaming over SSE behind corporate proxies is fragile — provide a sync-polling fallback
