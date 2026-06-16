---
id: FEATURE-064
created: 2026-01-31
owner: human
status: done
---

# New LLM providers (Z.AI, vLLM, SGLang)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The system could only talk to a fixed set of seven AI model vendors. Teams who wanted to use newer or self-hosted model services — Z.AI's GLM models, or locally run inference servers like vLLM and SGLang — had no supported way to plug them in. This limited cost control and model choice.

## Proposed shape

Add three new provider connectors so the system can reach Z.AI (GLM family models), vLLM, and SGLang. Because vLLM, SGLang, and Z.AI all speak the same common request format, introduce a single shared foundation they reuse, reducing duplicate work and making future providers cheaper to add. This grew the supported provider count from seven to ten-plus.

## Out of scope (initial)

- Building any custom model hosting infrastructure
- Provider-specific billing dashboards beyond standard usage tracking

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
