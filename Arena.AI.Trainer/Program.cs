using System.Globalization;
using Arena.AI.Trainer.Eval;
using Arena.AI.Trainer.Models;
using Arena.AI.Trainer.Training;

// --- Eval mode ---
if (ArgFlag(args, "--eval"))
{
    var games = ArgInt(args, "--games", 100);

    var specA = BuildPlayerSpec(
        runArg: "--run", modelArg: "--model", dbArg: "--db", defaultModel: "dwarf",
        required: true, args);

    var specB = ArgHas(args, "--opponent-run") || ArgHas(args, "--opponent-db")
        ? BuildPlayerSpec(
            runArg: "--opponent-run", modelArg: "--opponent-model", dbArg: "--opponent-db",
            defaultModel: "dwarf", required: true, args)
        : new EvalPlayerSpec(ArgString(args, "--opponent", "simple"), null, null);

    var evalFolder = new EvalRunFolder();

    await using var factoryA = await EvalPlayerFactory.CreateAsync(specA);
    await using var factoryB = await EvalPlayerFactory.CreateAsync(specB);

    await EvalRunner.RunAsync(factoryA, factoryB, games, evalFolder);
    return;
}

// --- Training mode ---
var modelName = ArgString(args, "--model", "dwarf");
var profile = ModelRegistry.Create(modelName);

var config = profile.DefaultConfig with
{
    Episodes = ArgInt(args, "--episodes", profile.DefaultConfig.Episodes),
    StartEpsilon = ArgDouble(args, "--epsilon", profile.DefaultConfig.StartEpsilon),
    FinalEpsilon = ArgDouble(args, "--final-epsilon", profile.DefaultConfig.FinalEpsilon),
};

var explicitDb = ArgHas(args, "--db") ? ArgString(args, "--db", "") : null;
var runFolder = new TrainingRunFolder(modelName, explicitDb);

await using (profile)
{
    await TrainingLoop.RunAsync(profile, config, runFolder);
}

// --- Helpers ---

// Builds an EvalPlayerSpec. If --run is given, resolves to runs/{run}/model.db
// and extracts model name from folder prefix (e.g. "dwarf_20260414" → "dwarf").
// Otherwise falls back to --model + --db.
static EvalPlayerSpec BuildPlayerSpec(
    string runArg, string modelArg, string dbArg,
    string defaultModel, bool required, string[] args)
{
    if (ArgHas(args, runArg))
    {
        var runName = ArgString(args, runArg, "");
        var runDir = Path.Combine("runs", runName);
        if (!Directory.Exists(runDir))
            throw new DirectoryNotFoundException($"Run folder not found: {runDir}");

        var dbPath = Path.Combine(runDir, "model.db");
        var modelFromFolder = runName.Split('_')[0]; // "dwarf_20260414_..." → "dwarf"
        var model = ArgString(args, modelArg, modelFromFolder);
        return new EvalPlayerSpec("model", model, dbPath);
    }

    return new EvalPlayerSpec(
        "model",
        ArgString(args, modelArg, defaultModel),
        ArgString(args, dbArg, ""));
}

static int ArgInt(string[] a, string k, int d)
    => int.TryParse(ArgString(a, k, d.ToString(CultureInfo.InvariantCulture)),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : d;

static double ArgDouble(string[] a, string k, double d)
    => double.TryParse(ArgString(a, k, d.ToString(CultureInfo.InvariantCulture)),
                       NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

static string ArgString(string[] a, string k, string d)
{
    var i = Array.IndexOf(a, k);
    return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : d;
}

static bool ArgFlag(string[] a, string k) => Array.IndexOf(a, k) >= 0;
static bool ArgHas(string[] a, string k) => a.Contains(k) && Array.IndexOf(a, k) + 1 < a.Length;
