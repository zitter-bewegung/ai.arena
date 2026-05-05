using Arena.AI.Core.Models;

namespace Arena.AI.Core.Logic.BattleLogic;

public class MovementOrderManager
{
    private static Random rnd = new Random();
    private Unit[] _units;
    private int _index;

    public MovementOrderManager(params Team[] teams)
    {
        _units = teams
            .SelectMany(t => t.Units)
            .OrderByDescending(u => u.Movement)
            .ThenBy(t => rnd.Next(100))
            .ToArray();

        _index = 0;
    }

    public string WhosNext()
    {
        do
        {
            _index++;
            if(_index >= _units.Length)
            {
                _index = 0;
            }
        } while(_units[_index].IsDead);

        return _units[_index].Name;
    }

}
