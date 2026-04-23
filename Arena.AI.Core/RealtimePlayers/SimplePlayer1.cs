using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.RealtimePlayers;
public class SimplePlayer1 : IRealtimePlayer
{
    public Task<UserAction> ActAsync(BattleState battleState)
    {
        var enemies = battleState.TeamA.Name == battleState.NextUnitInfo.TeamName ? battleState.TeamB : battleState.TeamA;
        var actor = battleState.NextUnitInfo.Unit;

        var closestUnit = enemies.AliveUnits
            .Select(u => new { Unit = u, Distance = DistanceCalculator.GetShortestDistanceValue(actor, u) })
            .OrderBy(x => x.Distance).Select(x => x.Unit).First();

        var canAttackWithoutMoving = DistanceCalculator.CanAttackWithoutMoving(actor, closestUnit);
        var canAttackWithMovement = DistanceCalculator.CanAttackWithMovement(actor, closestUnit);

        if(canAttackWithoutMoving && battleState.NextUnitInfo.AvailableAttackTarget.Contains(closestUnit.Name))
        {
            return Task.FromResult(UserAction.Attack(closestUnit.Name));
        }

        var availableTargetNames = battleState.NextUnitInfo.AvailableAttackTarget;
        var availableDestinations = battleState.NextUnitInfo.AvailableDestinations;

        if(availableTargetNames.Count > 0)
        {
            var unitToAttack = enemies.AliveUnits.Where(u => availableTargetNames.Contains(u.Name))
                .Select(u => new { Unit = u, Distance = DistanceCalculator.GetShortestDistanceValue(actor, u) })
                .OrderBy(x => x.Distance).Select(x => x.Unit).First();

            if(DistanceCalculator.CanAttackWithoutMoving(actor, unitToAttack))
            {
                return Task.FromResult(UserAction.Attack(unitToAttack.Name));
            }
            else
            {
                var copy = actor.DeepCopy();
                while(!DistanceCalculator.CanAttackWithoutMoving(copy, unitToAttack))
                {
                    var xDiff = unitToAttack.XPosition - copy.XPosition;
                    var yDiff = unitToAttack.YPosition - copy.YPosition;

                    if(Math.Abs(xDiff) > Math.Abs(yDiff))
                    {
                        var xProposed = copy.XPosition + Math.Sign(xDiff);
                        if(availableDestinations.Contains(NumberLetterConverter.GetDestination(xProposed, copy.YPosition)))
                        {
                            copy.XPosition = xProposed;
                            continue;
                        }
                    }

                    copy.YPosition += Math.Sign(yDiff);
                }

                return Task.FromResult(UserAction.Move(NumberLetterConverter.GetDestination(copy.XPosition, copy.YPosition)));
            }
        }
        else
        {
            var copy = actor.DeepCopy();

            var closestAvailableDestination = availableDestinations
                .Select(d =>
                {
                    NumberLetterConverter.TryParseDestination(d, out var dest);
                    copy.XPosition = dest.Item1;
                    copy.YPosition = dest.Item2;
                    return new { Destination = d, Distance = DistanceCalculator.GetShortestDistanceValue(copy, closestUnit) };
                })
                .OrderBy(x => x.Distance).Select(x => x.Destination).FirstOrDefault();

            if(closestAvailableDestination != null)
            {
                return Task.FromResult(UserAction.Move(closestAvailableDestination));
            }
        }
        
        return Task.FromResult(UserAction.Skip());
    }

    public Task ReportResultAsync(BattleResult result) => Task.CompletedTask;
}
