using System.Collections.Concurrent;
using System.Diagnostics;
using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;
using Arena.AI.Core.RealtimePlayers;
using Arena.AI.QFolder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Arena.AI.Trainer;

public static class DuckDbTrainer
{
    private const double CurriculumFraction = 1.0; // no self-play — train 100% against SimplePlayer1
    private const double StartSimpleProp = 1.0;
    private const double FinalSimpleProp = 1.0;
    private const int ValidationGames = 200;
    private const int ValidationInterval = 5000;
    private const int BatchSize = 100;
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(10);

    public static async Task RunAsync(
        int episodes, double startEps, double finalEps,
        string dbPath, bool noLearn)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCtrl+C received — stopping after current batch...");
        };
        var ct = cts.Token;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var trainingLogPath = $"training_{timestamp}.log";
        var validationLogPath = $"validation_{timestamp}.log";

        await using var trainingLog = new StreamWriter(trainingLogPath, append: false) { AutoFlush = false };
        await using var rawValidationLog = new StreamWriter(validationLogPath, append: false) { AutoFlush = false };
        var validationLog = TextWriter.Synchronized(rawValidationLog);

        Console.WriteLine($"Logs: {trainingLogPath}, {validationLogPath}");

        await using var repo = new PersistentDuckDbRepository(dbPath);
        repo.Log = trainingLog;
        await repo.OpenAsync();

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IQRepository<MinimalQStateAction>>(repo);
                services.AddSingleton<IQRecordsExtractor<MinimalQStateAction>, MinimalQRecordExtractor>();
                services.AddSingleton<QRecordManager<MinimalQStateAction>>();
                services.AddSingleton<QBattleResultBuffer>();
                services.AddHostedService<QBattleResultsFlushService>();
            })
            .Build();

        await host.StartAsync();

        var buffer = host.Services.GetRequiredService<QBattleResultBuffer>();
        var extractor = host.Services.GetRequiredService<IQRecordsExtractor<MinimalQStateAction>>();

        // Create initial snapshot table
        await repo.CopyTableAsync("minimal_model", "minimal_model_snapshot");

        // Self-play opponent reads from the snapshot table via its own connection
        await using var snapshotRepo = new DuckDbReadOnlyRepository(dbPath, "minimal_model_snapshot");
        await snapshotRepo.OpenAsync();

        // Read-through caches for thread-safe parallel access.
        // NOT registered in DI — the flush service uses repo directly on its own thread.
        var trainingCache = new CachedQRepository(repo);
        var snapshotCache = new CachedQRepository(snapshotRepo);

        var parallelism = Environment.ProcessorCount;
        int curriculumEpisodes = Math.Max(1, (int)(episodes * CurriculumFraction));
        var header =
            $"DuckDB Trainer starting: episodes={episodes} epsilon={startEps}->{finalEps} " +
            $"db={dbPath} curriculum={StartSimpleProp:P0}->{FinalSimpleProp:P0} over {curriculumEpisodes} eps " +
            $"parallelism={parallelism} batch={BatchSize}";
        Console.WriteLine(header);
        trainingLog.WriteLine(header);
        trainingLog.Flush();

        int wins = 0, losses = 0, draws = 0;
        int lastSnapshotEp = 0;
        int prevValidationSnapshotEp = 0;
        var lastSnapshotTime = Stopwatch.GetTimestamp();
        var sw = Stopwatch.StartNew();

        // Save initial model as the baseline for the first validation comparison
        await repo.CopyTableAsync("minimal_model", "minimal_model_prev_validation");

        var validationTasks = new List<Task>();
        var evalTablesToCleanUp = new ConcurrentBag<string>();
        int completedEps = 0;

        for (int batchStart = 1; batchStart <= episodes && !ct.IsCancellationRequested; batchStart += BatchSize)
        {
            int batchEnd = Math.Min(batchStart + BatchSize - 1, episodes);

            // Compute epsilon and curriculum at batch midpoint for logging
            double batchEps = startEps + (finalEps - startEps) * ((double)(batchEnd - 1) / Math.Max(1, episodes - 1));
            double batchSimpleProp = StartSimpleProp + (FinalSimpleProp - StartSimpleProp)
                * Math.Min(1.0, (double)(batchEnd - 1) / Math.Max(1, curriculumEpisodes - 1));

            int batchWins = 0, batchLosses = 0, batchDraws = 0;

            await Parallel.ForEachAsync(
                Enumerable.Range(batchStart, batchEnd - batchStart + 1),
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                async (ep, token) =>
                {
                    double eps = startEps + (finalEps - startEps) * ((double)(ep - 1) / Math.Max(1, episodes - 1));
                    double simpleProp = StartSimpleProp + (FinalSimpleProp - StartSimpleProp)
                        * Math.Min(1.0, (double)(ep - 1) / Math.Max(1, curriculumEpisodes - 1));

                    var localRng = new Random(42 + ep);
                    bool useSimple = localRng.NextDouble() < simpleProp;

                    IRealtimePlayer playerA = new QLearningBot1(trainingCache, extractor, eps, localRng);
                    IRealtimePlayer playerB = useSimple
                        ? new SimplePlayer1()
                        : new QLearningBot1(snapshotCache, extractor, eps, localRng);

                    var rt = new RealtimeBattleManager(playerA, playerB);
                    await rt.PlayBattleAsync();
                    var result = rt.GetBattleResult();

                    if (!noLearn)
                    {
                        if (useSimple)
                            buffer.Enqueue(result);
                        else
                        {
                            buffer.Enqueue(StripLabels(result, "TeamB"));
                            buffer.Enqueue(StripLabels(result, "TeamA"));
                        }
                    }

                    if      (result.Winner == "TeamA") Interlocked.Increment(ref batchWins);
                    else if (result.Winner == "TeamB") Interlocked.Increment(ref batchLosses);
                    else                               Interlocked.Increment(ref batchDraws);
                });

            wins += batchWins;
            losses += batchLosses;
            draws += batchDraws;
            completedEps = batchEnd;

            // Update snapshot and caches on the same cadence as the flush service (~10s)
            var timeSinceSnapshot = Stopwatch.GetElapsedTime(lastSnapshotTime);
            if (timeSinceSnapshot >= SnapshotInterval)
            {
                lastSnapshotEp = completedEps;
                await repo.CopyTableAsync("minimal_model", "minimal_model_snapshot");
                trainingCache.Invalidate();
                snapshotCache.Invalidate();
                lastSnapshotTime = Stopwatch.GetTimestamp();
                trainingLog.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] snapshot updated at ep {completedEps}");
            }

            var rate = completedEps / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            var logLine =
                $"[{DateTime.Now:HH:mm:ss}] ep {completedEps,6}/{episodes}  ε={batchEps:F3}  simple%={batchSimpleProp:P0}  " +
                $"W/L/D={wins}/{losses}/{draws}  winrate={(double)wins / completedEps:P1}  " +
                $"rate={rate:F1} ep/s  buffer={buffer.Count}";
            trainingLog.WriteLine(logLine);
            trainingLog.Flush();
            Console.WriteLine(logLine);

            // Kick off background validation
            if (completedEps % ValidationInterval == 0)
            {
                var evalCurrentTable = $"eval_current_{completedEps}";
                var evalPrevTable = $"eval_prev_{completedEps}";
                await repo.CopyTableAsync("minimal_model", evalCurrentTable);
                await repo.CopyTableAsync("minimal_model_prev_validation", evalPrevTable);

                evalTablesToCleanUp.Add(evalCurrentTable);
                evalTablesToCleanUp.Add(evalPrevTable);

                var valEp = completedEps;
                var valPrevEp = prevValidationSnapshotEp;
                var valWinrate = (double)wins / completedEps;
                validationTasks.Add(Task.Run(async () =>
                {
                    Console.WriteLine(
                        $"ep {valEp}/{episodes}  winrate={valWinrate:P1}  — validation started (background)");
                    await RunValidation(dbPath, extractor, evalCurrentTable, evalPrevTable,
                        valEp, valPrevEp, validationLog);
                }));

                // Rotate: current model becomes the "previous" for next validation
                await repo.CopyTableAsync("minimal_model", "minimal_model_prev_validation");
                prevValidationSnapshotEp = completedEps;
            }
        }

        sw.Stop();
        var summary =
            $"[{DateTime.Now:HH:mm:ss}] {(noLearn ? "Eval" : "Training")}{(ct.IsCancellationRequested ? " (interrupted)" : "")} " +
            $"done in {sw.Elapsed.TotalSeconds:F2}s. " +
            $"Final W/L/D = {wins}/{losses}/{draws}  winrate={(completedEps > 0 ? (double)wins / completedEps : 0):P1}";
        trainingLog.WriteLine(summary);
        trainingLog.Flush();
        Console.WriteLine(summary);

        await host.StopAsync();

        if (!ct.IsCancellationRequested && completedEps % ValidationInterval != 0)
        {
            Console.WriteLine("Running final validation...");
            var finalEvalTable = "eval_current_final";
            var finalPrevTable = "eval_prev_final";
            await repo.CopyTableAsync("minimal_model", finalEvalTable);
            await repo.CopyTableAsync("minimal_model_prev_validation", finalPrevTable);
            evalTablesToCleanUp.Add(finalEvalTable);
            evalTablesToCleanUp.Add(finalPrevTable);

            await RunValidation(dbPath, extractor, finalEvalTable, finalPrevTable,
                completedEps, prevValidationSnapshotEp, validationLog);
        }

        await Task.WhenAll(validationTasks);

        foreach (var table in evalTablesToCleanUp)
            await repo.DropTableAsync(table);
        await repo.DropTableAsync("minimal_model_prev_validation");

        validationLog.Flush();
        Console.WriteLine($"Done. See {trainingLogPath} and {validationLogPath}");
    }

    private static async Task RunValidation(
        string dbPath,
        IQRecordsExtractor<MinimalQStateAction> extractor,
        string currentModelTable,
        string snapshotTable,
        int episode,
        int snapshotEp,
        TextWriter log)
    {
        await using var currentRepo = new DuckDbReadOnlyRepository(dbPath, currentModelTable);
        await currentRepo.OpenAsync();
        await using var snapRepo = new DuckDbReadOnlyRepository(dbPath, snapshotTable);
        await snapRepo.OpenAsync();

        // Validation tables are static — cache never goes stale, perfect hit rate after warm-up
        var currentCache = new CachedQRepository(currentRepo);
        var snapCache = new CachedQRepository(snapRepo);

        var parallelism = Environment.ProcessorCount;
        int matchups = ValidationGames / 2; // each matchup played from both sides

        // Pre-generate fixed team compositions for stable results
        var teamRng = new Random(episode);
        var teamPairs = Enumerable.Range(0, matchups)
            .Select(_ => (
                A: TeamGenerator.GenerateRandomTeam("TeamA"),
                B: TeamGenerator.GenerateRandomTeam("TeamB")))
            .ToArray();

        log.WriteLine($"[{DateTime.Now:HH:mm:ss}] --- Validation at ep {episode} (ε=0, {matchups} matchups × 2 sides) ---");

        // vs SimplePlayer1
        int wins = 0, losses = 0, draws = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, matchups),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (i, _) =>
            {
                var (teamA, teamB) = teamPairs[i];

                // Game 1: our bot as TeamA
                var rng1 = new Random(episode * 10000 + i);
                var rt1 = new RealtimeBattleManager(
                    new QLearningBot1(currentCache, extractor, 0.0, rng1),
                    new SimplePlayer1(),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt1.PlayBattleAsync();
                var r1 = rt1.GetBattleResult();
                if      (r1.Winner == "TeamA") Interlocked.Increment(ref wins);
                else if (r1.Winner == "TeamB") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);

                // Game 2: our bot as TeamB (swap sides)
                var rng2 = new Random(episode * 10000 + matchups + i);
                var rt2 = new RealtimeBattleManager(
                    new SimplePlayer1(),
                    new QLearningBot1(currentCache, extractor, 0.0, rng2),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt2.PlayBattleAsync();
                var r2 = rt2.GetBattleResult();
                if      (r2.Winner == "TeamB") Interlocked.Increment(ref wins);
                else if (r2.Winner == "TeamA") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);
            });

        var totalGames = matchups * 2;
        var simpleLine =
            $"[{DateTime.Now:HH:mm:ss}] vs SimplePlayer1:              {totalGames} games  " +
            $"W/L/D={wins}/{losses}/{draws}  winrate={wins / (double)totalGames:P1}";
        log.WriteLine(simpleLine);
        Console.WriteLine($"  [ep {episode}] vs SimplePlayer1:  W/L/D={wins}/{losses}/{draws}  winrate={wins / (double)totalGames:P1}");

        // vs Previous Snapshot
        wins = 0; losses = 0; draws = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, matchups),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (i, _) =>
            {
                var (teamA, teamB) = teamPairs[i];

                // Game 1: our bot as TeamA
                var rng1 = new Random(episode * 10000 + matchups * 2 + i);
                var rt1 = new RealtimeBattleManager(
                    new QLearningBot1(currentCache, extractor, 0.0, rng1),
                    new QLearningBot1(snapCache, extractor, 0.0, rng1),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt1.PlayBattleAsync();
                var r1 = rt1.GetBattleResult();
                if      (r1.Winner == "TeamA") Interlocked.Increment(ref wins);
                else if (r1.Winner == "TeamB") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);

                // Game 2: our bot as TeamB (swap sides)
                var rng2 = new Random(episode * 10000 + matchups * 3 + i);
                var rt2 = new RealtimeBattleManager(
                    new QLearningBot1(snapCache, extractor, 0.0, rng2),
                    new QLearningBot1(currentCache, extractor, 0.0, rng2),
                    teamA.DeepCopy(), teamB.DeepCopy());
                await rt2.PlayBattleAsync();
                var r2 = rt2.GetBattleResult();
                if      (r2.Winner == "TeamB") Interlocked.Increment(ref wins);
                else if (r2.Winner == "TeamA") Interlocked.Increment(ref losses);
                else                           Interlocked.Increment(ref draws);
            });

        var snapshotLine =
            $"[{DateTime.Now:HH:mm:ss}] vs Snapshot (from ep {snapshotEp}):  {totalGames} games  " +
            $"W/L/D={wins}/{losses}/{draws}  winrate={wins / (double)totalGames:P1}";
        log.WriteLine(snapshotLine);
        log.Flush();
        Console.WriteLine($"  [ep {episode}] vs Snapshot (ep {snapshotEp}): W/L/D={wins}/{losses}/{draws}  winrate={wins / (double)totalGames:P1}");
    }

    private static BattleResult StripLabels(BattleResult original, string teamPrefixToStrip)
    {
        var prefix = teamPrefixToStrip + "_";
        return new BattleResult
        {
            BattleId = original.BattleId,
            Winner = original.Winner,
            Actions = original.Actions.Select(a => new BattleAction
            {
                UnitName    = a.UnitName,
                UnitType    = a.UnitType,
                ActionType  = a.ActionType,
                Target      = a.Target,
                Amount      = a.Amount,
                Destination = a.Destination,
                Label       = (a.UnitName?.StartsWith(prefix) ?? false) ? null : a.Label,
            }).ToList()
        };
    }
}
