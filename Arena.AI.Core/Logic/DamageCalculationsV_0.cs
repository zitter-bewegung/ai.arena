using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class DamageCalculationsV0
{
    private static double BaseDamage = Constants.UnitMaxHealth / 3.5;
    private static int StatFactor = 50;
    private static double RandomModifier = 0.1;
    private static Random rnd = new Random();

    public static int CalculateDamage(Unit attacker, Unit target)
    {
        var skew = attacker.Attack - target.Defence;

        var randomFactor = 1.0 + (rnd.NextDouble() - 0.5) * RandomModifier * 2;

        var damage = BaseDamage * (1 + 1.0*skew /StatFactor) * randomFactor;

        return (int)Math.Floor(damage);
    }
}
