using Arena.AI.Core.Logic;
using Arena.AI.Core.Logic.BattleLogic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core;

public class RealtimeBattle
{
    private readonly List<BattleAction> _actions = [];
    private Team _teamA;
    private Team _teamB;
    private MovementOrderManager _movementOrderManager;

    private BattleState _battleState;

    public string Id { get; }

    public RealtimeBattle()
    {
        Id = Guid.NewGuid().ToString();
        Init();
    }

    private void Init()
    {
        _teamA = TeamGenerator.GenerateRandomTeam("TeamA");
        _teamB = TeamGenerator.GenerateRandomTeam("TeamB");

        _actions.AddRange(UnitPlacer.PlaceUnits(_teamA, Side.Left));
        _actions.AddRange(UnitPlacer.PlaceUnits(_teamB, Side.Right));

        _movementOrderManager = new MovementOrderManager(_teamA, _teamB);

        _battleState = new BattleState
        {
            BattleId = Id,
            TeamA = _teamA,
            TeamB = _teamB,
        };

        CalculateNextMovement();
    }

    public List<BattleAction> GetBattleActions()
        => _actions;

    public BattleState GetBattleState()
        => _battleState;

    public bool Play(UserAction action)
    {
        if(action is null || action.ActionType == UserActionType.Skip)
        {
            return Skip(action?.Label);
        }

        if(action.ActionType == UserActionType.Move)
        {
            return Move(action.Destination!, action.Label);
        }

        if(action.ActionType == UserActionType.Attack)
        {
            return Attack(action.Target!, action.Label);
        }

        Skip(action.Label);
        return false;
    }

    private bool Skip(string? label = null)
    {
        var unit = _battleState.NextUnitInfo.Unit;
        _actions.Add(BattleActionFactory.Skip(unit, label));
        CalculateNextMovement();
        return true;
    }

    private bool Move(string destination, string? label = null)
    {
        if(!_battleState.NextUnitInfo.AvailableDestinations.Contains(destination))
        {
            Skip(label);
            return false;
        }

        if(NumberLetterConverter.TryParseDestination(destination, out var coord))
        {
            var unit = _battleState.NextUnitInfo.Unit;

            DistanceCalculator.Move(unit, coord.Item1, coord.Item2);
            _actions.Add(BattleActionFactory.Move(unit, label));

            var targets = CalculateAvailableTargets(unit);

            if(targets.Any())
            {
                _battleState.NextUnitInfo.AvailableDestinations = [];
            }
            else
            {
                CalculateNextMovement();
            }

            return true;
        }


        return false;
    }

    private bool Attack(string target, string? label = null)
    {
        if(!_battleState.NextUnitInfo.AvailableAttackTarget.Contains(target))
        {
            Skip(label);
            return false;
        }

        var actor = _battleState.NextUnitInfo.Unit;
        var unitToAttack = (_teamA.AliveUnits.Union(_teamB.AliveUnits)).First(u => u.Name == target);

        _actions.Add(BattleActionFactory.Attack(actor, unitToAttack, label));

        var damage = DamageCalculations.CalculateDamage(actor, unitToAttack);
        unitToAttack.Health -= damage;
        _actions.Add(BattleActionFactory.LooseHealth(unitToAttack, damage, label));

        if(unitToAttack.IsDead)
        {
            _actions.Add(BattleActionFactory.Die(unitToAttack, label));
        }
        else if(DistanceCalculator.CanAttackWithoutMoving(unitToAttack, actor))
        {
            _actions.Add(BattleActionFactory.Attack(unitToAttack, actor, label));

            var returnDamage = DamageCalculations.CalculateDamage(unitToAttack, actor) / 2;
            actor.Health -= returnDamage;
            _actions.Add(BattleActionFactory.LooseHealth(actor, returnDamage, label));

            if(actor.IsDead)
            {
                _actions.Add(BattleActionFactory.Die(actor, label));
            }
        }

        CalculateNextMovement();

        return true;
    }

    private void CalculateNextMovement()
    {
        var nextUnitName = _movementOrderManager.WhosNext();
        var team = _teamA.AliveUnits.Any(u => u.Name == nextUnitName) ? _teamA : _teamB;
        var unit = team.AliveUnits.First(u => u.Name == nextUnitName);

        _battleState.NextUnitInfo = new NextUnitInfo
        {
            Unit = unit,
            TeamName = team.Name,
            AvailableDestinations = CalculateAvailableDestinations(unit),
            AvailableAttackTarget = CalculateAvailableTargets(unit)
        };
    }

    private List<string> CalculateAvailableDestinations(Unit unit)
    {
        var occupiedDestinations = (_teamA.AliveUnits.Union(_teamB.AliveUnits))
            .Select(u => (u.XPosition, u.YPosition))
            .ToArray();

        var availableDestinations = new List<(int, int)>();

        for(var x = 1; x <= Constants.ArenaWidth; x++)
        {
            for(var y = 1; y <= Constants.ArenaHeight; y++)
            {
                if(occupiedDestinations.Any(d => d.XPosition == x && d.YPosition == y))
                {
                    continue;
                }

                if(DistanceCalculator.GetShortestDistanceValue(unit, x, y) <= unit.Movement)
                {
                    availableDestinations.Add((x, y));
                }
            }
        }

        return availableDestinations
            .Select(d => NumberLetterConverter.GetDestination(d.Item1, d.Item2))
            .ToList();
    }

    private List<string> CalculateAvailableTargets(Unit unit)
    {
        var enemyTeam = _teamA.AliveUnits.Any(u => u.Name == unit.Name) ? _teamB : _teamA;
        return DistanceCalculator.CanAttackWithoutMoving(unit, enemyTeam).Select(u => u.Name).ToList();
    }
}
