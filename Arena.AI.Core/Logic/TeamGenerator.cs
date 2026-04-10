using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class TeamGenerator
{
    private static Random rnd = new();

    public static Team GenerateTeamOfSpecificType(string name, UnitType unitType)
    {
        return new Team
        {
            Name = name,
            Units = Enumerable.Range(1, Constants.MaxNumberOfUnits)
                .Select(x => UnitFactory.GetUnit(unitType, GetUnitName(name, x)))
                .ToArray()
        };
    }

    public static Team GenerateRandomTeam(string name)
    {
        return new Team
        {
            Name = name,
            Units = Enumerable.Range(1, Constants.MaxNumberOfUnits)
                .Select(x => UnitFactory.GetUnit((UnitType)rnd.Next(5), GetUnitName(name, x)))
                .ToArray()
        };
    }

    private static string GetUnitName(string teamName, int unitNumber)
        => $"{teamName}_{unitNumber}";
}

public class Team
{
    public string Name { get; init; }
    public Unit[] Units { get; init; }
    public Unit[] AliveUnits => Units.Where(u => !u.IsDead).ToArray();
    public bool IsAnyoneAlive => Units.Where(u => !u.IsDead).Any();

    public Team DeepCopy()
        => new ()
        {
            Name = this.Name,
            Units = this.Units.Select(u => u.DeepCopy()).ToArray()
        };
}