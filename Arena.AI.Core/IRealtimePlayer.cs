using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.RealtimePlayers;

namespace Arena.AI.Core;

public interface IRealtimePlayer
{
    Task<UserAction> ActAsync(BattleState battleState);
    Task ReportResultAsync(BattleResult result);
}

public class PlayerInfo
{
    public PlayerKind PlayerKind { get; init; }
    public bool IsExternal => PlayerKind == PlayerKind.ExternalPlayer;
    public string? PassPhrase { get; set; }
}