using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class DamageCalculationsV0
{
    private static int BaseDamage = Constants.UnitMaxHealth / 2;
    private static int StatFactor = 100;
    private static double RandomModifier = 0.2;
    private static Random rnd = new Random();

    public static int CalculateDamage(Unit attacker, Unit target)
    {
        var skew = attacker.Attack - target.Defence;

        var randomFactor = 1 + (rnd.NextDouble() - 0.5) * RandomModifier;

        var damage = BaseDamage * (1 + skew /StatFactor) * randomFactor;

        return (int)Math.Floor(damage);
    }
}
