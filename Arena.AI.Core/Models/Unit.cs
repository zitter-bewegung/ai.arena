namespace Arena.AI.Core.Models;

public class Unit: UnitDefinition
{
    public int Health { get; set; }
    public string Name { get; set; }
    public int XPosition { get; set; } = 0;
    public int YPosition { get; set; } = 0;
    public bool IsDead => Health <= 0;

    public Unit DeepCopy()
        => new Unit
        {
            Name = this.Name,
            Health = this.Health,
            XPosition = this.XPosition,
            YPosition = this.YPosition,
            Type = this.Type,
            Attack = this.Attack,
            Defence = this.Defence,
            Range = this.Range,
            Movement = this.Movement
        };
}

public static class UnitExtensions
{
    public static string GetPositionOnArena(this Unit unit)
        => $"{NumberLetterConverter.GetDestination(unit.XPosition, unit.YPosition)}";
}