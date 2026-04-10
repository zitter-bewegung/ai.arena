using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class BattleActionFactory
{
    public static BattleAction PlaceUnit(Unit unit, string? label = null)
    {
        return new BattleAction
        {
            ActionType = BattleActionType.Appears,
            UnitName = unit.Name,
            UnitType = unit.Type,
            Destination = unit.GetPositionOnArena(),
            Label = label
        };
    }

    public static BattleAction Move(Unit unit, string? label = null)
    {
        return new BattleAction
        {
            ActionType = BattleActionType.Moves,
            UnitName = unit.Name,
            UnitType = unit.Type,
            Destination = unit.GetPositionOnArena(),
            Label = label
        };
    }

    public static BattleAction Attack(Unit actor, Unit target, string? label = null )
    {
        return new BattleAction
        {
            ActionType = BattleActionType.Attacks,
            UnitName = actor.Name,
            UnitType = actor.Type,
            Target = target.Name,
            Label = label
        };
    }

    public static BattleAction LooseHealth(Unit actor, int healthAmount, string? label = null)
    {
        return new BattleAction
        {
            ActionType = BattleActionType.LosesHealth,
            UnitName = actor.Name,
            UnitType = actor.Type,
            Amount = healthAmount,
            Label = label
        };
    }

    public static BattleAction Die(Unit actor, string? label = null)
    {
        return new BattleAction
        {
            ActionType = BattleActionType.Dies,
            UnitName = actor.Name,
            UnitType = actor.Type,
            Label = label
        };
    }

    public static BattleAction Skip(Unit actor, string? label = null)
    {
        return new BattleAction
        {
            ActionType = BattleActionType.SkipsTurn,
            UnitName = actor.Name,
            UnitType = actor.Type,
            Label = label
        };
    }
}
