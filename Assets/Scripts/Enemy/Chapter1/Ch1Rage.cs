using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 광폭화 ─────────────────────────

/// <summary>시체왕 2페이즈 광폭화: 공격력 +3, 3턴. 중복으로 걸리지 않는다 (이미 걸려 있으면 고르지 않음)</summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Rage", fileName = "Rage")]
public class Ch1Rage : EnemyAction
{
    [SerializeField] private int _amount = 3;
    [Min(1)][SerializeField] private int _turns = 3;

    public override bool CanUse(EnemyController enemy) =>
        enemy.Status == null || !enemy.Status.Has(StatusType.Rage);

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Facing = enemy.Facing;
        intent.Value = _amount;
        intent.ValueLabel = $"+{_amount}";
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f) yield return new WaitForSeconds(enemy.ActionDelay);
        if (enemy.Status != null) enemy.Status.Apply(StatusType.Rage, _turns, _amount, StatusStackRule.Refresh);
    }
}
