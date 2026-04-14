using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Trainer.Models;

namespace Arena.AI.Trainer.Training;

public static class ValidationRunner
{
    public static async Task RunAsync(
        IValidationContext context,
        int validationGames,
        int episode,
        int prevSnapshotEp,
        TextWriter log)
    {
        var parallelism = Environment.ProcessorCount;
        int matchups = validationGames / 2;

        var teamPairs = GenerateTeamPairs(matchups, episode);

        log.WriteLine($"[{DateTime.Now:HH:mm:ss}] --- Validation at ep {episode} (ε=0, {matchups} matchups x 2 sides) ---");

        var baselineResult = await RunMatchups(matchups, teamPairs, episode, 0,
            context.CreateCurrentPlayer, _ => context.CreateBaselineOpponent(), parallelism);

        var baselineLine =
            $"[{DateTime.Now:HH:mm:ss}] vs Baseline:                   {baselineResult.Total} games  " +
            $"W/L/D={baselineResult.Wins}/{baselineResult.Losses}/{baselineResult.Draws}  winrate={baselineResult.Winrate:P1}";
        log.WriteLine(baselineLine);
        Console.WriteLine($"  [ep {episode}] vs Baseline:   W/L/D={baselineResult.Wins}/{baselineResult.Losses}/{baselineResult.Draws}  winrate={baselineResult.Winrate:P1}");

        var snapshotResult = await RunMatchups(matchups, teamPairs, episode, matchups * 2,
            context.CreateCurrentPlayer, context.CreatePreviousPlayer, parallelism);

        var snapshotLine =
            $"[{DateTime.Now:HH:mm:ss}] vs Snapshot (from ep {prevSnapshotEp}):  {snapshotResult.Total} games  " +
            $"W/L/D={snapshotResult.Wins}/{snapshotResult.Losses}/{snapshotResult.Draws}  winrate={snapshotResult.Winrate:P1}";
        log.WriteLine(snapshotLine);
        log.Flush();
        Console.WriteLine($"  [ep {episode}] vs Snapshot (ep {prevSnapshotEp}): W/L/D={snapshotResult.Wins}/{snapshotResult.Losses}/{snapshotResult.Draws}  winrate={snapshotResult.Winrate:P1}");
    }

    private static (Team A, Team B)[] GenerateTeamPairs(int matchups, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, matchups)
            .Select(_ => (
                A: TeamGenerator.GenerateRandomTeam("TeamA"),
                B: TeamGenerator.GenerateRandomTeam("TeamB")))
            .ToArray();
    }

    private static async Task<MatchResult> RunMatchups(
        int matchups,
        (Team A, Team B)[] teamPairs,
        int episode,
        int seedOffset,
        Func<Random, IRealtimePlayer> createOurPlayer,
        Func<Random, IRealtimePlayer> createOpponent,
        int parallelism)
    {
        int wins = 0, losses = 0, draws = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, matchups),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (i, _) =>
            {
                var (teamA, teamB) = teamPairs[i];

                // Game 1: our bot as TeamA
                var rng1 = new Random(episode * 10000 + seedOffset + i);
                var rt1 = new RealtimeBattleManager(
                    createOurPlayer(rng1), createOpponent(rng1),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt1.PlayBattleAsync();
                var r1 = rt1.GetBattleResult();
                if      (r1.Winner == "TeamA") Interlocked.Increment(ref wins);
                else if (r1.Winner == "TeamB") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);

                // Game 2: our bot as TeamB (swap sides)
                var rng2 = new Random(episode * 10000 + seedOffset + matchups + i);
                var rt2 = new RealtimeBattleManager(
                    createOpponent(rng2), createOurPlayer(rng2),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt2.PlayBattleAsync();
                var r2 = rt2.GetBattleResult();
                if      (r2.Winner == "TeamB") Interlocked.Increment(ref wins);
                else if (r2.Winner == "TeamA") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);
            });

        return new MatchResult(wins, losses, draws);
    }

    private record MatchResult(int Wins, int Losses, int Draws)
    {
        public int Total => Wins + Losses + Draws;
        public double Winrate => Total > 0 ? (double)Wins / Total : 0;
    }
}
