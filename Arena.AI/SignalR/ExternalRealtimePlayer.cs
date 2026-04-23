using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Arena.AI.SignalR;

public class ExternalRealtimePlayer : IRealtimePlayer
{
    private readonly IHubContext<ExternalPlayerHub, IPlayerClient> _hub;
    private readonly string _connectionId;

    public ExternalRealtimePlayer(
        IHubContext<ExternalPlayerHub, IPlayerClient> hub,
        string connectionId)
    {
        _hub = hub;
        _connectionId=connectionId;
    }

    public async Task<UserAction> ActAsync(BattleState battleState)
    {
        var actionTask = ExternalPlayerHub.WaitForAction(_connectionId);
        await _hub.Clients.Client(_connectionId).PendingMovement(battleState);
        
        var result = await actionTask;
        return result;
    }

    public async Task ReportResultAsync(BattleResult result)
        => await _hub.Clients.Client(_connectionId).GameEnd(result);
}
