using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;

// Configurable parameters
int battleCount = args.Length > 0 ? int.Parse(args[0]) : 10000;
int battlesPerFile = args.Length > 1 ? int.Parse(args[1]) : 10000;
var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
int completedCount = 0;
int fileCounter = 0;

var options = new JsonSerializerOptions { WriteIndented = true };

var parallelOptions = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

var allResults = new ConcurrentBag<BattleResult>();
var batchLock = new object();
var currentBatch = new ConcurrentBag<BattleResult>();

// Process battles in parallel
Parallel.For(0, battleCount, parallelOptions, i =>
{
    try
    {
        var teamA = TeamGenerator.GenerateRandomTeam("TeamA");
        var teamB = TeamGenerator.GenerateRandomTeam("TeamB");
        var battleId = Guid.NewGuid().ToString();

        var result = AutoBattleCalculator.CalculateBattle(battleId, teamA, teamB);

        result = FeatureTransformer.Transform(result);
        allResults.Add(result);
        currentBatch.Add(result);

        // Write batch to file when full
        if (currentBatch.Count >= battlesPerFile)
        {
            lock (batchLock)
            {
                if (currentBatch.Count >= battlesPerFile)
                {
                    WriteBatchToFile(currentBatch, ref fileCounter, timestamp, options);
                    currentBatch.Clear();
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Battle {i} failed: {ex.Message}");
    }

    // Progress reporting
    int count = Interlocked.Increment(ref completedCount);
    if (count % 1000 == 0)
        Console.WriteLine($"Progress: {count}/{battleCount} battles");
});

// Write any remaining battles
if (currentBatch.Count > 0)
{
    WriteBatchToFile(currentBatch, ref fileCounter, timestamp, options);
}

Console.WriteLine($"Generated {allResults.Count} battles across {fileCounter} files");

static void WriteBatchToFile(ConcurrentBag<BattleResult> batch, ref int fileCounter, string timestamp, JsonSerializerOptions options)
{
    var batchList = batch.ToList();
    var batchNumber = Interlocked.Increment(ref fileCounter);
    var filePath = $"battles_batch_{batchNumber:D6}_{timestamp}.json";

    var json = JsonSerializer.Serialize(batchList, options);
    File.WriteAllText(filePath, json);

    Console.WriteLine($"Wrote batch {batchNumber}: {batchList.Count} battles -> {filePath}");
    Console.WriteLine($"File size: {new FileInfo(filePath).Length / 1024:N0} KB");
}
