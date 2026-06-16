---
id: FEATURE-025
created: 2026-05-31
owner: human
status: idea
---

# Python SDK

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Data scientists and scripting developers want to drive DraCode the way they drive any other service — from a notebook or a Python script — instead of clicking through a UI. Without a proper client library they'd have to hand-roll API calls, which is tedious and error-prone.

## Proposed shape

A typed Python package, published to PyPI as `koboldlair`, that wraps the KoboldLair API. It offers both a synchronous and an asynchronous client, lets you create projects, wait for analysis, list tasks, run them, and stream live events as a run progresses. It can connect to a configured server with a token, or auto-discover a local background service the same way the CLI does. The data models mirror the server's published API contract and are kept in sync automatically, with the package version tracking the server API version so users know what they're targeting.

## Out of scope (initial)

- Wrapping anything not exposed by the server's public API contract
- Hand-maintaining models that diverge from the generated ones

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
