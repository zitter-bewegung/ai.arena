using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class UnitFactory
{
    public static Unit GetUnit(UnitType type, string name)
    {
        var unit = GetUnitDefinition(type);
        unit.Health = Constants.UnitMaxHealth;
        unit.Name = name;

        return unit;
    }

    public static Dictionary<UnitType, UnitDefinition> GetUnitStats()
        => _unitStats;

    public static void SetUnitStats(Dictionary<UnitType, UnitDefinition> unitStats)
        => _unitStats = unitStats;

    private static Unit GetUnitDefinition(UnitType type)
    {
        var stats = _unitStats[type];
        return new Unit { Type = type, Attack = stats.Attack, Defence = stats.Defence, Range = stats.Range, Movement = stats.Movement };
    }

    private static Dictionary<UnitType, UnitDefinition> _unitStats = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 4, Defence = 4, Range = 1, Movement = 13 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 4, Defence = 5, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 5, Defence = 3, Range = 1, Movement = 15 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 4, Defence = 3, Range = 3, Movement = 6 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 4, Defence = 2, Range = 6, Movement = 3 },
    };
}