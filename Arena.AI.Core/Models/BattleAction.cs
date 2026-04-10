using System.Text.Json.Serialization;

namespace Arena.AI.Core.Models;

public class BattleAction
{
    public string UnitName { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UnitType UnitType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BattleActionType ActionType { get; set; }
    public string? Target { get; set; }
    public int? Amount { get; set; }
    public string? Destination { get; set; }
    public string? Label { get; set; }
}
