---
id: FEATURE-018
created: 2026-05-31
owner: human
status: idea
---

# REST + SSE facade for non-streaming clients

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today every integration has to hold open a live streaming connection to interact with KoboldLair. That suits the interactive browser surfaces, but it is awkward for a Discord bot, a CI script, or a quick command-line call that just wants to make one request and move on. Those callers need plain HTTP.

## Proposed shape

Add a versioned HTTP interface that covers everything the streaming surfaces don't need to: listing and creating projects, managing specifications and features, inspecting tasks and plans, starting runs, reading cost reports, and seeing which agents are active. For progress on a running job, callers can subscribe to a lightweight server-sent stream instead of opening a full live connection.

The interface ships with an auto-generated, browsable API description so integrators can discover it. It is a thin layer over the existing services — one source of truth, two ways to reach it — and it reuses the same internal event feed the streaming surfaces already publish.

## Out of scope (initial)

- The interactive streaming surfaces themselves (those stay as live connections)
- Authentication, which is layered on top by FEATURE-019
- A maintained client SDK for any specific language

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
