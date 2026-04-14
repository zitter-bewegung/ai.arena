using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;

namespace Arena.AI.Trainer.Models.Dwarf;

public class RecordExtractor : IQRecordsExtractor<QStateAction>
{
    private const double Gamma = 0.95;

    public QStateAction ExtractState(BattleState battleState)
    {
        var nextUnitInfo = battleState.NextUnitInfo;
        var actorTeam = battleState.TeamA.Name == nextUnitInfo.TeamName ? battleState.TeamA : battleState.TeamB;
        var enemyTeam = battleState.TeamA.Name == nextUnitInfo.TeamName ? battleState.TeamB : battleState.TeamA;

        var enemiesData = CalculateEnemies(nextUnitInfo.Unit, enemyTeam);

        return new QStateAction
        {
            ActorUnitType = nextUnitInfo.Unit.Type,
            ActorHealthLevel = GetUnitHealthLevel(nextUnitInfo.Unit.Health),
            NumberOfTeammates = (byte)actorTeam.AliveUnits.Length,
            NumberOfEnemies = (byte)enemyTeam.AliveUnits.Length,
            DistanceToWeakest = enemiesData.DistanceToWeakest,
            HealthOfWeakest = enemiesData.HealthOfWeakest,
            DistanceToClosest = enemiesData.DistanceToClosest,
            HealthOfClosest = enemiesData.HealthOfClosest,
            DistanceAverage = enemiesData.DistanceAverage,
        };
    }

    public IEnumerable<QRecord<QStateAction>> ExtractRecords(BattleResult battleResult)
    {
        var historicalStates = battleResult.ExtractHistory();
        var result = new List<QRecord<QStateAction>>(historicalStates.Count);
        var myTeamName = historicalStates.First(h => !string.IsNullOrWhiteSpace(h.ActorAction.Label)).ActorTeam.Split("_")[0];

        Func<BattleHistory, Team> enemy = h => h.TeamA.Name == myTeamName ? h.TeamB : h.TeamA;

        var killSteps = new List<(int index, int killCount)>();
        for (int j = 1; j < historicalStates.Count; j++)
        {
            int prevAlive = enemy(historicalStates[j - 1]).AliveUnits.Length;
            int currAlive = enemy(historicalStates[j]).AliveUnits.Length;
            if (currAlive < prevAlive)
                killSteps.Add((j, prevAlive - currAlive));
        }

        for (var i = 0; i < historicalStates.Count;)
        {
            if (historicalStates[i].ActorTeam != myTeamName || string.IsNullOrEmpty(historicalStates[i].ActorAction.Label))
            {
                i++;
                continue;
            }

            var state = ExtractState(new BattleState
            {
                TeamA = historicalStates[i].TeamA,
                TeamB = historicalStates[i].TeamB,
                NextUnitInfo = new NextUnitInfo
                {
                    Unit = historicalStates[i].Actor,
                    TeamName = historicalStates[i].ActorTeam
                }
            });

            var currentActionLabel = historicalStates[i].ActorAction.Label;
            var currentActorName = historicalStates[i].Actor.Name;
            state.Action = (QAction)Enum.Parse(typeof(QAction), currentActionLabel);

            double discountedReward = 0;
            foreach (var (killIndex, killCount) in killSteps)
            {
                if (killIndex > i)
                    discountedReward += killCount * Math.Pow(Gamma, killIndex - i);
            }

            result.Add(new QRecord<QStateAction>
            {
                StateAction = state,
                Reward = discountedReward,
            });

            i++;
            while (i < historicalStates.Count && historicalStates[i].Actor.Name == currentActorName && historicalStates[i].ActorAction.Label == currentActionLabel)
                i++;
        }

        return result;
    }

    private static UnitHealthLevel GetUnitHealthLevel(int health)
    {
        if (health > 6) return UnitHealthLevel.High;
        if (health > 3) return UnitHealthLevel.Medium;
        return UnitHealthLevel.Low;
    }

    private static EnemyCalculations CalculateEnemies(Unit actor, Team enemy)
    {
        var distances = enemy.AliveUnits
            .ToDictionary(u => u.Name, u => DistanceCalculator.GetShortestDistanceValue(actor, u));
        var weakest = enemy.AliveUnits.OrderBy(u => u.Health).ThenBy(u => distances[u.Name]).First();
        var closest = enemy.AliveUnits.OrderBy(u => distances[u.Name]).First();

        return new EnemyCalculations
        {
            DistanceToWeakest = GetDistanceLevel(actor, distances[weakest.Name]),
            HealthOfWeakest = GetUnitHealthLevel(weakest.Health),
            DistanceToClosest = GetDistanceLevel(actor, distances[closest.Name]),
            HealthOfClosest = GetUnitHealthLevel(closest.Health),
            DistanceAverage = GetDistanceLevel(actor, distances.Values.Average())
        };
    }

    private static DistanceLevel GetDistanceLevel(Unit actor, double distance)
    {
        if (distance / actor.Range < 1) return DistanceLevel.AttackRange;
        if (distance / (actor.Movement + actor.Range) < 1) return DistanceLevel.MoveAndAttackRange;
        return DistanceLevel.CannotAttack;
    }

    private class EnemyCalculations
    {
        public DistanceLevel DistanceToWeakest { get; set; }
        public UnitHealthLevel HealthOfWeakest { get; set; }
        public DistanceLevel DistanceToClosest { get; set; }
        public UnitHealthLevel HealthOfClosest { get; set; }
        public DistanceLevel DistanceAverage { get; set; }
    }
}
