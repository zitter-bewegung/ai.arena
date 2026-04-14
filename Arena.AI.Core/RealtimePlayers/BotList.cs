namespace Arena.AI.Core.RealtimePlayers;

public enum PlayerKind
{
    ExternalPlayer,
    SimpleBot1,
}

public static class BotList
{
    public static Dictionary<PlayerKind, Func<IRealtimePlayer>> Factories = new()
    {
        [PlayerKind.SimpleBot1] = () => new SimplePlayer1()
    };
}
