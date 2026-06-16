---
id: FEATURE-022
created: 2026-05-31
owner: human
status: idea
---

# DraCode.KoboldLair.Cli — single-file binary CLI client

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today the only way to drive DraCode is through a web browser, and you have to manually start a server first. Developers who live in the terminal want to kick off an agent run, hold a planning conversation, or check status from any folder — instantly, without the browser detour and without babysitting a server process.

## Proposed shape

A single downloadable `koboldlair` program that runs on Windows, macOS, and Linux with no install ceremony. From any project folder a developer can type one command to: run an ad-hoc agent against the current code, open an interactive planning chat, analyze a request into a task breakdown, check what's running, or merge an agent's work back in. When no background service is running, the program quietly starts one for itself and cleans up afterward; for shared/remote setups it can sign in and connect to a hosted daemon. Interactive mode gets a friendly header (active agents, current project, tokens spent) and live streaming output; scripting commands stay clean and quiet.

## Out of scope (initial)

- Package-manager distribution (Chocolatey, Homebrew tap, Scoop) — follow-up after the first GitHub-release binaries
- Anything beyond the first-release `dotnet publish` artifacts

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
