using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

namespace Arena.AI.Core.RealtimePlayers;

public class QLearningBot1 : IRealtimePlayer
{
    private readonly IQRepository<MinimalQStateAction> _repo;
    private readonly IQRecordsExtractor<MinimalQStateAction> _extractor;
    private readonly double _epsilon;
    private readonly Random _rng;

    public QLearningBot1(
        IQRepository<MinimalQStateAction> repo,
        IQRecordsExtractor<MinimalQStateAction> extractor,
        double epsilon = 0.0,
        Random? rng = null)
    {
        _repo = repo;
        _extractor = extractor;
        _epsilon = epsilon;
        _rng = rng ?? new Random();
    }

    public async Task<UserAction> ActAsync(BattleState battleState)
    {
        var baseState = _extractor.ExtractState(battleState);

        MinimalQAction chosen;
        if (_rng.NextDouble() < _epsilon)
        {
            var values = Enum.GetValues<MinimalQAction>();
            chosen = values[_rng.Next(values.Length)];
        }
        else
        {
            var best = MinimalQAction.Skips;
            var bestQ = double.NegativeInfinity;
            // Iterating in shuffled order so ties (e.g. all-zero Q-table) break randomly.
            foreach (var action in Enum.GetValues<MinimalQAction>().OrderBy(_ => _rng.Next()))
            {
                var sa = baseState with { Action = action };
                var q = await _repo.GetRewardAsync(sa);
                if (q > bestQ)
                {
                    bestQ = q;
                    best = action;
                }
            }
            chosen = best;
        }

        return TranslateToUserAction(chosen, battleState);
    }

    public Task ReportResultAsync(BattleResult result) => Task.CompletedTask;

    // MinimalQAction → concrete UserAction. The Label MUST be `action.ToString()` so the
    // extractor can round-trip via Enum.Parse(typeof(MinimalQAction), label).
    private UserAction TranslateToUserAction(MinimalQAction action, BattleState bs)
    {
        var label = action.ToString();
        var actor = bs.NextUnitInfo.Unit;
        var enemyTeam = bs.TeamA.Name == bs.NextUnitInfo.TeamName ? bs.TeamB : bs.TeamA;
        var aliveEnemies = enemyTeam.AliveUnits;
        if (aliveEnemies.Length == 0)
        {
            return UserAction.Skip(label);
        }

        var availTargets = bs.NextUnitInfo.AvailableAttackTarget;
        var availDests = bs.NextUnitInfo.AvailableDestinations;

        // Post-move follow-up state: RealtimeBattle.Move() re-prompts the same unit
        // (with empty AvailableDestinations) when it can attack from its new position.
        // The BattleHistory parser assumes a Move is always followed by an Attack from
        // the same unit, so we MUST attack here — skipping would corrupt history parsing.
        if (availDests.Count == 0 && availTargets.Count > 0)
        {
            var followUpTarget = aliveEnemies
                .Where(u => availTargets.Contains(u.Name))
                .OrderBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u))
                .First();
            return UserAction.Attack(followUpTarget.Name, label);
        }

        switch (action)
        {
            case MinimalQAction.Skips:
                return UserAction.Skip(label);

            case MinimalQAction.AttacksClosest:
                return AttackOrApproach(Closest(actor, aliveEnemies), label, availTargets, availDests);

            case MinimalQAction.AttacksWeakest:
                return AttackOrApproach(Weakest(actor, aliveEnemies), label, availTargets, availDests);

            case MinimalQAction.AttacksAnother:
            {
                var w = Weakest(actor, aliveEnemies);
                var c = Closest(actor, aliveEnemies);
                var others = aliveEnemies.Where(u => u.Name != w.Name && u.Name != c.Name).ToArray();
                var target = others.Length > 0 ? others[_rng.Next(others.Length)] : c;
                return AttackOrApproach(target, label, availTargets, availDests);
            }

            case MinimalQAction.Retreats:
            {
                if (availDests.Count == 0)
                {
                    return UserAction.Skip(label);
                }
                // Pick the destination that maximizes the minimum distance to any alive enemy.
                var bestDest = availDests
                    .Select(d =>
                    {
                        NumberLetterConverter.TryParseDestination(d, out var c);
                        var minDist = aliveEnemies.Min(e =>
                            DistanceCalculator.GetShortestDistanceValue(e, c.Item1, c.Item2));
                        return (Dest: d, MinDist: minDist);
                    })
                    .OrderByDescending(x => x.MinDist)
                    .First()
                    .Dest;
                return UserAction.Move(bestDest, label);
            }
        }

        return UserAction.Skip(label);
    }

    private static Unit Closest(Unit actor, Unit[] enemies)
        => enemies
            .OrderBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u))
            .First();

    private static Unit Weakest(Unit actor, Unit[] enemies)
        => enemies
            .OrderBy(u => u.Health)
            .ThenBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u))
            .First();

    private static UserAction AttackOrApproach(
        Unit target,
        string label,
        List<string> availTargets,
        List<string> availDests)
    {
        if (availTargets.Contains(target.Name))
        {
            return UserAction.Attack(target.Name, label);
        }

        if (availDests.Count == 0)
        {
            return UserAction.Skip(label);
        }

        // Move to the available destination closest to the target.
        var best = availDests
            .Select(d =>
            {
                NumberLetterConverter.TryParseDestination(d, out var c);
                return (Dest: d, Dist: DistanceCalculator.GetShortestDistanceValue(target, c.Item1, c.Item2));
            })
            .OrderBy(x => x.Dist)
            .First()
            .Dest;

        return UserAction.Move(best, label);
    }
}
