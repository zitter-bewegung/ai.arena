using System.Text.Json;

namespace Arena.AI.Trainer.Eval;

public class EvalRunFolder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string EvalDir { get; }
    public string GamesDir { get; }
    public string SummaryPath { get; }

    public EvalRunFolder(string baseDir = "evals")
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EvalDir = Path.Combine(baseDir, timestamp);
        GamesDir = Path.Combine(EvalDir, "games");
        Directory.CreateDirectory(GamesDir);
        SummaryPath = Path.Combine(EvalDir, "summary.json");
    }

    public void WriteSummary(object summary)
    {
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(SummaryPath, json);
    }
}
