using System.Globalization;
using Arena.AI.Trainer;

int episodes    = ArgInt(args, "--episodes", 2000);
double startEps = ArgDouble(args, "--epsilon", 0.3);
double finalEps = ArgDouble(args, "--final-epsilon", 0.05);
string dbPath   = ArgString(args, "--db", "battles.db");
bool noLearn    = ArgFlag(args, "--no-learn");

await DuckDbTrainer.RunAsync(episodes, startEps, finalEps, dbPath, noLearn);

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
