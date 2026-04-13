# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Turn-based tactical arena game where units battle on a 20x20 grid. Supports auto-battle (REST API) and real-time multiplayer (SignalR WebSockets). Monorepo with C#/.NET 8 backend, React 19 frontend, and a Python simulation script.

## Commands

### Frontend (Vite + React)
```bash
npm install              # Install dependencies
npm run dev              # Dev server at http://localhost:5173
npm run build            # Production build to dist/
npm run preview          # Preview the production build locally
npm run lint             # ESLint on .js/.jsx files
```

### Backend (.NET 8)
```bash
dotnet build                          # Build entire solution
dotnet run --project Arena.AI/        # Run API (http://localhost:5222)
dotnet watch run --project Arena.AI/  # Run with hot reload
```

### Python Simulation
```bash
python3 sim.py    # Run 1000 battle iterations against the backend API
```

### Development Setup
Run both frontend and backend simultaneously. The Vite dev server proxies `/api` requests to the backend at `http://localhost:5222`.

## Architecture

### Three-Layer Structure
- **Arena.AI/** — ASP.NET Core Web API. Controllers (`BattleCalculatorController`, `UnitStatsController`), SignalR hub (`ExternalPlayerHub` at `/play` endpoint), and DuckDB-backed persistence services under `Services/` and `QFolder/`.
- **Arena.AI.Core/** — Standalone class library with all game logic. No external dependencies. `Models/` holds the domain types (`Unit`, `BattleState`, `UnitType`, etc.); `Logic/` holds battle orchestration (`AutoBattleCalculator`, `MovementDecider`, `DamageCalculations`, `TeamGenerator`, `UnitFactory`) with a nested `BattleLogic/` subfolder; `QStorage/` defines Q-learning records and repository abstractions; `RealtimePlayers/` holds in-process bot implementations.
- **src/** — React SPA. Main game component is `src/components/Arena.jsx`. Uses Axios for REST, SignalR client for real-time, GSAP for sprite animations, Tailwind CSS for styling.

### Two Battle Modes
1. **Auto-Battle:** REST endpoints on `/BattleCalculator` — backend calculates all moves, returns full battle log.
2. **Real-Time:** SignalR hub. Players join via `Join()`, receive `PendingMovement` events (their turn), respond with `Act()` (move/attack/skip). `GameEnd` signals result.

### Key Game Concepts
- **Arena:** Fixed 20x20 grid, coordinates in chess notation (A1–T20) via `NumberLetterConverter`
- **Unit Types:** Light, Heavy, Fast, ShortRange, LongRange (enum `UnitType`)
- **Turn Order:** By movement speed descending (`MovementOrderManager`)
- **Combat:** Attack minus defense with counterattack mechanics (`DamageCalculations`)
- **Unit naming:** `{TeamName}_{UnitNumber}` (e.g., "TeamA_1")
- **Constants:** Max 8 units per team, defined in `Constants.cs`

### Battle State Flow
Setup → Waiting for Player Input → Action Applied → Check Win Condition → Loop or End

### Concurrency
`ActiveBattlesManager` uses `ConcurrentDictionary` to manage active battles and lobby state. Each `RealtimeBattle` instance handles one game session.

### Bots
In-process bot implementations live in `Arena.AI.Core/RealtimePlayers/` (e.g. `SimplePlayer1`) and are registered via `BotList.cs`, which is the entry point for adding a new bot usable in realtime battles.

### Q-Learning Infrastructure
- `Arena.AI.Core/QStorage/` — Q-learning domain model: `QRecord`, `QStateAction`, `QRecordManager`, and the `IQRepository` / `IQRecordsExtractor` abstractions.
- `Arena.AI/QFolder/` — DuckDB-backed implementation: `DuckDbRepository`, `QBattleResultBuffer`, and `QBattleResultsFlushService` buffer Q-records and flush them asynchronously. This mirrors the battle-result persistence pattern in `Services/`.

### API Conventions
- JSON property naming uses kebab-case (`battle-id`, `unit-type-A`)
- Swagger UI at `http://localhost:5222/swagger` in Development mode (or `https://localhost:7065/swagger` with `--launch-profile https`)
- SignalR client methods: `Joined`, `PendingMovement`, `GameEnd`
- SignalR server methods: `Join`, `Act`

### Persistence
Active battle and lobby state is in-memory via `ActiveBattlesManager`. Battle results are buffered and asynchronously flushed to DuckDB by `BattleResultBuffer` + `BattleResultsFlushService` (see `Arena.AI/Services/DuckDbBattleRepository.cs`); Q-learning records follow the same pattern in `Arena.AI/QFolder/`. No authentication.
