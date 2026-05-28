---
id: TASK-008
parent: STORY-001
status: todo
priority: P1
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# CI/CD integration (GitHub Actions + Docker)

## Context

Build + test + publish pipelines so DraCode can be deployed as a containerized service.

## Acceptance criteria

- [ ] `.github/workflows/ci.yml` — build + run all tests on push to any branch
- [ ] `.github/workflows/release.yml` — on tag push: build Docker images for AppHost + KoboldLair.Server + KoboldLair.Client, push to registry (GHCR or Docker Hub — TBD)
- [ ] `Dockerfile.apphost` + `Dockerfile.koboldlair-server` + `Dockerfile.koboldlair-client`
- [ ] `docker-compose.yml` for local + dev environment
- [ ] CI matrix: Windows + Linux build (cross-platform validation)
- [ ] CI badge added to README

## Out of scope

- Kubernetes manifests (could be follow-up if demand appears)
- Multi-arch Docker images (linux/arm64 — future)

## Implementation plan

_Populated by `/tasks plan TASK-008` — leave empty until then._
