---
id: FEATURE-006
created: 2026-05-31
owner: human
status: idea
---

# Advanced ML research (RAG + fine-tuned models)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

On larger codebases, automated workers can struggle to find and use the right context, which hurts the quality of what they produce. There may be better ways to feed them relevant code and to tune the underlying models to a project's conventions — but these are unproven ideas that need investigation before any commitment.

## Proposed shape

Two speculative research tracks, each taken only as far as a proof-of-concept with a written ship / pivot / drop conclusion. The first explores retrieval-augmented generation: automatically indexing a project's code and injecting the most relevant snippets into a worker's context instead of relying on name-based search, then measuring whether completion quality improves. The second explores fine-tuning a smaller open-weight model on a specific project's code and style, then benchmarking it for latency and convention adherence against generic models.

## Out of scope (initial)

- Production hardening — both tracks are spikes
- Continuous re-indexing on every file change (poll-based initially)
- Hosting and serving infrastructure for fine-tuned models (assume local in v1)
- Multi-project or shared fine-tunes

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
