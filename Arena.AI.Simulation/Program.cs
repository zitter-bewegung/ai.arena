using Arena.AI.Core.Logic;
using System.Collections.Concurrent;
using System.Text.Json;

int battleCount = args.Length > 0 ? int.Parse(args[0]) : 100000;
int battlesPerFile = args.Length > 1 ? int.Parse(args[1]) : 10000;

var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

var options = new JsonSerializerOptions
{
    WriteIndented = false
};

var queue = new BlockingCollection<BattleResult>(50000);

int completedCount = 0;
int fileCounter = 0;

var writerTask = Task.Run(() =>
{
    var batch = new List<BattleResult>(battlesPerFile);

    foreach (var result in queue.GetConsumingEnumerable())
    {
        batch.Add(result);

        if (batch.Count >= battlesPerFile)
        {
            WriteBatchToFile(batch, ref fileCounter, timestamp, options);
            batch.Clear();
        }
    }

    if (batch.Count > 0)
        WriteBatchToFile(batch, ref fileCounter, timestamp, options);
});

Parallel.For(0, battleCount, new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
},
i =>
{
    try
    {
        var teamA = TeamGenerator.GenerateRandomTeam("TeamA");
        var teamB = TeamGenerator.GenerateRandomTeam("TeamB");

        var battleId = Guid.NewGuid().ToString();

        var result = AutoBattleCalculator.CalculateBattle(battleId, teamA, teamB);

        queue.Add(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Battle {i} failed: {ex.Message}");
    }

    int count = Interlocked.Increment(ref completedCount);

    if (count % 1000 == 0)
        Console.WriteLine($"Progress: {count}/{battleCount}");
});

queue.CompleteAdding();
writerTask.Wait();

Console.WriteLine($"Generated {completedCount} battles across {fileCounter} files");

static void WriteBatchToFile(List<BattleResult> batch, ref int fileCounter, string timestamp, JsonSerializerOptions options)
{
    var batchNumber = Interlocked.Increment(ref fileCounter);

    var filePath = $"battles_batch_{batchNumber:D6}_{timestamp}.json";

    var json = JsonSerializer.Serialize(batch, options);

    File.WriteAllText(filePath, json);

    Console.WriteLine($"Wrote batch {batchNumber}: {batch.Count}");
}