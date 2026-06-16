---
id: FEATURE-003
created: 2026-05-31
owner: human
status: idea
---

# Enterprise features (team collaboration, audit, CI/CD)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

DraCode today assumes a single user on a single machine. Enterprises can't adopt it: there's no way for a team to share work safely, no record of who did what for compliance reviews, and no repeatable way to ship the system as a deployable service. Regulated, team-based organizations need all three before they can trust it.

## Proposed shape

Add multi-user team workspaces where people are grouped into a team with roles — admins manage members, regular users get work done, and viewers can look but not touch. Teams are walled off from each other so one team never sees another's data. Alongside this, every meaningful action (both human commands and automated agent steps) is written to a tamper-resistant audit trail that can be exported for compliance reporting. Finally, the system gains automated build-and-publish pipelines so it can be packaged as containers and deployed consistently.

## Out of scope (initial)

- SSO / SAML at the team level (separate concern)
- Multi-team membership — one team per user in v1
- Real-time SIEM integration for the audit log (possible follow-up)
- Anomaly detection on the audit log (separate analytics concern)
- Kubernetes manifests (follow-up if demand appears)
- Multi-architecture container images (linux/arm64 — future)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
