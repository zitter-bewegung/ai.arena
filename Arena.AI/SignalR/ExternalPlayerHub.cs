using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Arena.AI.SignalR;

public class ExternalPlayerHub : Hub<IPlayerClient>
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<UserAction>> _pendingActions
        = new();

    override public Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public async Task Join(string battleId)
    {
        var wasJoined = ActiveBattlesManager.JoinAsPlayer(battleId, Context.ConnectionId);
        await Clients.Client(Context.ConnectionId).Joined(wasJoined);
    }

    public Task Act(UserAction action)
    {
        if(_pendingActions.TryRemove(Context.ConnectionId, out var tcs))
        {
            tcs.SetResult(action);
        }

        return Task.CompletedTask;
    }

    public static Task<UserAction> WaitForAction(string connectionId)
    {
        var tcs = new TaskCompletionSource<UserAction>();
        _pendingActions[connectionId] = tcs;
        return tcs.Task;
    }
}


public interface IPlayerClient
{
    Task Joined(bool wasSuccessful);
    Task PendingMovement(BattleState state);
    Task GameEnd(BattleResult result);
}
