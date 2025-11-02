using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Arena.AI.Controllers;

[ApiController]
[Route("[controller]")]
public class BattleCalculatorController : ControllerBase
{
    [HttpGet("random-team")]
    public RandomBattle Get()
    {
        return new RandomBattle
        {
            BattleID = Guid.NewGuid().ToString(),
            TeamA = TeamGenerator.GenerateRandomTeam("teamA"),
            TeamB = TeamGenerator.GenerateRandomTeam("teamB")
        };
    }

    [HttpPost("calculate-team")]
    public BattleResult CalculateBattle(RandomBattle request)
    {
        return BattleCalculator.CalculateBattle(
            request.BattleID,
            request.TeamA,
            request.TeamB);
    }

    [HttpPost("calculate-random-team")]
    public BattleResult CalculateBattle()
    {
        var request = Get();

        return BattleCalculator.CalculateBattle(
            request.BattleID,
            request.TeamA,
            request.TeamB);
    }

    [HttpPost("calculate-specific-units-team")]
    public BattleResult CalculateBattleWithSpecificUnitTypes(
        [FromBody] CalculateBattleWithSpecificUnitTypes request
        )
    {
        var unitTypeA = request.UnitTypeTeamA;
        var unitTypeB = request.UnitTypeTeamB;

        return BattleCalculator.CalculateBattle(
            request.BattleID, 
            TeamGenerator.GenerateTeamOfSpecificType(unitTypeA.ToString(), unitTypeA), 
            TeamGenerator.GenerateTeamOfSpecificType(unitTypeB.ToString(), unitTypeB));
    }
}

public class CalculateBattleWithSpecificUnitTypes
{
    [JsonPropertyName("battle-id")]
    public string BattleID { get; set; }
    
    [JsonPropertyName("unit-type-A")]
    public UnitType UnitTypeTeamA { get; set; }
    
    [JsonPropertyName("unit-type-B")]
    public UnitType UnitTypeTeamB { get; set; }
}

public class RandomBattle
{
    [JsonPropertyName("battle-id")]
    public string BattleID { get; set; }

    [JsonPropertyName("team-a")]
    public Team TeamA { get; set; }

    [JsonPropertyName("team-B")]
    public Team TeamB { get; set; }
}