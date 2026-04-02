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
- **Arena.AI/** — ASP.NET Core Web API. Controllers (`BattleCalculatorController`, `UnitStatsController`) and SignalR hub (`ExternalPlayerHub` at `/play` endpoint) for real-time games.
- **Arena.AI.Core/** — Standalone class library with all game logic. No external dependencies. Contains models, battle logic, damage calculations, team generation, and bot AI.
- **src/** — React SPA. Main game component is `Arena.jsx`. Uses Axios for REST, SignalR client for real-time, GSAP for sprite animations, Tailwind CSS for styling.

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

### API Conventions
- JSON property naming uses kebab-case (`battle-id`, `unit-type-A`)
- Swagger UI at `http://localhost:5222/swagger` in Development mode (or `https://localhost:7065/swagger` with `--launch-profile https`)
- SignalR client methods: `Joined`, `PendingMovement`, `GameEnd`
- SignalR server methods: `Join`, `Act`

### No Persistence
All state is in-memory. No database, no authentication.
