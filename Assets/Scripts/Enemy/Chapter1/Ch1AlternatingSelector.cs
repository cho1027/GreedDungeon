using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 두 묶음을 번갈아 쓴다: A 묶음에서 무작위 → B 묶음에서 무작위 → 반복.
/// 쓸 수 없는 행동(CanUse = false, 예: 이미 광폭화 중)은 고르지 않는다.
/// 시체왕 1/2페이즈 행동 순서에 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Alternating Selector", fileName = "AlternatingSelector")]
public class Ch1AlternatingSelector : EnemyActionSelector
{
    [SerializeField] private List<EnemyAction> _groupA = new List<EnemyAction>();
    [SerializeField] private List<EnemyAction> _groupB = new List<EnemyAction>();

    public override EnemyAction Choose(EnemyController enemy)
    {
        bool first = enemy.PhaseActionCount % 2 == 0;
        List<EnemyAction> main = first ? _groupA : _groupB;
        List<EnemyAction> other = first ? _groupB : _groupA;
        return Ch1Select.RandomUsable(enemy, main) ?? Ch1Select.RandomUsable(enemy, other);
    }
}
