using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class FeatureTransformer
{
    const int mapSize = 20;
    const int macroGrid = 4;
    const int cellSize = mapSize / macroGrid;

    public static BattleResult Transform(BattleResult battle)
    {
        foreach (var action in battle.Actions)
        {
            if (!string.IsNullOrEmpty(action.Destination))
            {
                action.Destination = ConvertPosition(action.Destination);
            }

            if (action.Amount != null)
            {
                action.Amount = CategorizeHp(action.Amount.Value);
            }
        }

        return battle;
    }

    static string ConvertPosition(string position)
    {
        // A3 → координати
        int x = position[0] - 'A';
        int y = int.Parse(position.Substring(1));

        int gridX = x / cellSize;
        int gridY = y / cellSize;

        return $"G{gridX}{gridY}";
    }

    static int CategorizeHp(int hp)
    {
        if (hp <= 3)
            return 0; // low

        if (hp <= 6)
            return 1; // medium

        return 2; // high
    }
}