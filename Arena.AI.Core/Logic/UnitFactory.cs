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

    private static Dictionary<UnitType, UnitDefinition> _unitStatsDis = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 14, Defence = 14, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 16, Defence = 20, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 16, Defence = 9, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 15, Defence = 10, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 2, Defence = 4, Range = 17, Movement = 2 },
    };

    private static Dictionary<UnitType, UnitDefinition> _unitStats = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 13, Defence = 13, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 15, Defence = 18, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 15, Defence = 9, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 17, Defence = 10, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 3, Defence = 0, Range = 17, Movement = 2 },
    };

    private static Dictionary<UnitType, UnitDefinition> _unitStatsBalanced = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 13, Defence = 15, Range = 1, Movement = 11 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 20, Defence = 24, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 29, Defence = 15, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 13, Defence = 4, Range = 6, Movement = 4 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 2, Defence = 0, Range = 9, Movement = 2 },
    };

    private static Dictionary<UnitType, UnitDefinition> _unitStats2 = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 15, Defence = 14, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 16, Defence = 24, Range = 1, Movement = 6 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 17, Defence = 12, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 7, Defence = 4, Range = 6, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 3, Defence = 1, Range = 7, Movement = 2 },
    };
}