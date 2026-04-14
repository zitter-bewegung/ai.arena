using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;

namespace Arena.AI.Trainer.Models.Scout;

public class Bot : IRealtimePlayer
{
    private readonly IQRepository<QStateAction> _repo;
    private readonly IQRecordsExtractor<QStateAction> _extractor;
    private readonly double _epsilon;
    private readonly Random _rng;

    public Bot(
        IQRepository<QStateAction> repo,
        IQRecordsExtractor<QStateAction> extractor,
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

        QAction chosen;
        if (_rng.NextDouble() < _epsilon)
        {
            var values = Enum.GetValues<QAction>();
            chosen = values[_rng.Next(values.Length)];
        }
        else
        {
            var best = QAction.Skips;
            var bestQ = double.NegativeInfinity;
            foreach (var action in Enum.GetValues<QAction>().OrderBy(_ => _rng.Next()))
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

    private UserAction TranslateToUserAction(QAction action, BattleState bs)
    {
        var label = action.ToString();
        var actor = bs.NextUnitInfo.Unit;
        var enemyTeam = bs.TeamA.Name == bs.NextUnitInfo.TeamName ? bs.TeamB : bs.TeamA;
        var aliveEnemies = enemyTeam.AliveUnits;
        if (aliveEnemies.Length == 0)
            return UserAction.Skip(label);

        var availTargets = bs.NextUnitInfo.AvailableAttackTarget;
        var availDests = bs.NextUnitInfo.AvailableDestinations;

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
            case QAction.Skips:
                return UserAction.Skip(label);

            case QAction.AttacksClosest:
                return AttackOrApproach(Closest(actor, aliveEnemies), label, availTargets, availDests);

            case QAction.AttacksWeakest:
                return AttackOrApproach(Weakest(actor, aliveEnemies), label, availTargets, availDests);

            case QAction.AttacksThreat:
                return AttackOrApproach(Threat(actor, aliveEnemies), label, availTargets, availDests);

            case QAction.AttacksAnother:
            {
                var w = Weakest(actor, aliveEnemies);
                var c = Closest(actor, aliveEnemies);
                var t = Threat(actor, aliveEnemies);
                var others = aliveEnemies.Where(u => u.Name != w.Name && u.Name != c.Name && u.Name != t.Name).ToArray();
                var target = others.Length > 0 ? others[_rng.Next(others.Length)] : c;
                return AttackOrApproach(target, label, availTargets, availDests);
            }

            case QAction.Retreats:
            {
                if (availDests.Count == 0)
                    return UserAction.Skip(label);
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
        => enemies.OrderBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u)).First();

    private static Unit Weakest(Unit actor, Unit[] enemies)
        => enemies.OrderBy(u => u.Health).ThenBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u)).First();

    private static Unit Threat(Unit actor, Unit[] enemies)
        => enemies.OrderByDescending(u => u.Attack).ThenBy(u => DistanceCalculator.GetShortestDistanceValue(actor, u)).First();

    private static UserAction AttackOrApproach(
        Unit target, string label, List<string> availTargets, List<string> availDests)
    {
        if (availTargets.Contains(target.Name))
            return UserAction.Attack(target.Name, label);
        if (availDests.Count == 0)
            return UserAction.Skip(label);

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
