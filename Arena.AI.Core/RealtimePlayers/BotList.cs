namespace Arena.AI.Core.RealtimePlayers;

public enum PlayerKind
{
    ExternalPlayer,
    SimpleBot1,
    QLearningBot1,
}

public static class BotList
{
    public static Dictionary<PlayerKind, Func<IRealtimePlayer>> Factories = new ()
    {
       [PlayerKind.SimpleBot1] = () => new SimplePlayer1()
    };

    public static void RegisterQLearningBot(Func<IRealtimePlayer> factory)
        => Factories[PlayerKind.QLearningBot1] = factory;
}
