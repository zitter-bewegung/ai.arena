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
public class BalancingController : ControllerBase
{

    [HttpPost("objective-function/specific-team")]
    public double GetObjectiveFunctionSpecificUnits(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        return CalculateMetrics(x => TeamGenerator.GenerateTeamOfSpecificType(x.ToString(), x), mode:1);
    }

    [HttpPost("objective-function/specific-team/mode2")]
    public double GetObjectiveFunctionSpecificUnits2(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        return CalculateMetrics(x => TeamGenerator.GenerateTeamOfSpecificType(x.ToString(), x), mode: 2);
    }

    [HttpPost("objective-function/winner-contribution")]
    public double GetSurvivalRateObjective(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        var results = new Dictionary<UnitType, int>();
        var statistics = new Dictionary<UnitType, int>[10];

        Parallel.For(0, 10, i =>
        {
            statistics[i] = GetVictoryContributors();
        });

        for(var i = 0; i < 5; i++)
        {
            results[(UnitType)i] = statistics.Sum(s => s[(UnitType)i]);
        }

        var average = results.Values.Average();

        var objective = results.Select(x => Math.Pow(x.Value / (double)average - 1, 2)).Sum();
        Debug.WriteLine($"Objective function: {objective}");
        return objective;
    }

    private Dictionary<UnitType, int> GetVictoryContributors()
    {
        var winners = new Dictionary<UnitType, int>
        {
            {UnitType.Heavy, 0 },
            {UnitType.Light, 0 },
            {UnitType.Fast, 0 },
            {UnitType.ShortRange, 0 },
            {UnitType.LongRange, 0 }
        };

        for(int i = 0; i < 1000; i++)
        {
            var request = Get();
            var result = AutoBattleCalculator.CalculateBattle(
                request.BattleId,
                request.TeamA,
                request.TeamB);

            var winnerUnits = result.Actions
                .Where(a => a.ActionType == BattleActionType.Appears && a.UnitName.StartsWith(result.Winner))
                .Select(a => a.UnitType)
                .ToArray();

            foreach(var unit in winnerUnits)
            {
                winners[unit]++;
            }
        }

        return winners;
    }

    [HttpPost("objective-function/survival-rate")]
    public double GetSurvivalRate(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }


        var dead = new Dictionary<UnitType, int>();

        var survived = new Dictionary<UnitType, int>();

        foreach(var unit in new[] { UnitType.Light, UnitType.Heavy, UnitType.Fast, UnitType.ShortRange, UnitType.LongRange })
        {
            dead[unit] = 0;
            survived[unit] = 0;
        }


            for(int i = 0; i < 3000; i++)
        {
            if(i % 500 == 0)
            {
                Console.WriteLine($"Calculating random team battle {i}/10000");
            }

            var request = Get();
            var result = AutoBattleCalculator.CalculateBattle(
                request.BattleId,
                request.TeamA,
                request.TeamB);

            var teamAUnits = result.Actions
                .Where(x => x.ActionType == BattleActionType.Appears && x.UnitName.StartsWith("teamA"))
                .Select(x => x.UnitType)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var teamBUnits = result.Actions
                .Where(x => x.ActionType == BattleActionType.Appears && x.UnitName.StartsWith("teamB"))
                .Select(x => x.UnitType)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var deadAUnits = result.Actions
                .Where(x => x.ActionType == BattleActionType.Dies && x.UnitName.StartsWith("teamA"))
                .Select(x => x.UnitType)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var deadBUnits = result.Actions
                .Where(x => x.ActionType == BattleActionType.Dies && x.UnitName.StartsWith("teamB"))
                .Select(x => x.UnitType)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach(var unit in new[] { UnitType.Light, UnitType.Heavy, UnitType.Fast, UnitType.ShortRange, UnitType.LongRange })
            {
                
                dead[unit] += deadAUnits.ContainsKey(unit) ? deadAUnits[unit] : 0;
                dead[unit] += deadBUnits.ContainsKey(unit) ? deadBUnits[unit] : 0;

                survived[unit] += teamAUnits.ContainsKey(unit) ? teamAUnits[unit] : 0;
                survived[unit] += teamBUnits.ContainsKey(unit) ? teamBUnits[unit] : 0;

                survived[unit] -= deadAUnits.ContainsKey(unit) ? deadAUnits[unit] : 0;
                survived[unit] -= deadBUnits.ContainsKey(unit) ? deadBUnits[unit] : 0;
            }
        }

        var results = new Dictionary<UnitType, double>();

        foreach(var unit in new[] { UnitType.Light, UnitType.Heavy, UnitType.Fast, UnitType.ShortRange, UnitType.LongRange })
        {
            results[unit] = survived[unit] == 0 ? 0 : (double)survived[unit] / (survived[unit] + dead[unit]);
        }

        var mean = results.Select(x => x.Value).Average();
        var dispersion = results.Select(x => Math.Pow(x.Value - mean, 2)).Average();

        return Math.Sqrt(dispersion);
    }


    [HttpPost("objective-function/semi-random-team")]
    public double GetObjectiveFunctionsemiRandomUnits(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        return CalculateMetrics(x => TeamGenerator.GenerateSemiRandomTeam(x.ToString(), x), mode: 1);
    }

    [HttpPost("objective-function/semi-random-team/mode2")]
    public double GetObjectiveFunctionsemiRandomUnitsMode2(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        return CalculateMetrics(x => TeamGenerator.GenerateSemiRandomTeam(x.ToString(), x), mode: 2);
    }

    private double CalculateMetrics(Func<UnitType, Team> teamGeneration, int mode = 1)
    {
        const int simulationsPerPair = 100000;
        var avKills = new double[5, 5];

        var values = Enum.GetValues(typeof(UnitType)).Cast<UnitType>();

        var calcs = values
            .SelectMany(a => values, (a, b) => (a, b))
            .Where(pair => pair.a != pair.b)
            .AsParallel()
            .Select(pair =>
            {
                var typeA = pair.a;
                var typeB = pair.b;

                if((int)typeA == (int)typeB)
                {
                    return null;
                }

                var stats = new Stats
                {
                    TypeA = typeA,
                    TypeB = typeB,
                    KillsA = 0,
                    KillsB = 0
                };

                for(int i = 0; i < simulationsPerPair; i++)
                {

                    var win = AutoBattleCalculator.CalculateBattle(
                        Guid.NewGuid().ToString(),
                        teamGeneration(pair.a),
                        teamGeneration(pair.b));

                    /*var results = win.GetDamageDealt();

                    stats.KillsA = results[typeB.ToString()];
                    stats.KillsB = results[typeA.ToString()];
                    */
                    
                    if((UnitType)Enum.Parse(typeof(UnitType), win.Winner) == (UnitType)typeA)
                    {
                        stats.KillsA += 8;
                        stats.KillsB += (8-win.WinnerUnitsLeft.Value);
                    }
                    else
                    {
                        stats.KillsA += (8-win.WinnerUnitsLeft.Value);
                        stats.KillsB += 8;
                    }
                }

                return stats;
            })
            .Where(x => x != null)
            .ToArray();

        foreach(var stat in calcs)
        {
            avKills[(int)stat.TypeA, (int)stat.TypeB] += 1.0 * stat.KillsA / simulationsPerPair;
            avKills[(int)stat.TypeB, (int)stat.TypeA] += 1.0 * stat.KillsB / simulationsPerPair;
        }

        if(mode == 1)
        {
            // global balance
            var killsPerTypeAr = new double[5];
            var deathsPerTypeAr = new double[5];

            for(int i = 0; i < 5; i++)
            {
                for(int j = 0; j < 5; j++)
                {
                    if(i==j)
                    {
                        continue;
                    }

                    killsPerTypeAr[i] += avKills[i, j];
                    deathsPerTypeAr[i] += avKills[j, i];
                }

                killsPerTypeAr[i] /= 4;
                deathsPerTypeAr[i] /= 4;
            }

            var avg = killsPerTypeAr.Average() + deathsPerTypeAr.Average();
            avg /= 2;

            return Math.Sqrt((CalculateMSE(killsPerTypeAr, avg) + CalculateMSE(deathsPerTypeAr, avg))/2)/avg;
        }
        else
        {
            var avg = 0.0;

            for(int i = 0; i < 5; i++)
            {
                for(int j = 0; j < 5; j++)
                {
                    if(i==j)
                    {
                        continue;
                    }

                    avg += avKills[i, j];
                }
            }

            avg /= 20;

            var mse2 = 0.0;

            for(int i = 0; i < 5; i++)
            {
                for(int j = 0; j < 5; j++)
                {
                    if(i==j)
                    {
                        continue;
                    }

                    mse2 += Math.Pow(avKills[i, j] - avg, 2);
                }
            }

            mse2 /= 20;
            var mse = Math.Sqrt(mse2);
            return mse / avg;
        }


        //global balance
        var killsObjective = 0.0;
        var deathsObjective = 0.0;

        for(int i = 0; i < 5; i++)
        {
            var killsPerType = 0.0;
            var deathsPerType = 0.0;

            for(int j = 0; j < 5; j++)
            {
                killsPerType += avKills[i, j];
                deathsPerType += avKills[j, i];
            }

            killsObjective += Math.Pow(killsPerType / 8 / 4  - 0.5, 2);
            deathsObjective += Math.Pow(deathsPerType / 8 / 4  - 0.5, 2);
        }


        return (killsObjective + deathsObjective)/2;

        //strict balance

        /*

        var metric2 = 0.0;
        for(int i = 0; i < 5; i++)
        {
            for(int j = 0; j < 5; j++)
            {
                if(i == j)
                {
                    continue;
                }

                metric2 += Math.Pow(1.0*avKills[i, j]/8 - 0.5, 2);

            }
        }

        return metric2/5; */
    }

    private static double CalculateMSE(double[] array, double avg)
    {
        var mse2 = array.Select(x => Math.Pow(x - avg, 2)).Average();

        return mse2;
    }

    public class Stats
    {
        public UnitType TypeA { get; init; }
        public UnitType TypeB { get; init; }
        public int KillsA { get; set; }
        public int KillsB { get; set; }
    }

    [HttpPost("objective-function/random-team")]
    public double GetObjectiveFunction(Dictionary<UnitType, UnitDefinition> stats = null)
    {
        if(stats != null)
        {
            UnitFactory.SetUnitStats(stats);
        }

        var results = new Dictionary<int, int>();
        var statistics = new Dictionary<int, int>[10];

        Parallel.For(0, 10, i =>
        {
            statistics[i] = GetStatistics();
        });

        for(var i = -8; i <= 8; i++)
        {
            results[i] = statistics.Sum(s => s[i]);
        }

        var objective = CalculateObjectiveFunction(results);
        Debug.WriteLine($"Objective function: {objective}");
        return objective;
    }

    private double CalculateObjectiveFunction(Dictionary<int, int> stats)
    {
        var func = 0;
        var total = 0;

        for(int i = -8; i <= 8; i++)
        {
            func += i * i * stats[i];
            total += stats[i];
        }

        return Math.Sqrt(1.0 * func / total);
    }

    private Dictionary<int, int> GetStatistics()
    {
        var results = new Dictionary<int, int>();
        for(int i = -8; i <= 8; i++)
        {
            results[i] = 0;
        }

        for(int i = 0; i < 500; i++)
        {
            if(i % 500 == 0)
            {
                Console.WriteLine($"Calculating random team battle {i}/3000");
            }

            var request = Get();

            var result = AutoBattleCalculator.CalculateBattle(
                request.BattleId,
                request.TeamA,
                request.TeamB);

            if(result.Winner == "teamA")
            {
                results[result.WinnerUnitsLeft!.Value]++;
            }
            else
            {
                results[-result.WinnerUnitsLeft!.Value]++;
            }
        }

        return results;
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
