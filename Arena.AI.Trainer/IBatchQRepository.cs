using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

namespace Arena.AI.Trainer;

/// <summary>
/// Optional interface for repos that support fetching all action rewards
/// for a given state in a single query (5x fewer DB round-trips per turn).
/// </summary>
public interface IBatchQRepository
{
    Task<Dictionary<MinimalQAction, double>> GetAllRewardsForStateAsync(MinimalQStateAction baseState);
}
