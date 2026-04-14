using System.Collections.Concurrent;
using System.Diagnostics;
using Arena.AI.Core;
using Arena.AI.QFolder;
using Arena.AI.Trainer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Arena.AI.Trainer.Training;

public static class TrainingLoop
{
    public static async Task RunAsync(IModelProfile profile, TrainingConfig config, TrainingRunFolder runFolder)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCtrl+C received — stopping after current batch...");
        };
        var ct = cts.Token;

        await using var trainingLog = new StreamWriter(runFolder.TrainingLogPath, append: false) { AutoFlush = false };
        await using var rawValidationLog = new StreamWriter(runFolder.ValidationLogPath, append: false) { AutoFlush = false };
        var validationLog = TextWriter.Synchronized(rawValidationLog);

        Console.WriteLine($"Run folder: {runFolder.RunDir}");
        runFolder.WriteConfig(config, profile.ModelName);

        await profile.InitializeAsync(runFolder.DbPath, trainingLog);

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services => profile.ConfigureServices(services))
            .Build();

        await host.StartAsync();

        var buffer = host.Services.GetRequiredService<QBattleResultBuffer>();
        var parallelism = Environment.ProcessorCount;
        int curriculumEpisodes = Math.Max(1, (int)(config.Episodes * config.CurriculumFraction));

        var header =
            $"Training [{profile.ModelName}]: episodes={config.Episodes} " +
            $"epsilon={config.StartEpsilon}->{config.FinalEpsilon} " +
            $"curriculum={config.StartSimpleProp:P0}->{config.FinalSimpleProp:P0} over {curriculumEpisodes} eps " +
            $"parallelism={parallelism} batch={config.BatchSize}";
        Console.WriteLine(header);
        trainingLog.WriteLine(header);
        trainingLog.Flush();

        int wins = 0, losses = 0, draws = 0;
        int prevValidationSnapshotEp = 0;
        var lastSnapshotTime = Stopwatch.GetTimestamp();
        var sw = Stopwatch.StartNew();

        var validationTasks = new List<Task>();
        var evalTablesToCleanUp = new ConcurrentBag<string>();
        int completedEps = 0;

        for (int batchStart = 1; batchStart <= config.Episodes && !ct.IsCancellationRequested; batchStart += config.BatchSize)
        {
            int batchEnd = Math.Min(batchStart + config.BatchSize - 1, config.Episodes);

            double batchEps = ComputeEpsilon(config, batchEnd);
            double batchSimpleProp = ComputeSimpleProp(config, curriculumEpisodes, batchEnd);

            int batchWins = 0, batchLosses = 0, batchDraws = 0;

            await Parallel.ForEachAsync(
                Enumerable.Range(batchStart, batchEnd - batchStart + 1),
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                async (ep, token) =>
                {
                    double eps = ComputeEpsilon(config, ep);
                    double simpleProp = ComputeSimpleProp(config, curriculumEpisodes, ep);

                    var localRng = new Random(42 + ep);
                    bool useSimple = localRng.NextDouble() < simpleProp;

                    IRealtimePlayer playerA = profile.CreateTrainingPlayer(eps, localRng);
                    IRealtimePlayer playerB = useSimple
                        ? profile.CreateBaselineOpponent()
                        : profile.CreateSnapshotPlayer(eps, localRng);

                    var rt = new RealtimeBattleManager(playerA, playerB);
                    await rt.PlayBattleAsync();
                    var result = rt.GetBattleResult();

                    if (!config.NoLearn)
                        profile.EnqueueResult(buffer, result, !useSimple);

                    if      (result.Winner == "TeamA") Interlocked.Increment(ref batchWins);
                    else if (result.Winner == "TeamB") Interlocked.Increment(ref batchLosses);
                    else                               Interlocked.Increment(ref batchDraws);
                });

            wins += batchWins;
            losses += batchLosses;
            draws += batchDraws;
            completedEps = batchEnd;

            if (Stopwatch.GetElapsedTime(lastSnapshotTime) >= config.SnapshotInterval)
            {
                await profile.UpdateSnapshotAsync();
                lastSnapshotTime = Stopwatch.GetTimestamp();
                trainingLog.WriteLine($"[{DateTime.Now:HH:mm:ss}] snapshot updated at ep {completedEps}");
            }

            var rate = completedEps / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            var logLine =
                $"[{DateTime.Now:HH:mm:ss}] ep {completedEps,6}/{config.Episodes}  ε={batchEps:F3}  " +
                $"simple%={batchSimpleProp:P0}  W/L/D={wins}/{losses}/{draws}  " +
                $"winrate={(double)wins / completedEps:P1}  rate={rate:F1} ep/s  buffer={buffer.Count}";
            trainingLog.WriteLine(logLine);
            trainingLog.Flush();
            Console.WriteLine(logLine);

            if (completedEps % config.ValidationInterval == 0)
            {
                await LaunchValidation(profile, config, runFolder, completedEps,
                    prevValidationSnapshotEp, wins, validationTasks, evalTablesToCleanUp, validationLog);

                await profile.SavePrevValidationSnapshotAsync();
                prevValidationSnapshotEp = completedEps;
            }
        }

        sw.Stop();
        var summary =
            $"[{DateTime.Now:HH:mm:ss}] Training{(ct.IsCancellationRequested ? " (interrupted)" : "")} " +
            $"done in {sw.Elapsed.TotalSeconds:F2}s. " +
            $"Final W/L/D = {wins}/{losses}/{draws}  " +
            $"winrate={(completedEps > 0 ? (double)wins / completedEps : 0):P1}";
        trainingLog.WriteLine(summary);
        trainingLog.Flush();
        Console.WriteLine(summary);

        await host.StopAsync();

        if (!ct.IsCancellationRequested && completedEps % config.ValidationInterval != 0)
        {
            Console.WriteLine("Running final validation...");
            await using var valContext = await profile.CreateValidationContextAsync(runFolder.DbPath, completedEps);
            foreach (var t in valContext.TablesToCleanUp)
                evalTablesToCleanUp.Add(t);

            await ValidationRunner.RunAsync(valContext, config.ValidationGames,
                completedEps, prevValidationSnapshotEp, validationLog);
        }

        await Task.WhenAll(validationTasks);
        await profile.DropEvalTablesAsync(evalTablesToCleanUp);

        validationLog.Flush();
        Console.WriteLine($"Done. See {runFolder.RunDir}/");
    }

    private static async Task LaunchValidation(
        IModelProfile profile, TrainingConfig config, TrainingRunFolder runFolder,
        int completedEps, int prevValidationSnapshotEp, int wins,
        List<Task> validationTasks, ConcurrentBag<string> evalTablesToCleanUp,
        TextWriter validationLog)
    {
        var valEp = completedEps;
        var valPrevEp = prevValidationSnapshotEp;
        var valWinrate = (double)wins / completedEps;

        var valContext = await profile.CreateValidationContextAsync(runFolder.DbPath, valEp);
        foreach (var t in valContext.TablesToCleanUp)
            evalTablesToCleanUp.Add(t);

        validationTasks.Add(Task.Run(async () =>
        {
            Console.WriteLine($"ep {valEp}/{config.Episodes}  winrate={valWinrate:P1}  — validation started");
            await using (valContext)
            {
                await ValidationRunner.RunAsync(valContext, config.ValidationGames,
                    valEp, valPrevEp, validationLog);
            }
        }));
    }

    private static double ComputeEpsilon(TrainingConfig config, int ep)
    {
        var t = (double)(ep - 1) / Math.Max(1, config.Episodes - 1);
        // Exponential decay: spend more time exploring early, then transition sharply to exploitation.
        // decay factor 5 means ~99% of the drop happens by ~60% of training.
        return config.FinalEpsilon
            + (config.StartEpsilon - config.FinalEpsilon) * Math.Exp(-5.0 * t);
    }

    private static double ComputeSimpleProp(TrainingConfig config, int curriculumEpisodes, int ep)
        => config.StartSimpleProp + (config.FinalSimpleProp - config.StartSimpleProp)
            * Math.Min(1.0, (double)(ep - 1) / Math.Max(1, curriculumEpisodes - 1));
}
