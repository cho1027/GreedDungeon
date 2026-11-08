using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시체왕: CorpseKingBoss 의 페이즈(1 = 거대한 육체, 2 = 드러난 본체)에 맞는 선택기를 쓴다.
/// 페이즈가 바뀌면 행동 순번을 처음부터 다시 센다.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Corpse King Selector", fileName = "CorpseKingSelector")]
public class Ch1CorpseKingSelector : EnemyActionSelector
{
    [SerializeField] private EnemyActionSelector _phase1;
    [SerializeField] private EnemyActionSelector _phase2;

    public override EnemyAction Choose(EnemyController enemy)
    {
        CorpseKingBoss boss = enemy.GetComponent<CorpseKingBoss>();
        int phase = boss != null ? boss.Phase : 1;

        if (enemy.SelectorPhase != phase) enemy.EnterSelectorPhase(phase, false);

        EnemyActionSelector selector = phase >= 2 ? _phase2 : _phase1;
        if (selector == null || selector == this) return null;
        return selector.Choose(enemy);
    }
}
