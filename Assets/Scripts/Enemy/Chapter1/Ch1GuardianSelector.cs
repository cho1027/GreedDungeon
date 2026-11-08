using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 묘지의 수호자
///  1. (철벽 방어 / 반격 태세 중 무작위) → (방패 강타 / 내려찍기) 를 반복
///  2. 공격 차례: 플레이어가 상하좌우 1칸에 붙어 있으면 방패 강타, 아니면 내려찍기
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Guardian Selector", fileName = "GuardianSelector")]
public class Ch1GuardianSelector : EnemyActionSelector
{
    [SerializeField] private List<EnemyAction> _defenses = new List<EnemyAction>();
    [SerializeField] private EnemyAction _shieldBash;
    [SerializeField] private EnemyAction _slam;

    public override EnemyAction Choose(EnemyController enemy)
    {
        bool defenseTurn = enemy.PhaseActionCount % 2 == 0;
        if (defenseTurn)
        {
            EnemyAction d = Ch1Select.RandomUsable(enemy, _defenses);
            if (d != null) return d;
        }

        if (_shieldBash != null && enemy.DistanceToPlayer() == 1) return _shieldBash;
        return _slam != null ? _slam : _shieldBash;
    }
}
