namespace Arena.AI.Trainer.Eval;

public record EvalPlayerSpec(string PlayerType, string? ModelName, string? DbPath)
{
    public bool IsHardcoded => PlayerType != "model";

    public string DisplayName => IsHardcoded
        ? PlayerType
        : $"{ModelName}({Path.GetFileName(DbPath)})";
}
