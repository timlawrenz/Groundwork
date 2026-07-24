# Groundwork

A cross-platform, moddable, touch-first city-builder with a pure simulation core and no combat.

**Spiritual successor to Banished** — peaceful cooperative economy, deep production chains, individual citizen simulation.

[![Status: Scaffolding](https://img.shields.io/badge/status-scaffolding-yellow)](#project-status)

## Design Pillars

- **Architecture-first** — simulation engine ships before any UI
- **Moddable from day 1** — content in JSON, behaviors in Lua, mod API == internal API
- **Cross-platform** — Android, Windows, Linux, WebGL from one codebase
- **Touch-first UX** — designed for fingers on glass, keyboard+mouse as secondary
- **No warfare, no enemies** — all depth from economy, seasons, citizen well-being, trade

## Tech Stack

| Layer | Technology |
|---|---|
| Engine | Unity ECS (DOTS) |
| Content | JSON definitions |
| Scripting | Lua (MoonSharp / NLua) |
| Build | Unity, one target toggle per platform |

## Project Status

See [`PROJECT_STATUS.md`](PROJECT_STATUS.md) for the current phase and next action.

See [`TODO.md`](TODO.md) for the task board.

## Quick Start

*Unity project not yet scaffolded. This section will be updated when Step 3 is complete.*

## Docs

- [`docs/design.md`](docs/design.md) — Game design: entities, simulation loop, MVP scope
- [`docs/architecture.md`](docs/architecture.md) — System boundaries, ECS design, command bus
- [`docs/decisions.md`](docs/decisions.md) — Architecture Decision Records
- [`docs/unity-features.md`](docs/unity-features.md) — Unity built-in feature catalog: what to USE vs EXTEND vs BUILD

## License

TBD — likely GPLv3 or MIT.
