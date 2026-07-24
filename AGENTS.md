# AGENTS.md — Groundwork

Instructions for AI agents (Hermes, Claude, Codex, etc.) working on this project.

## Before doing anything

1. **Read [`PROJECT_STATUS.md`](PROJECT_STATUS.md)** — know the current phase, blockers, and the single next action. Do not start new work without understanding the project state.

2. **Read [`TODO.md`](TODO.md)** — know what's queued. Check whether your task is already listed, completed, or blocked.

3. **Check [`docs/decisions.md`](docs/decisions.md)** — before proposing a technology or architecture change, see if it's already been decided. Re-litigating settled decisions wastes time.

4. **Check [`docs/unity-features.md`](docs/unity-features.md)** — before building any system, verify Unity doesn't already ship it. The catalog tags every feature as USE, EXTEND, or BUILD.

## Governance

This project is in the **research and scaffolding** phase. Governance is lightweight:

- **Plans** — for any task requiring 3+ non-trivial steps, write a plan to `.hermes/plans/` using Hermes plan mode. Plans are committed to git.
- **ADRs** — architecture decisions go in [`docs/decisions.md`](docs/decisions.md). Each entry has: date, decision, rationale, status (proposed/accepted/superseded).
- **Tasks** — track near-term work in [`TODO.md`](TODO.md). Use checkboxes. Mark completed items with a date.
- **Design** — game design docs live in `docs/design.md`. Architecture docs in `docs/architecture.md`. These are living documents.

## Project-specific conventions

- **Unity ECS (DOTS)** — entities are archetypes, not GameObjects. Data-oriented, not object-oriented.
- **JSON for content** — items, buildings, recipes, maps. Human-readable, mod-friendly.
- **Lua for behavior scripting** — sandboxed, no filesystem/network/OS access.
- **Command pattern** — all mutations flow through the command bus. UI is decoupled from sim.
- **Deterministic simulation** — single-threaded, citizens tick in ID order. Reproducible and debuggable.
- **Stateless renderer** — renderer reads game state snapshot each frame. Sim never touches rendering.

## Key constraints (do not violate)

- **No combat, no enemies, no warfare.** Depth comes from economy, seasons, and citizen well-being. If a proposed feature introduces fighting, it's out of scope.
- **Mod API == internal API.** If the simulation engine can do it, a mod can do it. Never build a feature as internal-only if it could be a mod hook.
- **Architecture before UI.** The simulation engine must run headless and pass automated tests before any renderer exists.
