namespace Arena.AI.Trainer.Training;

public record TrainingConfig
{
    public int Episodes { get; init; } = 2000;
    public double StartEpsilon { get; init; } = 0.3;
    public double FinalEpsilon { get; init; } = 0.05;
    public bool NoLearn { get; init; }
    public double CurriculumFraction { get; init; } = 1.0;
    public double StartSimpleProp { get; init; } = 1.0;
    public double FinalSimpleProp { get; init; } = 1.0;
    public int ValidationGames { get; init; } = 200;
    public int ValidationInterval { get; init; } = 5000;
    public int BatchSize { get; init; } = 100;
    public TimeSpan SnapshotInterval { get; init; } = TimeSpan.FromSeconds(10);
}
