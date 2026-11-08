using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 방어 ─────────────────────────

/// <summary>
/// 방어도 증가. 방어도는 지속시간이 없는 능력치이고, 매 턴 시작마다 방어도만큼 방어막이 쌓인다 (사라지지 않음).
/// 쓰는 즉시 방어막도 같은 양만큼 얻는다.
///  - 망자의 병사 방어: 방어도 +2
///  - 묘지의 수호자 철벽 방어: 방어도 +10
///  - 묘지의 수호자 반격 태세: 방어도 +3
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Guard", fileName = "Guard")]
public class Ch1Guard : EnemyAction
{
    [Min(0)][SerializeField] private int _shield = 2;
    [Tooltip("방어막 유지 턴 (이 적의 턴 시작마다 1씩 줄어든다). 0 = 사라지지 않고 쌓인다")]
    [Min(0)][SerializeField] private int _shieldTurns = 0;
    [Tooltip("방어도 증가: 매 턴 시작마다 방어도만큼 사라지지 않는 방어막이 쌓인다")]
    [Min(0)][SerializeField] private int _armorGain = 0;

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Facing = enemy.Facing;
        intent.Value = _armorGain > 0 ? _armorGain : _shield;
        intent.ValueLabel = null;
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f) yield return new WaitForSeconds(enemy.ActionDelay);

        if (enemy.Shield != null) enemy.Shield.Add(_shield, _shieldTurns);
        if (_armorGain > 0) enemy.AddArmor(_armorGain);

        Debug.Log($"[Enemy] {enemy.name} {DisplayName} → 방어막 +{_shield} (현재 {(enemy.Shield != null ? enemy.Shield.Amount : 0)}), 방어도 {enemy.Armor}", enemy);
    }
}
