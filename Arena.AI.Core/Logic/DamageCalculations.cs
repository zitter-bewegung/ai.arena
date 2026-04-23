using Arena.AI.Core.Models;
using System;

namespace Arena.AI.Core.Logic
{
    public static class DamageCalculations
    {
        private static Random rnd = new Random();

        public static int CalculateDamage(Unit attacker, Unit target)
        {
            int attack = attacker.Attack;
            int defence = target.Defence;

            int attackerType = (int)attacker.Type;
            int targetType = (int)target.Type;

            // 1. Баланс "Light vs Heavy"
            if (attackerType == 0 && targetType == 1)
            {
                if (rnd.NextDouble() < 0.10)
                {
                    attack += 1;
                }
            }

            // 2. Баланс "Fast vs Light"
            if (attackerType == 0 && targetType == 2)
            {
                if (rnd.NextDouble() < 0.03)
                {
                    return 0;
                }
            }

            // 3. Баланс "Heavy vs Fast" 
            if (attackerType == 1 && targetType == 2)
            {
                if (rnd.NextDouble() < 0.05)
                {
                    attack += 1;
                }
            }

            // Логіка для ShortRange
            if (attackerType == 3)
            {
                switch (targetType)
                {
                    case 1: // Heavy 
                        if (rnd.NextDouble() < 0.03) defence -= 3;
                        break;

                    case 0: // Light
                        if (rnd.NextDouble() < 0.07) defence -= 2;
                        break;

                    case 2: // Fast
                        if (rnd.NextDouble() < 0.85) defence -= 1;
                        break;

                    case 4: // Long Range
                        if (rnd.NextDouble() < 0.75) defence += 1;
                        break;
                }
            }

            // Логіка для LongRange
            if (attackerType == 4)
            {
                switch (targetType)
                {
                    case 1: // Heavy
                        defence += 6;
                        break;

                    case 0: // Light
                        if (rnd.NextDouble() < 0.75) defence -= 2;
                        break;

                    case 2: // Fast
                        if (rnd.NextDouble() < 0.72) defence -= 2;
                        break;
                }
            }

            // Метод розрахунку шкоди
            int damage = attack - defence;
            if (damage < 1) damage = 1;

            // Рандомний множник
            double randomMultiplier = 0.9 + (rnd.NextDouble() * 0.2);
            int finalDamage = (int)(damage * randomMultiplier);
            return finalDamage < 1 ? 1 : finalDamage;
        }
    }
}