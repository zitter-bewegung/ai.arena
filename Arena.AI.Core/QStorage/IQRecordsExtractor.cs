using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.QStorage;

public interface IQRecordsExtractor<TQStateAction> where TQStateAction : QStateAction
{
    IEnumerable<QRecord<TQStateAction>> ExtractRecords(BattleResult battleResult);
    IEnumerable<QRecord<TQStateAction>> ExtractRecords(IEnumerable<BattleResult> battleResults)
        => battleResults.SelectMany(ExtractRecords);

    TQStateAction ExtractState(BattleState battleState);
}
