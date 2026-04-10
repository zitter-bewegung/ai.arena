using Arena.AI.Core.Models;

namespace Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

public record MinimalQStateAction: QStateAction
{
    //State
    public UnitType ActorUnitType { get; set; }
    public UnitHealthLevel ActorHealthLevel { get; set; }
    public byte NumberOfTeammates { get; set; }
    public byte NumberOfEnemies { get; set; }
    public DistanceLevel DistanceToWeakest { get; set; }
    public UnitHealthLevel HealthOfWeakest { get; set; }
    public DistanceLevel DistanceToClosest { get; set; }
    public UnitHealthLevel HealthOfClosest { get; set; }
    public DistanceLevel DistanceAverage { get; set; }

    //Action
    public MinimalQAction? Action { get; set; }
}

public enum MinimalQAction
{
    Skips,
    AttacksWeakest,
    AttacksClosest,
    AttacksAnother,
    Retreats
}

public enum UnitHealthLevel
{
    High,
    Medium,
    Low
}

public enum DistanceLevel
{
    AttackRange,  // less than 0.67 attack range
    MoveAndAttackRange, // between 0.67 and 1 attack range
    CannotAttack     // more than 1 attack range
}