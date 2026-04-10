using Arena.AI.Core.Logic;

namespace Arena.AI.Core.Models;

public class BattleHistory
{
    public Unit Actor { get; set; }
    public string ActorTeam { get; set; }
    public UserAction ActorAction { get; set; }
    public Team TeamA { get; set; }
    public Team TeamB { get; set; }
    public string Winner { get; set; }
}

public static class BattleStateHistoricalExtractor
{
    public static List<BattleHistory> ExtractHistory(this BattleResult battleResult)
    {
        var history = new List<BattleHistory>(battleResult.Actions.Count);
        var teams = ParseTeams(battleResult.Actions);
        var teamA = teams.Item1;
        var teamB = teams.Item2;
        var aliveUnits = teamA.AliveUnits.Union(teamB.AliveUnits);

        var actions = battleResult.Actions.Where(a => a.ActionType != BattleActionType.Appears).ToArray();

        for(var i = 0; i < actions.Length; )
        {
            var actor = aliveUnits.First(u => u.Name == actions[i].UnitName);

            if(actions[i].ActionType == BattleActionType.SkipsTurn)
            {
                history.Add(new BattleHistory
                {
                    Actor = actor.DeepCopy(),
                    ActorTeam = actions[i].UnitName.Split("_")[0],
                    ActorAction = UserAction.Skip(actions[i].Label),
                    TeamA = teamA.DeepCopy(),
                    TeamB = teamB.DeepCopy(),
                    Winner = battleResult.Winner
                });

                i++;
                continue;
            }
            else if(actions[i].ActionType == BattleActionType.Moves)
            {
                history.Add(new BattleHistory
                {
                    Actor = actor.DeepCopy(),
                    ActorTeam = actions[i].UnitName.Split("_")[0],
                    ActorAction = UserAction.Move(actions[i].Destination, actions[i].Label),
                    TeamA = teamA.DeepCopy(),
                    TeamB = teamB.DeepCopy(),
                    Winner = battleResult.Winner
                });

                NumberLetterConverter.TryParseDestination(actions[i].Destination, out var dest);
                actor.XPosition = dest.Item1;
                actor.YPosition = dest.Item2;

                if(actions[i].UnitName != actions[i+1].UnitName)
                {
                    // it means that unit moved without attacking
                    i++;
                    continue;
                }
                else
                {
                    // it means that unit moved and attacked
                    // i+1: actor attacks
                    history.Add(new BattleHistory
                    {
                        Actor = actor.DeepCopy(),
                        ActorTeam = actions[i].UnitName.Split("_")[0],
                        ActorAction = UserAction.Attack(actions[i+1].Target, actions[i+1].Label),
                        TeamA = teamA.DeepCopy(),
                        TeamB = teamB.DeepCopy(),
                        Winner = battleResult.Winner
                    });
                    // i+2: target looses health
                    var target = aliveUnits.First(u => u.Name == actions[i+2].UnitName);
                    target.Health -= actions[i+2].Amount.Value;

                    if(actions[i+3].ActionType == BattleActionType.Dies)
                    {
                        target.Health = 0;
                        i = i+3+1;
                        continue;
                    }
                    else if(actions[i+3].ActionType == BattleActionType.Attacks && actions[i+3].Target == actor.Name)
                    {
                        // i+3: target attacks back
                        // i+4: actor looses health
                        actor.Health -= actions[i+4].Amount.Value;

                        if(actions[i+5].ActionType == BattleActionType.Dies)
                        {
                            actor.Health = 0;
                            i = i+5+1;
                            continue;
                        }
                        else
                        {
                            i = i+4+1;
                            continue;
                        }
                    }
                    else
                    {
                        i = i+2+1;
                        continue;
                    }
                }
            }
            else
            {
                // i: actor attacks
                history.Add(new BattleHistory
                {
                    Actor = actor.DeepCopy(),
                    ActorTeam = actions[i].UnitName.Split("_")[0],
                    ActorAction = UserAction.Attack(actions[i].Target, actions[i].Label),
                    TeamA = teamA.DeepCopy(),
                    TeamB = teamB.DeepCopy(),
                    Winner = battleResult.Winner
                });

                // i+1: target looses health
                var target = aliveUnits.First(u => u.Name == actions[i+1].UnitName);
                target.Health -= actions[i+1].Amount.Value;

                if(actions[i+2].ActionType == BattleActionType.Dies)
                {
                    target.Health = 0;
                    i = i+2+1;
                    continue;
                }
                else if(actions[i+2].ActionType == BattleActionType.Attacks && actions[i+2].Target == actor.Name)
                {
                    // i+2: target attacks back
                    // i+3: actor looses health
                    actor.Health -= actions[i+3].Amount.Value;

                    if(actions[i+4].ActionType == BattleActionType.Dies)
                    {
                        actor.Health = 0;
                        i = i+4+1;
                        continue;
                    }
                    else
                    {
                        i = i+3+1;
                        continue;
                    }
                }
                else
                {
                    i = i+1+1;
                    continue;
                }
            }
        }

        return history;
    }

    private static (Team, Team) ParseTeams(List<BattleAction> actions)
    {
        var appearanceActions = actions.Where(a => a.ActionType == BattleActionType.Appears).ToArray();
        var teamNames = appearanceActions.Select(a => a.UnitName.Split("_")[0]).Distinct().ToArray();

        return (ParseTeam(teamNames[0], appearanceActions), ParseTeam(teamNames[1], appearanceActions));
    }

    private static Team ParseTeam(string teamName, BattleAction[] appearsActions)
    {
        var teamActions = appearsActions.Where(a => a.UnitName.StartsWith(teamName)).ToArray();

        var units = teamActions.Select(a => {
            var unit = UnitFactory.GetUnit(a.UnitType, a.UnitName);
            unit.Health = Constants.UnitMaxHealth;

            NumberLetterConverter.TryParseDestination(a.Destination, out var dest);
            unit.XPosition = dest.Item1;
            unit.YPosition = dest.Item2;

            return unit;
        }).ToArray();

        return new Team
        {
            Name = teamName,
            Units = units
        };
    }
}