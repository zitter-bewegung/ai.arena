using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core.QStorage;

public class QRecordManager<TQStateAction> where TQStateAction : QStateAction
{
    private readonly IQRepository<TQStateAction> _repository;
    private readonly IQRecordsExtractor<TQStateAction> _recordsExtractor;

    public QRecordManager(
        IQRepository<TQStateAction> repository,
        IQRecordsExtractor<TQStateAction> recordsExtractor)
    {
        _repository = repository;
        _recordsExtractor=recordsExtractor;
    }

    public async Task ProcessBattleResultsAsync(IEnumerable<BattleResult> battleResults)
    {
        var records = _recordsExtractor.ExtractRecords(battleResults)!;
        await _repository.SaveRecordsAsync(records);
    }

    public async Task<double> GetRewardAsync(BattleState battleState)
    {
        var qRecord = _recordsExtractor.ExtractState(battleState);
        return await _repository.GetRewardAsync(qRecord);
    }
}
