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

    [HttpPost]
    public void Set(Dictionary<UnitType, UnitDefinition> stats) 
        => UnitFactory.SetUnitStats(stats);
}
