---
name: Agent task
about: A feature, chore, spike or design task sized for one Claude Code session and one PR
title: ""
labels: []
assignees: []
---

## Context
Why this exists. Link to scope section (docs/scope.md#...) and parent epic.

## Goal
One sentence describing the observable outcome.

## Scope
**In:** bullet list
**Out:** explicit non-goals (prevents agent scope creep)

## Acceptance Criteria
- [ ] Testable statement (Given/When/Then where useful)
- [ ] ...

## Technical Notes
- Projects/files to touch, interfaces to add or implement
- Data model / migration changes
- Config keys introduced (name, dev source, prod env var)
- Relevant docs links (NetCord, Fluxer.Net, Aspire, MudBlazor)

## Platform Parity  (only if parity:needed)
| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Notes |
|---|---|---|---|
| ... | ✅ | ⚠️ / ❌ / ❓ | |

## Test Plan
Tests to write first (TDD), one or more per acceptance criterion, with layer
(unit / Testcontainers / bUnit / Aspire). Plus any manual check
(e.g. "run AppHost, send a message, row appears in message_events").

## Dependencies
Blocked by: #x · Blocks: #y

## Definition of Done
- [ ] Implemented test-first per docs/coding-guidelines.md
- [ ] Builds without warnings; all tests green; `dotnet format` clean
- [ ] PR maps each acceptance criterion to its tests
- [ ] No platform types outside adapter projects
- [ ] UI text German, everything else English
- [ ] docs/ updated if behaviour or config changed
