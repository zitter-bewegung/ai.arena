using Arena.AI.Core.Logic.BattleLogic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class AutoBattleCalculator
{
    public static BattleResult CalculateBattle(string battleId, Team teamA, Team teamB)
    {
        var actions = new List<BattleAction>();

        actions.AddRange(UnitPlacer.PlaceUnits(teamA, Side.Left));
        actions.AddRange(UnitPlacer.PlaceUnits(teamB, Side.Right));

        var movementOrderManager = new MovementOrderManager(teamA, teamB);

        while(teamA.IsAnyoneAlive && teamB.IsAnyoneAlive)
        {
            var next = movementOrderManager.WhosNext();
            actions.AddRange(Act(next, teamA, teamB));
        }

        return new BattleResult
        {
            BattleId = battleId,
            Winner = teamA.IsAnyoneAlive ? teamA.Name : teamB.IsAnyoneAlive ? teamB.Name : string.Empty,
            WinnerUnitsLeft = teamA.AliveUnits.Union(teamB.AliveUnits).Count(),
            Actions = actions
        };
    }

    private static List<BattleAction> Act(string actorName, Team teamA, Team teamB)
    {
        var battleActions = new List<BattleAction>();
        
        var isActorFromA = teamA.AliveUnits.Any(u => u.Name == actorName);
        var actor = (isActorFromA ? teamA : teamB).AliveUnits.First(u => u.Name == actorName);

        battleActions.AddRange(Act(actor, isActorFromA ? teamB : teamA));
        
        return battleActions;
    }
    
    private static List<BattleAction> Act(Unit actor, Team enemies)
    {
        var battleActions = new List<BattleAction>();

        var unitToAttack = enemies.AliveUnits
            .Select(u => new { Unit = u, Distance = DistanceCalculator.GetShortestDistanceValue(actor, u) })
            .OrderBy(x => x.Distance).Select(x => x.Unit).First();

        var canAttackWithoutMoving = DistanceCalculator.CanAttackWithoutMoving(actor, unitToAttack);
        var canAttackWithMovement = DistanceCalculator.CanAttackWithMovement(actor, unitToAttack);

        if(canAttackWithoutMoving || canAttackWithMovement)
        {
            if(!canAttackWithoutMoving)
            {
                DistanceCalculator.MoveAttackerToAttackTarget(actor, unitToAttack);
                battleActions.Add(BattleActionFactory.Move(actor));
            }

            battleActions.Add(BattleActionFactory.Attack(actor, unitToAttack));

            var damage = DamageCalculationsV0.CalculateDamage(actor, unitToAttack);
            unitToAttack.Health -= damage;
            battleActions.Add(BattleActionFactory.LooseHealth(unitToAttack, damage));

            if(unitToAttack.IsDead)
            {
                battleActions.Add(BattleActionFactory.Die(unitToAttack));
            }
            else if(DistanceCalculator.CanAttackWithoutMoving(unitToAttack, actor))
            {
                battleActions.Add(BattleActionFactory.Attack(unitToAttack, actor));

                var returnDamage = DamageCalculationsV0.CalculateDamage(unitToAttack, actor) / 2;
                actor.Health -= returnDamage;
                battleActions.Add(BattleActionFactory.LooseHealth(actor, returnDamage));

                if(actor.IsDead)
                {
                    battleActions.Add(BattleActionFactory.Die(actor));
                }
            }
        }
        else
        {
            DistanceCalculator.MoveAttackerCloserToTarget(actor, unitToAttack);
            battleActions.Add(BattleActionFactory.Move(actor));
        }

        return battleActions;
    }
}

public class BattleResult
{
    public string BattleId { get; set; }
    public string Winner {  get; set; }
    public int? WinnerUnitsLeft { get; set; }
    public List<BattleAction> Actions { get; set; }
}

public static class BattleResultExtensions
{
    public static Dictionary<string, int> GetDamageDealt(this BattleResult battleResult)
    {
        var teamNames = battleResult.Actions.Select(a => a.UnitName.Split("_")[0]).Distinct().ToArray();

        Dictionary<string, int> summary = new Dictionary<string, int>();
        summary[teamNames[0]] = battleResult.Actions!.Where(a => a.ActionType == BattleActionType.LosesHealth && a.UnitName.StartsWith(teamNames[1])).Select(a => a.Amount).Sum() ?? 0;
        summary[teamNames[1]] = battleResult.Actions!.Where(a => a.ActionType == BattleActionType.LosesHealth && a.UnitName.StartsWith(teamNames[0])).Select(a => a.Amount).Sum() ?? 0;

        return summary;
    }
}
