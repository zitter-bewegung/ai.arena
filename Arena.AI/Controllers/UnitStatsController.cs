using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Arena.AI.Controllers;

[ApiController]
[Route("unit-stats")]
public class UnitStatsController : ControllerBase
{

    [HttpGet]
    public Dictionary<UnitType, UnitDefinition> Get() 
        => UnitFactory.GetUnitStats();

    [HttpGet("similarity-matrix")]
    public double[][] GetSimilarityMatrix()
    {
        var matrix = UnitFactory.GetSimilarityMatrix();
        return ToJagged(matrix);
    }

    [HttpPost]
    public void Set(Dictionary<UnitType, UnitDefinition> stats) 
        => UnitFactory.SetUnitStats(stats);

    [HttpPost("predefined/{mode}")]
    public void SetPredefined(int mode)
        => UnitFactory.SetUnitStats(mode);

    private static double[][] ToJagged(double[,] array)
    {
        int rows = array.GetLength(0);
        int cols = array.GetLength(1);

        double[][] result = new double[rows][];

        for(int i = 0; i < rows; i++)
        {
            result[i] = new double[cols];
            for(int j = 0; j < cols; j++)
            {
                result[i][j] = array[i, j];
            }
        }

        return result;
    }
}
