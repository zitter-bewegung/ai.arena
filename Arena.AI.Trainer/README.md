# Arena.AI.Trainer

Offline trainer + evaluator for the tabular Q-learning bots that play the arena game. It is a standalone .NET 8 console app that runs simulated battles in-process (no HTTP / SignalR), feeds the resulting transitions into a per-model DuckDB Q-table, and writes everything to a timestamped run folder for later inspection.

## What this project actually does

There are two modes, picked by command-line flag:

1. **Training** (default) — runs N episodes of self-play + curriculum, updates the Q-table on disk, periodically validates against a frozen snapshot and the hard-coded baseline.
2. **Eval** (`--eval`) — runs N games between two specified players (trained model vs trained model, or trained model vs baseline), writes per-game JSON traces and a summary.

There is **no "live game" mode in this project**. Live games (SignalR, browser) are served by the `Arena.AI` web project. Trained bots are not yet wired into `Arena.AI.Core/RealtimePlayers/BotList.cs`, so today the trainer is the only place a Q-learning bot actually plays.

## Code layout

```
Arena.AI.Trainer/
  Program.cs                    # CLI entry point — parses flags, dispatches to Training or Eval
  Models/
    IModelProfile.cs            # Per-model contract: state/action shape, DI wiring, snapshot lifecycle
    ModelRegistry.cs            # "dwarf" | "scout" → IModelProfile
    Dwarf/                      # One model. Owns its QStateAction record, RecordExtractor (state encoding +
    Scout/                      # credit assignment), TableSchema (DuckDB columns), Bot (ε-greedy player),
                                # CachedQRepository (in-memory read cache), ModelProfile (DI + snapshots).
  Training/
    TrainingLoop.cs             # Self-play episode loop: parallel battles → buffer → flush, ε decay, curriculum,
                                # periodic snapshots and async validation.
    ValidationRunner.cs         # Current model vs baseline + vs previous snapshot, both colors swapped.
    TrainingConfig.cs           # Hyperparams (episodes, ε schedule, batch size, snapshot/validation cadence).
    TrainingRunFolder.cs        # Creates runs/{model}_{timestamp}/ with model.db, training.log, validation.log, config.json.
  Eval/
    EvalRunner.cs               # Plays N matchups (each played twice with sides swapped) and dumps JSON traces.
    EvalPlayerFactory.cs        # Resolves "simple" or a trained model from a model.db on disk.
    EvalPlayerSpec.cs           # (PlayerType, ModelName, DbPath) DTO.
    EvalRunFolder.cs            # Creates evals/{timestamp}/games/ + summary.json.
```

### How a single training step flows

1. `TrainingLoop` runs `BatchSize` battles in parallel; the writer-side bot uses an ε-greedy policy backed by `CachedQRepository`.
2. After each battle, `IModelProfile.EnqueueResult` pushes the `BattleResult` into `QBattleResultBuffer` (for self-play, two POV-stripped copies — one per side).
3. A background `QBattleResultsFlushService` (in `Arena.AI/QFolder/`) drains the buffer every 10s and calls `QRecordManager.ProcessBattleResultsAsync`.
4. The model's `RecordExtractor.ExtractRecords` walks the battle history and emits `(state, action, reward)` rows with reward = discounted future-kill sum (γ=0.95).
5. `DuckDbQRepository.SaveRecordsAsync` groups records by key, computes an effective α for the batch (`1 - (1-α)^n`), stages them, and applies the standard Q-update in SQL: `Q ← (1-α)·Q + α·target`.
6. Every `SnapshotInterval`, `q_table` is copied to `q_table_snapshot` (used by the opponent in self-play) and `q_table_prev_validation` (used by validation).

### How a model is added

Implement `IModelProfile` in `Models/{YourModel}/` with: a `QStateAction` record (state + action features), a `RecordExtractor` (state encoding + per-record reward), a `TableSchema` (DuckDB column mapping), a `Bot` (`IRealtimePlayer`), an `EvalPlayerProvider` (read-only repo loader), and register it in `ModelRegistry.Create`.

## Commands

All commands run from the repo root.

### Build

```bash
dotnet build Arena.AI.Trainer
```

### Train

```bash
# Default: dwarf model, profile defaults
dotnet run --project Arena.AI.Trainer

# Pick a model, set hyperparams
dotnet run --project Arena.AI.Trainer -- \
  --model scout \
  --episodes 50000 \
  --epsilon 0.8 \
  --final-epsilon 0.05

# Continue training into an existing DB instead of a new run folder
dotnet run --project Arena.AI.Trainer -- --model dwarf --db runs/dwarf_20260414_153000/model.db
```

Outputs go to `runs/{model}_{yyyyMMdd_HHmmss}/`:
- `model.db` — DuckDB file with `q_table` (live), `q_table_snapshot` (frozen self-play opponent), `q_table_prev_validation` (last validation snapshot)
- `training.log` — per-batch W/L/D, ε, episodes/sec, buffer size, snapshot events
- `validation.log` — vs Baseline and vs previous-snapshot win-rates at each `ValidationInterval`
- `config.json` — the resolved `TrainingConfig`

Press Ctrl+C to stop cleanly after the current batch (a final snapshot + validation will run).

#### Common training flags

| Flag | Default | What it does |
|---|---|---|
| `--model` | `dwarf` | Model profile name (`dwarf`, `scout`) |
| `--episodes` | model default (2000) | Total self-play episodes |
| `--epsilon` | model default (0.8) | Starting ε for exploration |
| `--final-epsilon` | model default (0.05) | ε floor (exponential decay, ~99% drop by ~60% of training) |
| `--db` | new file under `runs/` | Train into an existing `model.db` |

Other knobs (`BatchSize`, `ValidationInterval`, `ValidationGames`, `SnapshotInterval`, `CurriculumFraction`, `StartSimpleProp`, `FinalSimpleProp`) live in each model's `ModelProfile.DefaultConfig` — change them in code.

### Evaluate

Plays `--games` battles (rounded down to even) between two specified players, with sides swapped on every other game so position bias cancels. Writes one JSON trace per game.

```bash
# Trained model vs the hard-coded baseline (SimplePlayer1)
dotnet run --project Arena.AI.Trainer -- --eval \
  --run dwarf_20260414_153000 \
  --games 200

# Trained model vs another trained model (e.g. dwarf vs scout)
dotnet run --project Arena.AI.Trainer -- --eval \
  --run dwarf_20260414_153000 \
  --opponent-run scout_20260414_160000 \
  --games 200

# Same model, two different runs (regression check)
dotnet run --project Arena.AI.Trainer -- --eval \
  --run dwarf_20260414_153000 \
  --opponent-run dwarf_20260413_120000 \
  --games 500
```

`--run NAME` resolves to `runs/NAME/model.db` and infers the model name from the folder prefix (`dwarf_…` → `dwarf`). Override with `--model` / `--opponent-model` if needed. The opponent defaults to `simple` (the `SimplePlayer1` baseline) when `--opponent-run` is not given.

Outputs go to `evals/{yyyyMMdd_HHmmss}/`:
- `games/game_NNNN_sideA.json`, `game_NNNN_sideB.json` — full action trace per battle
- `summary.json` — overall W/L/D + winrate

### Live game

The trainer does not host live games. To play a live game in a browser, run the web app as documented in the root `CLAUDE.md`:

```bash
# Backend (terminal 1)
dotnet run --project Arena.AI

# Frontend (terminal 2)
npm install
npm run dev    # http://localhost:5173
```

Today the only bot exposed via the SignalR hub is `SimplePlayer1` (see `Arena.AI.Core/RealtimePlayers/BotList.cs`). To let a trained Q-learning model play live, a new `PlayerKind` and `BotList` factory entry would need to be added that loads a `model.db` through the model's `EvalPlayerProvider` and returns its `Bot`.
