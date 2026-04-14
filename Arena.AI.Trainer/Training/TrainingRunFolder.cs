using System.Text.Json;

namespace Arena.AI.Trainer.Training;

public class TrainingRunFolder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string RunDir { get; }
    public string DbPath { get; }
    public string TrainingLogPath { get; }
    public string ValidationLogPath { get; }
    public string ConfigPath { get; }

    public TrainingRunFolder(string modelName, string? explicitDbPath = null, string baseDir = "runs")
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        RunDir = Path.Combine(baseDir, $"{modelName}_{timestamp}");
        Directory.CreateDirectory(RunDir);

        DbPath = explicitDbPath ?? Path.Combine(RunDir, "model.db");
        TrainingLogPath = Path.Combine(RunDir, "training.log");
        ValidationLogPath = Path.Combine(RunDir, "validation.log");
        ConfigPath = Path.Combine(RunDir, "config.json");
    }

    public void WriteConfig(TrainingConfig config, string modelName)
    {
        var json = JsonSerializer.Serialize(new { model = modelName, config }, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
