using Arena.AI.Core.Models;
using CoreQStateAction = Arena.AI.Core.QStorage.QStateAction;

namespace Arena.AI.Trainer.Models.Dwarf;

public record QStateAction : CoreQStateAction
{
    // State
    public UnitType ActorUnitType { get; set; }
    public UnitHealthLevel ActorHealthLevel { get; set; }
    public byte NumberOfTeammates { get; set; }
    public byte NumberOfEnemies { get; set; }
    public DistanceLevel DistanceToWeakest { get; set; }
    public UnitHealthLevel HealthOfWeakest { get; set; }
    public DistanceLevel DistanceToClosest { get; set; }
    public UnitHealthLevel HealthOfClosest { get; set; }
    public DistanceLevel DistanceAverage { get; set; }

    // Action
    public QAction? Action { get; set; }
}

public enum QAction
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
    AttackRange,
    MoveAndAttackRange,
    CannotAttack
}
