---
id: STORY-001
parent: EPIC-004
status: planned
created: 2026-05-28
---

# Enterprise features

## User story

As an enterprise adopter, I want multi-user team workspaces, audit logs, and CI/CD-ready containerized builds so DraCode is usable in regulated, team-based environments.

## Behaviour

- Team workspaces — multiple users with role-based access (admin / user / viewer matches existing Birko.Security.Authorization roles)
- Audit log — every agent action + user command captured for retrospective review
- CI/CD — GitHub Actions builds Docker images on every push; release pipeline tags + publishes
