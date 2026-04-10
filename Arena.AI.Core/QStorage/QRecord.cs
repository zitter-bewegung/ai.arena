namespace Arena.AI.Core.QStorage;

public record QRecord<TQStateAction> where TQStateAction : QStateAction
{
    public TQStateAction StateAction { get; init; }
    public double Reward => 1.0 * NumberOfKills / NumberOfGames;
    public int NumberOfKills { get; set; }
    public int NumberOfGames { get; set; }
}

