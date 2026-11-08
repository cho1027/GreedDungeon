using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 역병 구울
///  1. 플레이어가 인접(상하좌우 1칸)하면 할퀴기
///  2. 2~3칸 떨어져 있으면 부패 분출 (연속 사용 불가)
///  3. 그 외에는 할퀴기를 예고하고 다가간다 (Post Move = Chase)
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Ghoul Selector", fileName = "GhoulSelector")]
public class Ch1GhoulSelector : EnemyActionSelector
{
    [SerializeField] private EnemyAction _claw;
    [SerializeField] private EnemyAction _spew;
    [Min(1)][SerializeField] private int _spewMinDistance = 2;
    [Min(1)][SerializeField] private int _spewMaxDistance = 3;

    public override EnemyAction Choose(EnemyController enemy)
    {
        int d = enemy.DistanceToPlayer();
        if (d == 1) return _claw;

        bool spewReady = _spew != null && enemy.LastAction != _spew;
        if (spewReady && d >= _spewMinDistance && d <= _spewMaxDistance) return _spew;

        return _claw != null ? _claw : _spew;
    }
}
