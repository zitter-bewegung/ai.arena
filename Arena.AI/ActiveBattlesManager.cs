using Arena.AI.Core.RealtimePlayers;
using Arena.AI.SignalR;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Arena.AI.Core;
public static class ActiveBattlesManager
{
    private static readonly ConcurrentDictionary<string, LobbyInfo> Lobby = new();
    private static readonly ConcurrentDictionary<string, RealtimeBattleManager> ActiveBattles = new();
    private static readonly ConcurrentDictionary<string, string> PassphraseBattleMapping = new();

    private static IServiceProvider _services;

    public static void Init(IServiceProvider services)
    {
        _services = services;
    }

    public static string CreateInvite()
    {
        var inviteId = Guid.NewGuid().ToString();
        Lobby.TryAdd(inviteId, new LobbyInfo
        {
            InviteId = inviteId,
            PlayerA = new PlayerInfo { PlayerKind = PlayerKind.ExternalPlayer },
            PlayerB = new PlayerInfo { PlayerKind = PlayerKind.ExternalPlayer },
        });

        return inviteId;
    }

    public static string CreateInviteWithBot(PlayerKind botKind)
    {
        var inviteId = Guid.NewGuid().ToString();
        Lobby.TryAdd(inviteId, new LobbyInfo
        {
            InviteId = inviteId,
            PlayerA = new PlayerInfo { PlayerKind = PlayerKind.ExternalPlayer },
            PlayerB = new PlayerInfo { PlayerKind = botKind },

        });

        return inviteId;
    }

    public static string CreatePvPBattle(IRealtimePlayer playerA, IRealtimePlayer playerB)
    {
        var battleManager = new RealtimeBattleManager(playerA, playerB);
        ActiveBattles.TryAdd(battleManager.BattleId, battleManager);

        return battleManager.BattleId;
    }

    public static bool JoinAsPlayer(string inviteId, string passPhrase)
    {
        var wasJoined = JoinAsPlayerA(inviteId, passPhrase);

        if(!wasJoined)
        {
            wasJoined = JoinAsPlayerB(inviteId, passPhrase);
        }

        if(wasJoined && ArePlayersReady(inviteId))
        {
            Task.Run(async () => await PlayBattleAsync(inviteId));
        }

        return wasJoined;
    }

    private static async Task PlayBattleAsync(string inviteId)
    {
        var lobbyInfo = Lobby[inviteId];

        var playerAInfo = lobbyInfo.PlayerA; 
        var playerBInfo = lobbyInfo.PlayerB;

        var playerA = playerAInfo.IsExternal ? GetExternalPlayer(playerAInfo.PassPhrase!) : BotList.Factories[playerAInfo.PlayerKind]();
        var playerB = playerBInfo.IsExternal ? GetExternalPlayer(playerBInfo.PassPhrase!) : BotList.Factories[playerBInfo.PlayerKind]();
        
        var rtManager = new RealtimeBattleManager(playerA, playerB);

        ActiveBattles.TryAdd(rtManager.BattleId, rtManager);
        Lobby.TryRemove(inviteId, out var _);

        if(lobbyInfo.PlayerA.IsExternal && lobbyInfo.PlayerA.PassPhrase != null)
        {
            PassphraseBattleMapping[lobbyInfo.PlayerA.PassPhrase] = rtManager.BattleId;
        }

        if(lobbyInfo.PlayerB.IsExternal && lobbyInfo.PlayerB.PassPhrase != null)
        {
            PassphraseBattleMapping[lobbyInfo.PlayerB.PassPhrase] = rtManager.BattleId;
        }

        await rtManager.PlayBattleAsync();
    }

    private static ExternalRealtimePlayer GetExternalPlayer(string connectionId)
        => new ExternalRealtimePlayer(_services.GetRequiredService<IHubContext<ExternalPlayerHub, IPlayerClient>>(), connectionId);

    public static bool JoinAsPlayerA(string inviteId, string passPhrase)
    {
        if (Lobby.TryGetValue(inviteId, out var battleInfo) && battleInfo.PlayerA.IsExternal && battleInfo.PlayerA.PassPhrase == null)
        {
            battleInfo.PlayerA.PassPhrase = passPhrase;
            return true;
        }

        return false;
    }

    public static bool JoinAsPlayerB(string inviteId, string passPhrase)
    {
        if(Lobby.TryGetValue(inviteId, out var battleInfo) && battleInfo.PlayerB.IsExternal && battleInfo.PlayerB.PassPhrase == null)
        {
            battleInfo.PlayerB.PassPhrase = passPhrase;
            return true;
        }

        return false;
    }

    public static string? GetBattleIdByPassPhrase(string passPhrase)
    {
        if(PassphraseBattleMapping.TryGetValue(passPhrase, out var battleId))
        {
            return battleId;
        }

        return null;
    }

    public static bool ArePlayersReady(string inviteId)
    {
        if(Lobby.TryGetValue(inviteId, out var battleInfo) 
            && (!battleInfo.PlayerA.IsExternal || battleInfo.PlayerA.PassPhrase != null)
            && (!battleInfo.PlayerB.IsExternal || battleInfo.PlayerB.PassPhrase != null))
        {
            return true;
        }

        return false;
    }

    public static void CloseBattle(string battleId)
    {
        ActiveBattles.TryRemove(battleId, out _);
    }
}

public class LobbyInfo
{
    public string? InviteId { get; set; }
    public PlayerInfo PlayerA { get; set; }
    public PlayerInfo PlayerB { get; set; }
}
