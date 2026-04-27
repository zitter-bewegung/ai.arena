# %% [markdown]
# # Advanced Unit Balancing Research (Arena.AI C# Backend)
#
# This notebook-style script analyzes and balances units using the exact combat semantics from your C# backend.
#
# Implemented research approaches:
# 1. Side-corrected Bayesian matchup inference + Bradley-Terry strength estimation.
# 2. Multi-objective constrained optimization (simulated annealing with stat budgets).
# 3. Robust minimax tuning against mixed-team meta scenarios.
#
# Engine parity targets from backend:
# - `UnitFactory` base stats
# - `DamageCalculations`
# - `DistanceCalculator`
# - `AutoBattleCalculator`
# - `MovementOrderManager`
# - `UnitPlacer`

# %%
from __future__ import annotations

import copy
import json
import math
import random
from dataclasses import dataclass
from typing import Dict, List, Sequence, Tuple

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import seaborn as sns

sns.set_theme(style="whitegrid")

try:
    from IPython.display import display
except Exception:
    def display(obj) -> None:  # type: ignore[no-redef]
        print(obj)


# %% [markdown]
# ## 1) Backend-aligned constants and base stats

# %%
ARENA_WIDTH = 20
ARENA_HEIGHT = 20
UNIT_MAX_HEALTH = 10
MAX_NUMBER_OF_UNITS = 8
DIAGONAL_PENALTY = 1.5

UNIT_ORDER = ["Light", "Heavy", "Fast", "ShortRange", "LongRange"]
UNIT_ID = {name: idx for idx, name in enumerate(UNIT_ORDER)}

# Mirrors Arena.AI.Core/Logic/UnitFactory.cs defaults
BASE_STATS: Dict[str, Dict[str, int]] = {
    "Light": {"attack": 4, "defence": 4, "range": 1, "movement": 13},
    "Heavy": {"attack": 4, "defence": 5, "range": 1, "movement": 5},
    "Fast": {"attack": 5, "defence": 3, "range": 1, "movement": 15},
    "ShortRange": {"attack": 4, "defence": 3, "range": 3, "movement": 6},
    "LongRange": {"attack": 4, "defence": 2, "range": 6, "movement": 3},
}

STAT_BOUNDS = {
    "attack": (1, 12),
    "defence": (1, 12),
    "range": (1, 8),
    "movement": (1, 15),
}

# Used by the optimizer to avoid global power creep.
BASE_BUDGETS = {
    unit: sum(BASE_STATS[unit][k] for k in ("attack", "defence", "range", "movement"))
    for unit in UNIT_ORDER
}


def clone_stats(stats: Dict[str, Dict[str, int]]) -> Dict[str, Dict[str, int]]:
    return {u: dict(v) for u, v in stats.items()}


def stats_signature(stats: Dict[str, Dict[str, int]]) -> Tuple[int, ...]:
    out: List[int] = []
    for unit in UNIT_ORDER:
        for key in ("attack", "defence", "range", "movement"):
            out.append(int(stats[unit][key]))
    return tuple(out)


def stats_to_frame(stats: Dict[str, Dict[str, int]]) -> pd.DataFrame:
    return pd.DataFrame(stats).T[["attack", "defence", "range", "movement"]]


# %% [markdown]
# ## 2) Exact C#-style simulator in Python

# %%
@dataclass
class UnitState:
    team: str
    name: str
    unit_type: str
    attack: int
    defence: int
    range: int
    movement: int
    health: int = UNIT_MAX_HEALTH
    x: int = 0
    y: int = 0

    @property
    def is_dead(self) -> bool:
        return self.health <= 0


@dataclass
class TeamState:
    name: str
    units: List[UnitState]

    @property
    def alive_units(self) -> List[UnitState]:
        return [u for u in self.units if not u.is_dead]

    @property
    def is_anyone_alive(self) -> bool:
        return any(not u.is_dead for u in self.units)


class ArenaSimulator:
    def __init__(self, stats: Dict[str, Dict[str, int]], seed: int = 42):
        self.stats = clone_stats(stats)
        self.rng = random.Random(seed)

    def _build_unit(self, team_name: str, unit_name: str, unit_type: str) -> UnitState:
        s = self.stats[unit_type]
        return UnitState(
            team=team_name,
            name=unit_name,
            unit_type=unit_type,
            attack=s["attack"],
            defence=s["defence"],
            range=s["range"],
            movement=s["movement"],
            health=UNIT_MAX_HEALTH,
        )

    def _build_team_from_types(self, team_name: str, unit_types: Sequence[str]) -> TeamState:
        units = [
            self._build_unit(team_name, f"{team_name}_{i + 1}", unit_type)
            for i, unit_type in enumerate(unit_types)
        ]
        return TeamState(name=team_name, units=units)

    @staticmethod
    def _place_team(team: TeamState, is_left: bool) -> None:
        for i, unit in enumerate(team.units):
            unit.y = 3 + i * 2
            unit.x = 1 if is_left else ARENA_WIDTH

    @staticmethod
    def _distance(a: UnitState, b: UnitState) -> float:
        delta_x = abs(a.x - b.x)
        delta_y = abs(a.y - b.y)
        diagonal_steps = min(delta_x, delta_y)
        linear_steps = max(delta_x, delta_y) - diagonal_steps
        return diagonal_steps * DIAGONAL_PENALTY + linear_steps

    def _is_near(self, attacker: UnitState, target: UnitState) -> bool:
        return self._distance(attacker, target) <= DIAGONAL_PENALTY

    def _can_attack_without_moving(self, attacker: UnitState, target: UnitState) -> bool:
        if self._is_near(attacker, target):
            return True
        return self._distance(attacker, target) <= attacker.range + (DIAGONAL_PENALTY - 1)

    def _can_attack_with_movement(self, attacker: UnitState, target: UnitState) -> bool:
        return self._distance(attacker, target) <= attacker.range + attacker.movement + (DIAGONAL_PENALTY - 1)

    @staticmethod
    def _step(attacker_dim: int, target_dim: int) -> int:
        if attacker_dim > target_dim:
            return -1
        if attacker_dim < target_dim:
            return 1
        return 0

    def _move_attacker_to_attack_target(self, attacker: UnitState, target: UnitState) -> None:
        if self._can_attack_without_moving(attacker, target):
            return
        while not self._can_attack_without_moving(attacker, target):
            attacker.x += self._step(attacker.x, target.x)
            attacker.y += self._step(attacker.y, target.y)

    def _move_attacker_closer_to_target(self, attacker: UnitState, target: UnitState) -> None:
        # Mirrors rnd.Next(attacker.Movement): [0, movement)
        move_steps = float(self.rng.randrange(attacker.movement))
        while move_steps >= DIAGONAL_PENALTY:
            x_step = self._step(attacker.x, target.x)
            y_step = self._step(attacker.y, target.y)
            attacker.x += x_step
            attacker.y += y_step
            move_steps -= math.sqrt(x_step * x_step + y_step * y_step)

    def _damage(self, attacker: UnitState, target: UnitState) -> int:
        attack = attacker.attack
        defence = target.defence

        attacker_type = UNIT_ID[attacker.unit_type]
        target_type = UNIT_ID[target.unit_type]

        # Mirrors Arena.AI.Core/Logic/DamageCalculations.cs exactly.
        if attacker_type == 0 and target_type == 1:
            if self.rng.random() < 0.10:
                attack += 1

        # Note: backend condition is attacker Light (0) vs target Fast (2).
        if attacker_type == 0 and target_type == 2:
            if self.rng.random() < 0.03:
                return 0

        if attacker_type == 1 and target_type == 2:
            if self.rng.random() < 0.05:
                attack += 1

        if attacker_type == 3:
            if target_type == 1:
                if self.rng.random() < 0.03:
                    defence -= 3
            elif target_type == 0:
                if self.rng.random() < 0.07:
                    defence -= 2
            elif target_type == 2:
                if self.rng.random() < 0.85:
                    defence -= 1
            elif target_type == 4:
                if self.rng.random() < 0.75:
                    defence += 1

        if attacker_type == 4:
            if target_type == 1:
                defence += 6
            elif target_type == 0:
                if self.rng.random() < 0.75:
                    defence -= 2
            elif target_type == 2:
                if self.rng.random() < 0.72:
                    defence -= 2

        damage = attack - defence
        if damage < 1:
            damage = 1

        random_multiplier = 0.9 + (self.rng.random() * 0.2)
        final_damage = int(damage * random_multiplier)
        return 1 if final_damage < 1 else final_damage

    def simulate_battle(
        self,
        team_a_types: Sequence[str],
        team_b_types: Sequence[str],
        max_turns: int = 4000,
    ) -> Dict[str, float]:
        team_a = self._build_team_from_types("A", team_a_types)
        team_b = self._build_team_from_types("B", team_b_types)
        self._place_team(team_a, is_left=True)
        self._place_team(team_b, is_left=False)

        # Mirrors MovementOrderManager sorting + index behavior.
        movement_order = sorted(team_a.units + team_b.units, key=lambda u: u.movement, reverse=True)
        index = 0
        turns = 0

        while team_a.is_anyone_alive and team_b.is_anyone_alive and turns < max_turns:
            turns += 1

            while True:
                index += 1
                if index >= len(movement_order):
                    index = 0
                if not movement_order[index].is_dead:
                    actor_name = movement_order[index].name
                    break

            actor_from_a = any(u.name == actor_name and not u.is_dead for u in team_a.units)
            actor = next(
                u for u in (team_a.alive_units if actor_from_a else team_b.alive_units) if u.name == actor_name
            )
            enemies = team_b if actor_from_a else team_a

            target = min(enemies.alive_units, key=lambda u: self._distance(actor, u))
            can_attack_without_moving = self._can_attack_without_moving(actor, target)
            can_attack_with_movement = self._can_attack_with_movement(actor, target)

            if can_attack_without_moving or can_attack_with_movement:
                if not can_attack_without_moving:
                    self._move_attacker_to_attack_target(actor, target)

                damage = self._damage(actor, target)
                target.health -= damage

                if (not target.is_dead) and self._can_attack_without_moving(target, actor):
                    return_damage = self._damage(target, actor) // 2
                    actor.health -= return_damage
            else:
                self._move_attacker_closer_to_target(actor, target)

        winner = "A" if team_a.is_anyone_alive else "B" if team_b.is_anyone_alive else "draw"
        return {"winner": winner, "turns": float(turns)}


# %% [markdown]
# ## 3) Evaluation primitives (side-corrected)

# %%
def pure_team(unit_type: str) -> List[str]:
    return [unit_type] * MAX_NUMBER_OF_UNITS


def random_mixed_team(np_rng: np.random.Generator) -> List[str]:
    probs = np_rng.dirichlet(np.ones(len(UNIT_ORDER)))
    picks = np_rng.choice(UNIT_ORDER, size=MAX_NUMBER_OF_UNITS, replace=True, p=probs)
    return [str(x) for x in picks]


def side_swapped_matchup(
    stats: Dict[str, Dict[str, int]],
    unit_a: str,
    unit_b: str,
    battles_per_side: int = 80,
    seed: int = 123,
) -> Dict[str, float]:
    sim = ArenaSimulator(stats, seed=seed)
    team_a = pure_team(unit_a)
    team_b = pure_team(unit_b)

    left_wins_for_a = 0.0
    right_wins_for_a = 0.0
    total_turns = 0.0

    for _ in range(battles_per_side):
        left = sim.simulate_battle(team_a, team_b)
        total_turns += left["turns"]
        if left["winner"] == "A":
            left_wins_for_a += 1.0
        elif left["winner"] == "draw":
            left_wins_for_a += 0.5

        right = sim.simulate_battle(team_b, team_a)
        total_turns += right["turns"]
        if right["winner"] == "B":
            right_wins_for_a += 1.0
        elif right["winner"] == "draw":
            right_wins_for_a += 0.5

    raw_left = left_wins_for_a / battles_per_side
    raw_right = right_wins_for_a / battles_per_side
    neutral = 0.5 * (raw_left + raw_right)
    side_bias = raw_left - raw_right
    avg_turns = total_turns / (2 * battles_per_side)

    return {
        "raw_left": raw_left,
        "raw_right": raw_right,
        "neutral": neutral,
        "side_bias": side_bias,
        "avg_turns": avg_turns,
    }


def compute_matchup_matrices(
    stats: Dict[str, Dict[str, int]],
    battles_per_side: int = 80,
    seed: int = 1234,
) -> Dict[str, pd.DataFrame]:
    neutral = pd.DataFrame(index=UNIT_ORDER, columns=UNIT_ORDER, dtype=float)
    raw_left = pd.DataFrame(index=UNIT_ORDER, columns=UNIT_ORDER, dtype=float)
    raw_right = pd.DataFrame(index=UNIT_ORDER, columns=UNIT_ORDER, dtype=float)
    side_bias = pd.DataFrame(index=UNIT_ORDER, columns=UNIT_ORDER, dtype=float)
    turns = pd.DataFrame(index=UNIT_ORDER, columns=UNIT_ORDER, dtype=float)

    pair_idx = 0
    for a in UNIT_ORDER:
        for b in UNIT_ORDER:
            pair_seed = seed + pair_idx * 17
            pair_idx += 1
            res = side_swapped_matchup(stats, a, b, battles_per_side=battles_per_side, seed=pair_seed)
            neutral.loc[a, b] = res["neutral"]
            raw_left.loc[a, b] = res["raw_left"]
            raw_right.loc[a, b] = res["raw_right"]
            side_bias.loc[a, b] = res["side_bias"]
            turns.loc[a, b] = res["avg_turns"]

    return {
        "neutral": neutral,
        "raw_left": raw_left,
        "raw_right": raw_right,
        "side_bias": side_bias,
        "turns": turns,
    }


def plot_heatmap(df: pd.DataFrame, title: str, center: float = 0.5, cmap: str = "coolwarm") -> None:
    plt.figure(figsize=(8, 6))
    sns.heatmap(df, annot=True, fmt=".2f", cmap=cmap, center=center, vmin=0.0, vmax=1.0)
    plt.title(title)
    plt.tight_layout()
    plt.show()


# %% [markdown]
# ## 4) Research Approach 1: Bayesian pairwise model + Bradley-Terry ranking
#
# Why:
# - Raw single-side win rates are biased by side/turn-order dynamics.
# - We estimate strengths from side-swapped outcomes, then quantify uncertainty.

# %%
def collect_pairwise_counts(
    stats: Dict[str, Dict[str, int]],
    battles_per_side: int = 120,
    seed: int = 2026,
) -> Tuple[np.ndarray, np.ndarray]:
    n = len(UNIT_ORDER)
    wins = np.zeros((n, n), dtype=float)
    totals = np.zeros((n, n), dtype=float)

    sim = ArenaSimulator(stats, seed=seed)

    for i, ui in enumerate(UNIT_ORDER):
        for j in range(i + 1, n):
            uj = UNIT_ORDER[j]
            for _ in range(battles_per_side):
                # ui on left
                left = sim.simulate_battle(pure_team(ui), pure_team(uj))
                if left["winner"] == "A":
                    wins[i, j] += 1.0
                elif left["winner"] == "B":
                    wins[j, i] += 1.0
                else:
                    wins[i, j] += 0.5
                    wins[j, i] += 0.5

                # ui on right
                right = sim.simulate_battle(pure_team(uj), pure_team(ui))
                if right["winner"] == "B":
                    wins[i, j] += 1.0
                elif right["winner"] == "A":
                    wins[j, i] += 1.0
                else:
                    wins[i, j] += 0.5
                    wins[j, i] += 0.5

            totals[i, j] = 2.0 * battles_per_side
            totals[j, i] = 2.0 * battles_per_side

    return wins, totals


def fit_bradley_terry_mm(
    wins: np.ndarray,
    totals: np.ndarray,
    max_iter: int = 3000,
    tol: float = 1e-9,
) -> np.ndarray:
    n = wins.shape[0]
    strengths = np.ones(n, dtype=float)
    n_ij = totals.copy()

    for _ in range(max_iter):
        old = strengths.copy()
        total_wins = wins.sum(axis=1)

        for i in range(n):
            denom = 0.0
            for j in range(n):
                if i == j or n_ij[i, j] <= 0:
                    continue
                denom += n_ij[i, j] / max(strengths[i] + strengths[j], 1e-12)

            if denom > 0 and total_wins[i] > 0:
                strengths[i] = total_wins[i] / denom

        # Normalize around 0 mean in log-space.
        strengths = np.clip(strengths, 1e-12, None)
        strengths /= np.exp(np.mean(np.log(strengths)))

        if np.max(np.abs(np.log(strengths) - np.log(old))) < tol:
            break

    return np.log(strengths)


def bootstrap_bt_intervals(
    wins: np.ndarray,
    totals: np.ndarray,
    bootstrap_samples: int = 300,
    seed: int = 404,
) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    n = wins.shape[0]

    p_hat = np.zeros_like(wins, dtype=float)
    with np.errstate(divide="ignore", invalid="ignore"):
        p_hat = np.where(totals > 0, wins / totals, 0.0)

    draws: List[np.ndarray] = []
    for _ in range(bootstrap_samples):
        bwins = np.zeros_like(wins, dtype=float)
        btotals = np.zeros_like(totals, dtype=float)

        for i in range(n):
            for j in range(i + 1, n):
                n_ij = int(totals[i, j])
                if n_ij <= 0:
                    continue
                kij = rng.binomial(n_ij, p_hat[i, j])
                bwins[i, j] = float(kij)
                bwins[j, i] = float(n_ij - kij)
                btotals[i, j] = float(n_ij)
                btotals[j, i] = float(n_ij)

        draws.append(fit_bradley_terry_mm(bwins, btotals))

    samples = np.vstack(draws)
    center = np.median(samples, axis=0)
    low = np.quantile(samples, 0.05, axis=0)
    high = np.quantile(samples, 0.95, axis=0)
    return center, low, high


def run_approach_1(
    stats: Dict[str, Dict[str, int]],
    battles_per_side: int = 120,
    bootstrap_samples: int = 300,
    seed: int = 2026,
) -> pd.DataFrame:
    wins, totals = collect_pairwise_counts(stats, battles_per_side=battles_per_side, seed=seed)
    bt = fit_bradley_terry_mm(wins, totals)
    center, low, high = bootstrap_bt_intervals(
        wins,
        totals,
        bootstrap_samples=bootstrap_samples,
        seed=seed + 1,
    )

    df = pd.DataFrame(
        {
            "unit": UNIT_ORDER,
            "bt_strength": bt,
            "bt_bootstrap_median": center,
            "bt_ci_05": low,
            "bt_ci_95": high,
        }
    ).sort_values("bt_bootstrap_median", ascending=False)
    return df.reset_index(drop=True)


# %% [markdown]
# ## 5) Research Approach 2: Multi-objective constrained optimization
#
# Why:
# - Attack-only tuning is unstable and causes power creep.
# - We optimize all four stats under budget constraints, using a balanced objective.

# %%
EVAL_CACHE: Dict[Tuple[Tuple[int, ...], int, int], Dict[str, object]] = {}


def evaluate_candidate(
    stats: Dict[str, Dict[str, int]],
    battles_per_side: int = 40,
    seed: int = 777,
    target_turns: float = 90.0,
    base_stats: Dict[str, Dict[str, int]] = BASE_STATS,
) -> Dict[str, object]:
    key = (stats_signature(stats), battles_per_side, seed)
    if key in EVAL_CACHE:
        return EVAL_CACHE[key]

    mats = compute_matchup_matrices(stats, battles_per_side=battles_per_side, seed=seed)
    neutral = mats["neutral"]
    side_bias = mats["side_bias"]
    turns = mats["turns"]

    neutral_values = neutral.to_numpy()
    side_bias_values = side_bias.to_numpy()
    turn_values = turns.to_numpy()

    n = neutral_values.shape[0]
    mask = ~np.eye(n, dtype=bool)

    balance_loss = float(np.mean(np.abs(neutral_values[mask] - 0.5)))
    mirror_loss = float(np.mean(np.abs(np.diag(neutral_values) - 0.5)))
    side_bias_loss = float(np.mean(np.abs(side_bias_values[mask])))
    tempo_loss = float(abs(np.mean(turn_values[mask]) - target_turns) / target_turns)

    row_spreads: List[float] = []
    for i in range(n):
        row = np.delete(neutral_values[i, :], i)
        row_spreads.append(float(np.std(row)))
    diversity = float(np.mean(row_spreads))
    diversity_penalty = float(max(0.05 - diversity, 0.0))

    drift_terms: List[float] = []
    for unit in UNIT_ORDER:
        for stat_name in ("attack", "defence", "range", "movement"):
            lo, hi = STAT_BOUNDS[stat_name]
            denom = max(hi - lo, 1)
            drift_terms.append(abs(stats[unit][stat_name] - base_stats[unit][stat_name]) / denom)
    drift_loss = float(np.mean(drift_terms))

    objective = (
        1.00 * balance_loss
        + 0.70 * mirror_loss
        + 0.80 * side_bias_loss
        + 0.35 * tempo_loss
        + 0.50 * diversity_penalty
        + 0.30 * drift_loss
    )

    out = {
        "objective": objective,
        "balance_loss": balance_loss,
        "mirror_loss": mirror_loss,
        "side_bias_loss": side_bias_loss,
        "tempo_loss": tempo_loss,
        "diversity": diversity,
        "diversity_penalty": diversity_penalty,
        "drift_loss": drift_loss,
        "matrices": mats,
    }
    EVAL_CACHE[key] = out
    return out


def _respect_bounds(stats: Dict[str, Dict[str, int]], unit: str, stat_name: str, value: int) -> bool:
    lo, hi = STAT_BOUNDS[stat_name]
    return not (value < lo or value > hi)


def mutate_budget_preserving(
    stats: Dict[str, Dict[str, int]],
    rng: random.Random,
    max_attempts: int = 40,
) -> Dict[str, Dict[str, int]]:
    candidate = clone_stats(stats)
    stat_names = ["attack", "defence", "range", "movement"]

    for _ in range(max_attempts):
        unit = rng.choice(UNIT_ORDER)
        inc = rng.choice(stat_names)
        dec = rng.choice([s for s in stat_names if s != inc])

        inc_val = candidate[unit][inc] + 1
        dec_val = candidate[unit][dec] - 1

        if not _respect_bounds(candidate, unit, inc, inc_val):
            continue
        if not _respect_bounds(candidate, unit, dec, dec_val):
            continue

        candidate[unit][inc] = inc_val
        candidate[unit][dec] = dec_val

        # Budget conservation guard.
        if sum(candidate[unit][k] for k in ("attack", "defence", "range", "movement")) != BASE_BUDGETS[unit]:
            return clone_stats(stats)

        return candidate

    return candidate


def anneal_optimize_stats(
    start_stats: Dict[str, Dict[str, int]],
    iterations: int = 40,
    battles_per_side: int = 30,
    seed: int = 9001,
) -> Tuple[Dict[str, Dict[str, int]], Dict[str, object], pd.DataFrame]:
    rng = random.Random(seed)
    current = clone_stats(start_stats)
    current_eval = evaluate_candidate(current, battles_per_side=battles_per_side, seed=seed)

    best = clone_stats(current)
    best_eval = current_eval
    history: List[Dict[str, float]] = []

    for step in range(iterations):
        proposal = mutate_budget_preserving(current, rng)
        proposal_eval = evaluate_candidate(proposal, battles_per_side=battles_per_side, seed=seed)

        delta = float(proposal_eval["objective"]) - float(current_eval["objective"])
        temp = max(0.03, 0.35 * (1.0 - step / max(iterations, 1)))
        accept = delta <= 0 or rng.random() < math.exp(-delta / temp)

        if accept:
            current = proposal
            current_eval = proposal_eval

        if float(proposal_eval["objective"]) < float(best_eval["objective"]):
            best = clone_stats(proposal)
            best_eval = proposal_eval

        history.append(
            {
                "iteration": step + 1,
                "current_objective": float(current_eval["objective"]),
                "best_objective": float(best_eval["objective"]),
                "accepted": 1.0 if accept else 0.0,
                "delta": delta,
            }
        )

    return best, best_eval, pd.DataFrame(history)


# %% [markdown]
# ## 6) Research Approach 3: Robust minimax tuning against mixed-team meta
#
# Why:
# - Pairwise pure-vs-pure balance does not guarantee robustness in real mixed compositions.
# - We minimize worst-tail exploitability under random meta-team scenarios.

# %%
def pure_vs_meta_scenarios(
    stats: Dict[str, Dict[str, int]],
    pure_unit: str,
    opponent_teams: List[List[str]],
    battles_per_scenario: int = 12,
    seed: int = 111,
) -> np.ndarray:
    sim = ArenaSimulator(stats, seed=seed)
    pure = pure_team(pure_unit)
    wrs: List[float] = []

    for enemy in opponent_teams:
        wins = 0.0
        total = 0.0
        for _ in range(battles_per_scenario):
            left = sim.simulate_battle(pure, enemy)
            total += 1.0
            if left["winner"] == "A":
                wins += 1.0
            elif left["winner"] == "draw":
                wins += 0.5

            right = sim.simulate_battle(enemy, pure)
            total += 1.0
            if right["winner"] == "B":
                wins += 1.0
            elif right["winner"] == "draw":
                wins += 0.5
        wrs.append(wins / total)

    return np.asarray(wrs, dtype=float)


def robust_meta_score(
    stats: Dict[str, Dict[str, int]],
    scenarios: int = 20,
    battles_per_scenario: int = 10,
    seed: int = 5151,
) -> Dict[str, object]:
    np_rng = np.random.default_rng(seed)
    opponent_teams = [random_mixed_team(np_rng) for _ in range(scenarios)]

    rows = []
    unit_losses = []
    for idx, unit in enumerate(UNIT_ORDER):
        wrs = pure_vs_meta_scenarios(
            stats,
            pure_unit=unit,
            opponent_teams=opponent_teams,
            battles_per_scenario=battles_per_scenario,
            seed=seed + 37 * (idx + 1),
        )
        deviations = np.abs(wrs - 0.5)
        tail = float(np.quantile(deviations, 0.90))
        median_dev = float(abs(np.median(wrs) - 0.5))
        # Robust loss prioritizes tail risk, then center bias.
        robust_loss = tail + 0.5 * median_dev
        unit_losses.append(robust_loss)
        rows.append(
            {
                "unit": unit,
                "median_wr": float(np.median(wrs)),
                "p10_wr": float(np.quantile(wrs, 0.10)),
                "p90_wr": float(np.quantile(wrs, 0.90)),
                "tail_abs_dev_90": tail,
                "robust_loss": robust_loss,
            }
        )

    per_unit = pd.DataFrame(rows).sort_values("robust_loss", ascending=False).reset_index(drop=True)
    overall = float(np.mean(unit_losses))
    worst = float(np.max(unit_losses))
    return {
        "overall_robust_loss": overall,
        "worst_unit_robust_loss": worst,
        "per_unit": per_unit,
    }


def robust_local_search(
    start_stats: Dict[str, Dict[str, int]],
    balance_ceiling: float,
    iterations: int = 25,
    battles_per_side: int = 20,
    robust_scenarios: int = 16,
    robust_battles_per_scenario: int = 8,
    seed: int = 8080,
) -> Tuple[Dict[str, Dict[str, int]], Dict[str, object], Dict[str, object], pd.DataFrame]:
    rng = random.Random(seed)

    current = clone_stats(start_stats)
    current_eval = evaluate_candidate(current, battles_per_side=battles_per_side, seed=seed)
    current_robust = robust_meta_score(
        current,
        scenarios=robust_scenarios,
        battles_per_scenario=robust_battles_per_scenario,
        seed=seed + 1,
    )

    best = clone_stats(current)
    best_eval = current_eval
    best_robust = current_robust
    history: List[Dict[str, float]] = []

    for step in range(iterations):
        proposal = mutate_budget_preserving(current, rng)
        proposal_eval = evaluate_candidate(proposal, battles_per_side=battles_per_side, seed=seed)
        if float(proposal_eval["balance_loss"]) > balance_ceiling:
            history.append(
                {
                    "iteration": step + 1,
                    "accepted": 0.0,
                    "robust_loss": float(current_robust["overall_robust_loss"]),
                    "reason_balance_reject": 1.0,
                }
            )
            continue

        proposal_robust = robust_meta_score(
            proposal,
            scenarios=robust_scenarios,
            battles_per_scenario=robust_battles_per_scenario,
            seed=seed + 100 + step,
        )

        delta = float(proposal_robust["overall_robust_loss"]) - float(current_robust["overall_robust_loss"])
        temp = max(0.02, 0.20 * (1.0 - step / max(iterations, 1)))
        accept = delta <= 0 or rng.random() < math.exp(-delta / temp)

        if accept:
            current = proposal
            current_eval = proposal_eval
            current_robust = proposal_robust

        if float(proposal_robust["overall_robust_loss"]) < float(best_robust["overall_robust_loss"]):
            best = clone_stats(proposal)
            best_eval = proposal_eval
            best_robust = proposal_robust

        history.append(
            {
                "iteration": step + 1,
                "accepted": 1.0 if accept else 0.0,
                "robust_loss": float(current_robust["overall_robust_loss"]),
                "best_robust_loss": float(best_robust["overall_robust_loss"]),
                "reason_balance_reject": 0.0,
            }
        )

    return best, best_eval, best_robust, pd.DataFrame(history)


# %% [markdown]
# ## 7) Utility: backend payload and optional push

# %%
def to_backend_payload(stats: Dict[str, Dict[str, int]]) -> Dict[str, Dict[str, int]]:
    payload: Dict[str, Dict[str, int]] = {}
    for unit in UNIT_ORDER:
        payload[unit] = {
            "type": UNIT_ID[unit],
            "attack": int(stats[unit]["attack"]),
            "defence": int(stats[unit]["defence"]),
            "range": int(stats[unit]["range"]),
            "movement": int(stats[unit]["movement"]),
        }
    return payload


def push_stats_to_backend(
    stats: Dict[str, Dict[str, int]],
    base_url: str = "http://localhost:5222",
    timeout: int = 20,
) -> None:
    import requests  # Lazy import to keep notebook runnable without requests until needed.

    endpoint = f"{base_url}/unit-stats"
    payload = to_backend_payload(stats)
    resp = requests.post(endpoint, json=payload, timeout=timeout, verify=False)
    resp.raise_for_status()


# %% [markdown]
# ## 8) Demo run (fast settings)
#
# Increase sample sizes for production:
# - `battles_per_side` in all evaluators
# - optimization iterations
# - bootstrap samples
# - robust scenarios

# %%
FAST_DEMO = True

if FAST_DEMO:
    battles_base = 24
    battles_opt = 18
    iterations_opt = 18
    battles_bt = 60
    bootstrap_bt = 160
    robust_scenarios = 10
    robust_battles = 6
    iterations_robust = 12
else:
    battles_base = 80
    battles_opt = 40
    iterations_opt = 50
    battles_bt = 150
    bootstrap_bt = 400
    robust_scenarios = 24
    robust_battles = 12
    iterations_robust = 30

# Baseline diagnostics
baseline_eval = evaluate_candidate(BASE_STATS, battles_per_side=battles_base, seed=1701)
baseline_mats = baseline_eval["matrices"]

print("Baseline objective metrics")
for k in ("objective", "balance_loss", "mirror_loss", "side_bias_loss", "tempo_loss", "diversity", "drift_loss"):
    print(f"{k:>16}: {baseline_eval[k]:.4f}")

plot_heatmap(baseline_mats["neutral"], "Baseline Neutral Winrate Matrix (side-corrected)", center=0.5, cmap="RdBu_r")

plt.figure(figsize=(8, 6))
sns.heatmap(
    baseline_mats["side_bias"],
    annot=True,
    fmt=".2f",
    cmap="vlag",
    center=0.0,
    vmin=-0.5,
    vmax=0.5,
)
plt.title("Baseline Side Bias (left - right)")
plt.tight_layout()
plt.show()

# %%
# Approach 1: Bayesian + BT strengths
approach_1_table = run_approach_1(
    BASE_STATS,
    battles_per_side=battles_bt,
    bootstrap_samples=bootstrap_bt,
    seed=1801,
)
display(approach_1_table)

plt.figure(figsize=(8, 4))
plt.errorbar(
    approach_1_table["unit"],
    approach_1_table["bt_bootstrap_median"],
    yerr=[
        approach_1_table["bt_bootstrap_median"] - approach_1_table["bt_ci_05"],
        approach_1_table["bt_ci_95"] - approach_1_table["bt_bootstrap_median"],
    ],
    fmt="o",
    capsize=4,
)
plt.axhline(0.0, color="black", linestyle="--", linewidth=1)
plt.title("Approach 1: Bradley-Terry Strength with 90% Bootstrap CI")
plt.ylabel("Log-strength (relative)")
plt.tight_layout()
plt.show()

# %%
# Approach 2: Multi-objective optimization
best_stats_2, best_eval_2, hist_2 = anneal_optimize_stats(
    BASE_STATS,
    iterations=iterations_opt,
    battles_per_side=battles_opt,
    seed=1901,
)

print("Approach 2 objective metrics")
for k in ("objective", "balance_loss", "mirror_loss", "side_bias_loss", "tempo_loss", "diversity", "drift_loss"):
    print(f"{k:>16}: baseline={baseline_eval[k]:.4f}  ->  optimized={best_eval_2[k]:.4f}")

display(stats_to_frame(best_stats_2))

plt.figure(figsize=(8, 4))
plt.plot(hist_2["iteration"], hist_2["current_objective"], label="Current")
plt.plot(hist_2["iteration"], hist_2["best_objective"], label="Best")
plt.title("Approach 2: Optimization Trajectory")
plt.xlabel("Iteration")
plt.ylabel("Objective")
plt.legend()
plt.tight_layout()
plt.show()

plot_heatmap(
    best_eval_2["matrices"]["neutral"],
    "Approach 2 Neutral Winrate Matrix",
    center=0.5,
    cmap="RdBu_r",
)

# %%
# Approach 3: Robust local search against mixed-team meta
balance_ceiling = float(best_eval_2["balance_loss"]) * 1.20
best_stats_3, best_eval_3, best_robust_3, hist_3 = robust_local_search(
    best_stats_2,
    balance_ceiling=balance_ceiling,
    iterations=iterations_robust,
    battles_per_side=battles_opt,
    robust_scenarios=robust_scenarios,
    robust_battles_per_scenario=robust_battles,
    seed=2001,
)

print("Approach 3 robust metrics")
print(f"{'overall_robust_loss':>24}: {best_robust_3['overall_robust_loss']:.4f}")
print(f"{'worst_unit_robust_loss':>24}: {best_robust_3['worst_unit_robust_loss']:.4f}")
display(best_robust_3["per_unit"])

display(stats_to_frame(best_stats_3))

if not hist_3.empty:
    plt.figure(figsize=(8, 4))
    plt.plot(hist_3["iteration"], hist_3["robust_loss"], label="Current robust loss")
    if "best_robust_loss" in hist_3.columns:
        plt.plot(hist_3["iteration"], hist_3["best_robust_loss"], label="Best robust loss")
    plt.title("Approach 3: Robust Search Trajectory")
    plt.xlabel("Iteration")
    plt.ylabel("Robust loss")
    plt.legend()
    plt.tight_layout()
    plt.show()

# %%
# Final recommendation selection
#
# Decision rule:
# - Prefer lower robust loss if pairwise balance is still close to Approach 2.
use_approach_3 = (
    float(best_robust_3["overall_robust_loss"]) <= 0.98 * float(
        robust_meta_score(best_stats_2, scenarios=robust_scenarios, battles_per_scenario=robust_battles, seed=2101)[
            "overall_robust_loss"
        ]
    )
)

final_stats = best_stats_3 if use_approach_3 else best_stats_2
final_source = "Approach 3 (robust minimax)" if use_approach_3 else "Approach 2 (multi-objective)"

print(f"Selected final profile: {final_source}")
display(stats_to_frame(final_stats))

payload = to_backend_payload(final_stats)
print("Backend JSON payload preview:")
print(json.dumps(payload, indent=2))

# Uncomment to push to running backend:
# push_stats_to_backend(final_stats, base_url="http://localhost:5222")
