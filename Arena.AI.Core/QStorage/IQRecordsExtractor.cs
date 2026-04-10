using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.QStorage;

public interface IQRecordsExtractor<TQStateAction> where TQStateAction : QStateAction
{
    IEnumerable<QRecord<TQStateAction>> ExtractRecords(BattleResult battleResult);
    IEnumerable<QRecord<TQStateAction>> ExtractRecords(IEnumerable<BattleResult> battleResults) 
        => battleResults
        .SelectMany(ExtractRecords)
        .GroupBy(x => x.StateAction)
        .Select(g => new QRecord<TQStateAction>
        {
            StateAction = g.Key,
            NumberOfKills = g.Sum(x => x.NumberOfKills),
            NumberOfGames = g.Sum(x => x.NumberOfGames)
        })
        .ToArray();

    TQStateAction ExtractState(BattleState battleState);
}
