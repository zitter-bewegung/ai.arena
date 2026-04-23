using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;

namespace Arena.AI.Core;

public class RealtimeBattleManager
{
    private readonly IRealtimePlayer _playerA;
    private readonly IRealtimePlayer _playerB;
    private readonly RealtimeBattle _realtimeBattle;

    public RealtimeBattleManager(IRealtimePlayer playerA, IRealtimePlayer playerB)
    {
        _playerA = playerA;
        _playerB = playerB;
        _realtimeBattle = new RealtimeBattle();
    }

    public async Task PlayBattleAsync()
    {
        while(_realtimeBattle.GetBattleState().Winner is null)
        {
            var battleState = _realtimeBattle.GetBattleState();
            var currentPlayer = battleState.NextUnitInfo.TeamName == "TeamA" ? _playerA : _playerB;
            var action = await currentPlayer.ActAsync(battleState);
            
            _realtimeBattle.Play(action);
        }

        await Task.WhenAll(
            _playerA.ReportResultAsync(GetBattleResult()),
            _playerB.ReportResultAsync(GetBattleResult())
            );
    }

    public string BattleId => _realtimeBattle.Id;

    public BattleResult GetBattleResult()
        => new ()
        {
            BattleId = _realtimeBattle.GetBattleState().BattleId,
            Winner = _realtimeBattle.GetBattleState().Winner ?? "No winner",
            Actions = _realtimeBattle.GetBattleActions()
        };

    public List<BattleAction> GetCurrentBattleState()
        => _realtimeBattle.GetBattleActions();
}

internal class BattleInfo
{
    public RealtimeBattle Battle { get; init; }
    public PlayerInfo PlayerA { get; set; }
    public PlayerInfo PlayerB { get; set; }

}