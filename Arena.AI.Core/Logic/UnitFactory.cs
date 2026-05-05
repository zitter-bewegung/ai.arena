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

    //winning contribution balanced (2.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats25wb = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 27, Defence = 23, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 27, Defence = 30, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 28, Defence = 25, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 3, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 5, Defence = 9, Range = 17, Movement = 2 },
    };

    //SEMI RANDOM balanced (2.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStatsSR = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 0, Defence = 29, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 30, Defence = 23, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 0, Defence = 30, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 17, Defence = 1, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 1, Defence = 8, Range = 17, Movement = 2 },
    };


    //globally balanced (2.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats25v1 = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 8, Defence = 21, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 5, Defence = 30, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 27, Defence = 0, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 29, Defence = 10, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 5, Defence = 0, Range = 17, Movement = 2 },
    };

    //globally balanced (2.5) v2
    private static Dictionary<UnitType, UnitDefinition> _unitStats25gv2 = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 1, Defence = 26, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 30, Defence = 14, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 10, Defence = 12, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 4, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 3, Range = 17, Movement = 2 },
    };

    //stricktly balanced (2.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats25l = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 25, Defence = 25, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 23, Defence = 30, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 17, Defence = 27, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 23, Defence = 29, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 5, Defence = 30, Range = 17, Movement = 2 },
    };

    //winning contribution balanced (3.5) 'f': 7.117187500000021e-06
    private static Dictionary<UnitType, UnitDefinition> _unitStats = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 22, Defence = 21, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 12, Defence = 29, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 30, Defence = 16, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 1, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 9, Defence = 11, Range = 17, Movement = 2 },
    };

    //globally balanced (3.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats35g = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 18, Defence = 9, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 5, Defence = 25, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 7, Defence = 16, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 2, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 19, Range = 17, Movement = 2 },
    };

    //globally balanced (3.5) SR
    private static Dictionary<UnitType, UnitDefinition> _unitStats35gSR = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 23, Defence = 24, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 29, Defence = 23, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 24, Defence = 21, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 28, Defence = 0, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 28, Range = 17, Movement = 2 },
    };

    //stricktly balanced (3.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats35l = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 21, Defence = 0, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 3, Defence = 17, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 16, Defence = 3, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 16, Defence = 14, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 14, Range = 17, Movement = 2 },
    };

    //stricktly balanced (3.5) SR
    private static Dictionary<UnitType, UnitDefinition> _unitStats35lSR = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 13, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 11, Defence = 26, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 27, Defence = 14, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 9, Defence = 17, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 1, Defence = 4, Range = 17, Movement = 2 },
    };

    // survival balanced (3.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStatsBBB = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 29, Range = 1, Movement = 11 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 8, Defence = 29, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 29, Defence = 27, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 7, Defence = 0, Range = 6, Movement = 4 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 1, Range = 9, Movement = 2 },
    };
}