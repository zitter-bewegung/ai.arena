using System.Globalization;
using Arena.AI.Core.Models;
using Arena.AI.QFolder;

namespace Arena.AI.Trainer.Models.Scout;

public class TableSchema : ITableSchema<QStateAction>
{
    public string ColumnDefinitionsSql => """
        actor_unit_type        INTEGER NOT NULL,
        actor_health_level     INTEGER NOT NULL,
        number_of_teammates    TINYINT NOT NULL,
        number_of_enemies      TINYINT NOT NULL,
        distance_to_weakest    INTEGER NOT NULL,
        health_of_weakest      INTEGER NOT NULL,
        distance_to_closest    INTEGER NOT NULL,
        health_of_closest      INTEGER NOT NULL,
        distance_average       INTEGER NOT NULL,
        zone                   INTEGER NOT NULL,
        distance_from_center   INTEGER NOT NULL,
        allies_nearby          INTEGER NOT NULL,
        enemy_threat           INTEGER NOT NULL,
        action                 INTEGER NOT NULL
        """;

    public string KeyColumnsCsv =>
        "actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies, " +
        "distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest, " +
        "distance_average, zone, distance_from_center, allies_nearby, enemy_threat, action";

    public string AllColumnsCsv => $"{KeyColumnsCsv}, reward";

    public string ToWhereClause(QStateAction sa) =>
        $"""
        actor_unit_type = {(int)sa.ActorUnitType}
          AND actor_health_level = {(int)sa.ActorHealthLevel}
          AND number_of_teammates = {sa.NumberOfTeammates}
          AND number_of_enemies = {sa.NumberOfEnemies}
          AND distance_to_weakest = {(int)sa.DistanceToWeakest}
          AND health_of_weakest = {(int)sa.HealthOfWeakest}
          AND distance_to_closest = {(int)sa.DistanceToClosest}
          AND health_of_closest = {(int)sa.HealthOfClosest}
          AND distance_average = {(int)sa.DistanceAverage}
          AND zone = {(int)sa.Zone}
          AND distance_from_center = {(int)sa.DistanceFromCenter}
          AND allies_nearby = {(int)sa.AlliesNearby}
          AND enemy_threat = {(int)sa.EnemyThreat}
          AND action = {(int)sa.Action!.Value}
        """;

    public string ToStateWhereClause(QStateAction sa) =>
        $"""
        actor_unit_type = {(int)sa.ActorUnitType}
          AND actor_health_level = {(int)sa.ActorHealthLevel}
          AND number_of_teammates = {sa.NumberOfTeammates}
          AND number_of_enemies = {sa.NumberOfEnemies}
          AND distance_to_weakest = {(int)sa.DistanceToWeakest}
          AND health_of_weakest = {(int)sa.HealthOfWeakest}
          AND distance_to_closest = {(int)sa.DistanceToClosest}
          AND health_of_closest = {(int)sa.HealthOfClosest}
          AND distance_average = {(int)sa.DistanceAverage}
          AND zone = {(int)sa.Zone}
          AND distance_from_center = {(int)sa.DistanceFromCenter}
          AND allies_nearby = {(int)sa.AlliesNearby}
          AND enemy_threat = {(int)sa.EnemyThreat}
        """;

    public string ToValuesTuple(QStateAction sa, double reward) =>
        $"({(int)sa.ActorUnitType}, {(int)sa.ActorHealthLevel}, {sa.NumberOfTeammates}, {sa.NumberOfEnemies}, {(int)sa.DistanceToWeakest}, {(int)sa.HealthOfWeakest}, {(int)sa.DistanceToClosest}, {(int)sa.HealthOfClosest}, {(int)sa.DistanceAverage}, {(int)sa.Zone}, {(int)sa.DistanceFromCenter}, {(int)sa.AlliesNearby}, {(int)sa.EnemyThreat}, {(int)sa.Action!.Value}, {reward.ToString(CultureInfo.InvariantCulture)})";

    public string ToValuesTupleWithAlpha(QStateAction sa, double reward, double effectiveAlpha) =>
        $"({(int)sa.ActorUnitType}, {(int)sa.ActorHealthLevel}, {sa.NumberOfTeammates}, {sa.NumberOfEnemies}, {(int)sa.DistanceToWeakest}, {(int)sa.HealthOfWeakest}, {(int)sa.DistanceToClosest}, {(int)sa.HealthOfClosest}, {(int)sa.DistanceAverage}, {(int)sa.Zone}, {(int)sa.DistanceFromCenter}, {(int)sa.AlliesNearby}, {(int)sa.EnemyThreat}, {(int)sa.Action!.Value}, {reward.ToString(CultureInfo.InvariantCulture)}, {effectiveAlpha.ToString(CultureInfo.InvariantCulture)})";

    public (QStateAction StateAction, double Reward) FromRow(dynamic row) =>
    (
        new QStateAction
        {
            ActorUnitType      = (UnitType)Convert.ToInt32(row.actor_unit_type),
            ActorHealthLevel   = (UnitHealthLevel)Convert.ToInt32(row.actor_health_level),
            NumberOfTeammates  = Convert.ToByte(row.number_of_teammates),
            NumberOfEnemies    = Convert.ToByte(row.number_of_enemies),
            DistanceToWeakest  = (DistanceLevel)Convert.ToInt32(row.distance_to_weakest),
            HealthOfWeakest    = (UnitHealthLevel)Convert.ToInt32(row.health_of_weakest),
            DistanceToClosest  = (DistanceLevel)Convert.ToInt32(row.distance_to_closest),
            HealthOfClosest    = (UnitHealthLevel)Convert.ToInt32(row.health_of_closest),
            DistanceAverage    = (DistanceLevel)Convert.ToInt32(row.distance_average),
            Zone               = (ActorZone)Convert.ToInt32(row.zone),
            DistanceFromCenter = (CenterDistance)Convert.ToInt32(row.distance_from_center),
            AlliesNearby       = (NearbyCount)Convert.ToInt32(row.allies_nearby),
            EnemyThreat        = (ThreatLevel)Convert.ToInt32(row.enemy_threat),
            Action             = (QAction)Convert.ToInt32(row.action),
        },
        Convert.ToDouble(row.reward)
    );
}
