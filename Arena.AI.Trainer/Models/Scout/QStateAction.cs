using Arena.AI.Core.Models;
using CoreQStateAction = Arena.AI.Core.QStorage.QStateAction;

namespace Arena.AI.Trainer.Models.Scout;

public record QStateAction : CoreQStateAction
{
    // Core combat features (same as Dwarf)
    public UnitType ActorUnitType { get; set; }
    public UnitHealthLevel ActorHealthLevel { get; set; }
    public byte NumberOfTeammates { get; set; }
    public byte NumberOfEnemies { get; set; }
    public DistanceLevel DistanceToWeakest { get; set; }
    public UnitHealthLevel HealthOfWeakest { get; set; }
    public DistanceLevel DistanceToClosest { get; set; }
    public UnitHealthLevel HealthOfClosest { get; set; }
    public DistanceLevel DistanceAverage { get; set; }

    // Spatial features (new in Scout)
    public bool Threatened { get; set; }
    public bool Supported { get; set; }
    public bool AtEdge { get; set; }

    // Action
    public QAction? Action { get; set; }
}

public enum QAction
{
    Skips,
    AttacksWeakest,
    AttacksClosest,
    AttacksAnother,
    AttacksThreat,
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
