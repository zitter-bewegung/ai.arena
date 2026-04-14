namespace Arena.AI.QFolder;

public record DuckDbQRepositoryOptions
{
    public required string TableName { get; init; }
    public string StageTableName => $"{TableName}_stage";
    public double Alpha { get; init; } = 0.1;
    public bool UseWeightedAlpha { get; init; }
    public bool ReadOnly { get; init; }
    public bool CreateIndex { get; init; }
}
