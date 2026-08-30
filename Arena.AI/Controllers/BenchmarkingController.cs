using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Arena.AI.Controllers;

[ApiController]
[Route("[controller]")]
public class BenchmarkingController : ControllerBase
{

    [HttpPost("velocity")]
    public async Task<double> GetVelocity()
    {
        const int iterations = 100_000;
        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, iterations, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        i =>
        {
            BenchmarkBattleCalculation();
        });

        stopwatch.Stop();

        return 1.0 * iterations / stopwatch.Elapsed.TotalSeconds;
    }

    private double BenchmarkBattleCalculation()
    {
        var request = Get();
        var result = AutoBattleCalculator.CalculateBattle(
                request.BattleId,
                request.TeamA,
                request.TeamB);

        return 1;
    }

    private RandomBattle Get()
    {
        return new RandomBattle
        {
            BattleId = Guid.NewGuid().ToString(),
            TeamA = TeamGenerator.GenerateRandomTeam("teamA"),
            TeamB = TeamGenerator.GenerateRandomTeam("teamB")
        };
    }
}




