using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic;

public static class DistanceCalculator
{
    public const double DiagonalPenalty = 1.5;
    private static Random rnd = new ();


    public static Unit[] CanAttackWithoutMoving(Unit attacker, Team enemyTeam)
        => enemyTeam.AliveUnits.Where(u => CanAttackWithoutMoving(attacker, u)).ToArray();

    public static bool CanAttackWithoutMoving(Unit attacker, Unit target)
    {
        if (IsNear(attacker, target))
        {
            return true; 
        };

        if(GetShortestDistanceValue(attacker, target) <= attacker.Range + (DiagonalPenalty - 1))
        {
            return true;
        }

        return false;
    }

    public static bool MoveAttackerToAttackTarget(Unit attacker, Unit target)
    {
        if (CanAttackWithoutMoving(attacker, target))
        {
            return false;
        }

        while(!CanAttackWithoutMoving(attacker, target))
        {
            attacker.XPosition += Step(attacker.XPosition, target.XPosition);
            attacker.YPosition += Step(attacker.YPosition, target.YPosition);
        }

        return true;
    }

    public static void MoveAttackerCloserToTarget(Unit attacker, Unit target)
    {
        double baseMovement = 1.0;

        var moveSteps = Math.Ceiling(rnd.Next(attacker.Movement) * (1-baseMovement) + baseMovement * attacker.Movement);

        while(moveSteps >= DiagonalPenalty)
        {
            var xStep = Step(attacker.XPosition, target.XPosition);
            var yStep = Step(attacker.YPosition, target.YPosition);

            attacker.XPosition += xStep;
            attacker.YPosition += yStep;

            moveSteps -= Math.Sqrt(xStep*xStep + yStep * yStep);

            if(CanAttackWithMovement(attacker, target))
            {
                return;
            }
        }

        if(Math.Abs(moveSteps - 1) < 0.1)
        {
            if(Math.Abs(attacker.XPosition - target.XPosition) > Math.Abs(attacker.YPosition - target.YPosition))
            {
                attacker.XPosition += attacker.XPosition > target.XPosition ? -1 : 1;
            }
            else
            {
                attacker.YPosition += attacker.YPosition > target.YPosition ? -1 : 1;
            }

        }
    }

    public static void Move(Unit unit, int x, int y)
    {
        unit.XPosition = x;
        unit.YPosition = y;
    }

    private static int Step(int attackerDim, int targetDim)
    {
        if(attackerDim > targetDim)
        {
            return -1;
        }

        if (attackerDim < targetDim)
        {
            return 1;
        }

        return 0;
    }

    public static bool CanAttackWithMovement(Unit attacker, Unit target)
    {
        if(GetShortestDistanceValue(attacker, target) <= attacker.Range + attacker.Movement + (DiagonalPenalty - 1))
        {
            return true;
        }

        return false;
    }

    public static bool IsNear(Unit attacker, Unit target) 
        => GetShortestDistanceValue(attacker, target) <= DiagonalPenalty;

    public static double GetShortestDistanceValue(Unit unit1, Unit unit2)
        => GetShortestDistanceValue(new Coordinate(unit1.XPosition, unit1.YPosition), new Coordinate(unit2.XPosition, unit2.YPosition));

    public static double GetShortestDistanceValue(Unit unit1, int x, int y)
    => GetShortestDistanceValue(new Coordinate(unit1.XPosition, unit1.YPosition), new Coordinate(x, y));

    private static double GetShortestDistanceValue(Coordinate unit1, Coordinate unit2)
    {
        var deltaX = Math.Abs(unit1.X - unit2.X);
        var deltaY = Math.Abs(unit1.Y - unit2.Y);

        var diagonalSteps = Math.Min(deltaX, deltaY);
        var linearSteps = Math.Max(deltaX, deltaY) - diagonalSteps;

        return diagonalSteps * DiagonalPenalty + linearSteps;
    }
}

public record struct Coordinate(int X, int Y);
