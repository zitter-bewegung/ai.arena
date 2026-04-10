using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.RealtimePlayers;
using Arena.AI.QFolder;
using Arena.AI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Arena.AI.Controllers;

[ApiController]
[Route("[controller]")]
public class BattleCalculatorController : ControllerBase
{
    private readonly QBattleResultBuffer _buffer;

    public BattleCalculatorController(QBattleResultBuffer buffer)
    {
        _buffer = buffer;
    }

    [HttpGet("random-team")]
    public RandomBattle Get()
    {
        return new RandomBattle
        {
            BattleId = Guid.NewGuid().ToString(),
            TeamA = TeamGenerator.GenerateRandomTeam("teamA"),
            TeamB = TeamGenerator.GenerateRandomTeam("teamB")
        };
    }

    [HttpPost("calculate-team")]
    public BattleResult CalculateBattle(RandomBattle request)
    {
        var result = AutoBattleCalculator.CalculateBattle(
            request.BattleId,
            request.TeamA,
            request.TeamB);

        _buffer.Enqueue(result);
        return result;
    }

    [HttpPost("calculate-random-team")]
    public BattleResult CalculateBattle()
    {
        var request = Get();

        var result = AutoBattleCalculator.CalculateBattle(
            request.BattleId,
            request.TeamA,
            request.TeamB);

        _buffer.Enqueue(result);
        return result;
    }

    [HttpPost("calculate-specific-units-team")]
    public BattleResult CalculateBattleWithSpecificUnitTypes(
        [FromBody] CalculateBattleWithSpecificUnitTypes request
        )
    {
        var unitTypeA = request.UnitTypeTeamA;
        var unitTypeB = request.UnitTypeTeamB;

        var result = AutoBattleCalculator.CalculateBattle(
            request.BattleID,
            TeamGenerator.GenerateTeamOfSpecificType(unitTypeA.ToString(), unitTypeA),
            TeamGenerator.GenerateTeamOfSpecificType(unitTypeB.ToString(), unitTypeB));

        _buffer.Enqueue(result);
        return result;
    }


    [HttpPost("calculate-sp1-vs-sp1")]
    public async Task<BattleResult> CalculateSp1VsSp1()
    {
        var rtbm = new RealtimeBattleManager(
            new SimplePlayer1(),
            new SimplePlayer1());

        await rtbm.PlayBattleAsync();

        return rtbm.GetBattleResult();
    }

    [HttpPost("create-pvp")]
    public async Task<string> CreatePvp()
    {
        var inviteId = ActiveBattlesManager.CreateInvite();
        return inviteId;
    }

    [HttpPost("create-pvb")]
    public async Task<string> CreatePvb()
    {
        var inviteId = ActiveBattlesManager.CreateInviteWithBot(PlayerKind.SimpleBot1);
        return inviteId;
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
    public string BattleId { get; set; }

    [JsonPropertyName("team-a")]
    public Team TeamA { get; set; }

    [JsonPropertyName("team-B")]
    public Team TeamB { get; set; }
}