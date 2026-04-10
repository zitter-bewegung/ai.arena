namespace Arena.AI.Core.QStorage;

public interface IQRepository<TQStateAction> where TQStateAction : QStateAction
{
    Task<double> GetRewardAsync(TQStateAction record);
    Task SaveRecordsAsync(IEnumerable<QRecord<TQStateAction>> records);
}
