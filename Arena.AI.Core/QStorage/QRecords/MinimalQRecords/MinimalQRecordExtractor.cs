using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

public class MinimalQRecordExtractor : IQRecordsExtractor<MinimalQStateAction>
{
    public MinimalQStateAction ExtractState(BattleState battleState)
    {
        var nextUnitInfo = battleState.NextUnitInfo;
        var actorTeam = battleState.TeamA.Name == nextUnitInfo.TeamName ? battleState.TeamA : battleState.TeamB;
        var enemyTeam = battleState.TeamA.Name == nextUnitInfo.TeamName ? battleState.TeamB : battleState.TeamA;

        var enemiesData = CalculateEnemies(nextUnitInfo.Unit, enemyTeam);

        return new MinimalQStateAction
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

    public IEnumerable<QRecord<MinimalQStateAction>> ExtractRecords(BattleResult battleResult)
    {
        var historicalStates = battleResult.ExtractHistory();

        var result = new List<QRecord<MinimalQStateAction>>(historicalStates.Count);
        var myTeamName = historicalStates.First(h => !string.IsNullOrWhiteSpace(h.ActorAction.Label)).ActorTeam.Split("_")[0];

        Func<BattleHistory, Team> enemy = h => h.TeamA.Name == myTeamName ? h.TeamB : h.TeamA;

        var finalEnemiesCount = battleResult.Winner == myTeamName ? 0 : enemy(historicalStates.Last()).AliveUnits.Length;

        for(var i = 0; i < historicalStates.Count; )
        {
            if(historicalStates[i].ActorTeam != myTeamName || !string.IsNullOrEmpty(historicalStates[i].ActorAction.Label))
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

            state.Action = (MinimalQAction)Enum.Parse(typeof(MinimalQAction), currentActionLabel);

            var record = new QRecord<MinimalQStateAction>
            {
                StateAction = state,
                NumberOfKills = enemy(historicalStates[i]).AliveUnits.Length - finalEnemiesCount,
                NumberOfGames = 1
            };

            result.Add(record);

            i++;

            while (i < historicalStates.Count && historicalStates[i].Actor.Name == currentActorName && historicalStates[i].ActorAction.Label == currentActionLabel)
            {
                i++;
            }
        }

        return result;
    }

    private static UnitHealthLevel GetUnitHealthLevel(int health)
    {
        if (health > 6)
        {
            return UnitHealthLevel.High;
        }

        if (health > 3)
        {
            return UnitHealthLevel.Medium;
        }

        return UnitHealthLevel.Low;
    }

    private static EnemyCalculations CalculateEnemies(Unit actor, Team enemy)
    {
        var enemiesDistances = enemy.AliveUnits
            .ToDictionary(u => u.Name, u => DistanceCalculator.GetShortestDistanceValue(actor, u));

        var weakest = enemy.AliveUnits
            .OrderBy(u => u.Health)
            .ThenBy(u => enemiesDistances[u.Name])
            .First();

        var closest = enemy.AliveUnits
            .OrderBy(u => enemiesDistances[u.Name])
            .First();

        return new EnemyCalculations
        {
            DistanceToWeakest = GetDistanceLevel(actor, enemiesDistances[weakest.Name]),
            HealthOfWeakest = GetUnitHealthLevel(weakest.Health),
            DistanceToClosest = GetDistanceLevel(actor, enemiesDistances[closest.Name]),
            HealthOfClosest = GetUnitHealthLevel(closest.Health),
            DistanceAverage = GetDistanceLevel(actor, enemiesDistances.Values.Average())
        };
    }

    private static DistanceLevel GetDistanceLevel(Unit actor, double distance)
    {
        var normalizedDistance = distance / (actor.Movement + actor.Range);
        var normalizedAttackDistance = distance / (actor.Range);

        if(normalizedAttackDistance < 1)
        {
            return DistanceLevel.AttackRange;
        }

        if (normalizedDistance < 1)
        {
            return DistanceLevel.MoveAndAttackRange;
        }

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

