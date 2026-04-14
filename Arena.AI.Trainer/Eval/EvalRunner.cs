using System.Text.Json;
using Arena.AI.Core;
using Arena.AI.Core.Logic;

namespace Arena.AI.Trainer.Eval;

public static class EvalRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task RunAsync(
        EvalPlayerFactory playerA,
        EvalPlayerFactory playerB,
        int games,
        EvalRunFolder folder)
    {
        var parallelism = Environment.ProcessorCount;
        int matchups = games / 2;

        Console.WriteLine($"Eval: {playerA.DisplayName} vs {playerB.DisplayName}  ({matchups} matchups x 2 sides)");
        Console.WriteLine($"Output: {folder.EvalDir}");

        // Generate all team pairs upfront (single-threaded, deterministic)
        var teamRng = new Random(12345);
        var teamPairs = Enumerable.Range(0, matchups)
            .Select(_ => (
                A: TeamGenerator.GenerateRandomTeam("TeamA"),
                B: TeamGenerator.GenerateRandomTeam("TeamB")))
            .ToArray();

        int wins = 0, losses = 0, draws = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, matchups),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (i, _) =>
            {
                var (teamA, teamB) = teamPairs[i];

                // Game 1: playerA as TeamA
                var rng1 = new Random(i * 2);
                var rt1 = new RealtimeBattleManager(
                    playerA.CreatePlayer(rng1),
                    playerB.CreatePlayer(rng1),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt1.PlayBattleAsync();
                var r1 = rt1.GetBattleResult();

                if      (r1.Winner == "TeamA") Interlocked.Increment(ref wins);
                else if (r1.Winner == "TeamB") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);

                var path1 = Path.Combine(folder.GamesDir, $"game_{i:D4}_sideA.json");
                await WriteGameTrace(path1, r1, playerA.DisplayName, playerB.DisplayName);

                // Game 2: playerA as TeamB (swap sides)
                var rng2 = new Random(i * 2 + 1);
                var rt2 = new RealtimeBattleManager(
                    playerB.CreatePlayer(rng2),
                    playerA.CreatePlayer(rng2),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt2.PlayBattleAsync();
                var r2 = rt2.GetBattleResult();

                if      (r2.Winner == "TeamB") Interlocked.Increment(ref wins);
                else if (r2.Winner == "TeamA") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);

                var path2 = Path.Combine(folder.GamesDir, $"game_{i:D4}_sideB.json");
                await WriteGameTrace(path2, r2, playerB.DisplayName, playerA.DisplayName);
            });

        var totalGames = matchups * 2;
        var winrate = totalGames > 0 ? (double)wins / totalGames : 0;

        Console.WriteLine($"Result: W/L/D={wins}/{losses}/{draws}  winrate={winrate:P1}  ({totalGames} games)");

        folder.WriteSummary(new
        {
            playerA = playerA.DisplayName,
            playerB = playerB.DisplayName,
            totalGames,
            wins,
            losses,
            draws,
            winrate,
            timestamp = DateTime.Now
        });

        Console.WriteLine($"Done. Traces in {folder.GamesDir}/");
    }

    private static async Task WriteGameTrace(
        string path, BattleResult result, string teamAPlayer, string teamBPlayer)
    {
        var trace = new
        {
            result.BattleId,
            result.Winner,
            metadata = new
            {
                teamA = teamAPlayer,
                teamB = teamBPlayer
            },
            result.Actions
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(trace, JsonOptions));
    }
}
