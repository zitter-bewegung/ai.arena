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

    public static Dictionary<UnitType, UnitDefinition> GetUnitStats(int mode)
        => modes[mode];

    public static void SetUnitStats(Dictionary<UnitType, UnitDefinition> unitStats)
        => _unitStats = unitStats;

    public static void SetUnitStats(int mode)
        => SetUnitStats(modes[mode]);

    public static double[,] GetSimilarityMatrix()
    {
        var similarityMatrix = new double[6, 6];

        for(var i = 0; i < 6; i++)
        {
            for(var j = 0; j < 6; j++)
            {
               similarityMatrix[i, j] = Math.Round(CalculateEuclideanDistance(i, j), 3);
            }
        }

        return similarityMatrix;
    }

    private static double CalculateEuclideanDistance(int mode1, int mode2)
    {
        var stats1 = modes[mode1];
        var stats2 = modes[mode2];

        double sumOfSquares = 0.0;
        foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
        {
            var unit1 = stats1[type];
            var unit2 = stats2[type];
            
            sumOfSquares += Math.Pow(unit1.Attack - unit2.Attack, 2);
            sumOfSquares += Math.Pow(unit1.Defence - unit2.Defence, 2);
        }

        return Math.Sqrt(sumOfSquares)/30;
    }

    private static Unit GetUnitDefinition(UnitType type)
    {
        var stats = _unitStats[type];
        return new Unit { Type = type, Attack = stats.Attack, Defence = stats.Defence, Range = stats.Range, Movement = stats.Movement };
    }

    static Dictionary<int, Dictionary<UnitType, UnitDefinition>> modes = new()
    {
        //globally balanced (3.5)
        [0] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 15, Defence = 19, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 30, Defence = 15, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 25, Defence = 8, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 8, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 13, Defence = 0, Range = 17, Movement = 2 },
        },
        //locally balanced (3.5)
        [1] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 12, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 29, Defence = 18, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 27, Defence = 11, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 29, Defence = 18, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 10, Defence = 0, Range = 17, Movement = 2 },
        },
        //globally balanced SR (3.5)
        [2] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 27, Defence = 0, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 8, Defence = 18, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 24, Defence = 5, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 16, Defence = 1, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 5, Range = 17, Movement = 2 },
        },
        //locally balanced SR (3.5)
        [3] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 28, Defence = 11, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 7, Defence = 30, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 29, Defence = 11, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 17, Defence = 5, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 10, Range = 17, Movement = 2 },
        },
        //survival rate balanced (3.5)
        [4] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 29, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 8, Defence = 29, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 29, Defence = 27, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 7, Defence = 0, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 1, Range = 17, Movement = 2 },
        },
        //victory impact balanced (3.5)
        [5] = new()
        {
            [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 22, Defence = 21, Range = 1, Movement = 12 },
            [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 12, Defence = 29, Range = 1, Movement = 5 },
            [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 30, Defence = 16, Range = 1, Movement = 17 },
            [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 1, Range = 7, Movement = 5 },
            [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 9, Defence = 11, Range = 17, Movement = 2 },
        }
    };

    //winning contribution balanced (3.5) 'f': 7.117187500000021e-06
    private static Dictionary<UnitType, UnitDefinition> _unitStats35w = new()
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
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 24, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 30, Defence = 12, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 25, Defence = 20, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 0, Defence = 19, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 1, Range = 17, Movement = 2 },
    };

    //stricktly balanced (3.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStats35s = new()
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
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 25, Defence = 13, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 30, Defence = 19, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 25, Defence = 18, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 24, Defence = 0, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 1, Range = 17, Movement = 2 },
    };

    // survival balanced (3.5)
    private static Dictionary<UnitType, UnitDefinition> _unitStat35SB = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 29, Defence = 29, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 8, Defence = 29, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 29, Defence = 27, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 7, Defence = 0, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 0, Defence = 1, Range = 17, Movement = 2 },
    };


    //new
    private static Dictionary<UnitType, UnitDefinition> _unitStats_best = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 22, Defence = 21, Range = 1, Movement = 12 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 12, Defence = 29, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 30, Defence = 16, Range = 1, Movement = 17 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 1, Range = 7, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 9, Defence = 11, Range = 17, Movement = 2 },
    };

    //new
    private static Dictionary<UnitType, UnitDefinition> _unitStats = new()
    {
        [UnitType.Light] = new UnitDefinition { Type = UnitType.Light, Attack = 0, Defence = 0, Range = 1, Movement = 11 },
        [UnitType.Heavy] = new UnitDefinition { Type = UnitType.Heavy, Attack = 0, Defence = 0, Range = 1, Movement = 5 },
        [UnitType.Fast] = new UnitDefinition { Type = UnitType.Fast, Attack = 0, Defence = 0, Range = 1, Movement = 16 },
        [UnitType.ShortRange] = new UnitDefinition { Type = UnitType.ShortRange, Attack = 30, Defence = 30, Range = 9, Movement = 5 },
        [UnitType.LongRange] = new UnitDefinition { Type = UnitType.LongRange, Attack = 30, Defence = 30, Range = 18, Movement = 2 },
    };
}