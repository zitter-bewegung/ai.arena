namespace Arena.AI.Core.QStorage;

public record QRecord<TQStateAction> where TQStateAction : QStateAction
{
    public TQStateAction StateAction { get; init; }
    public double Reward { get; set; }
}

