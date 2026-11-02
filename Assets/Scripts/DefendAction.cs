using System.Collections;
using UnityEngine;

/// <summary>
/// 방어 행동: 방어막을 얻는다. 방어막은 다음 플레이어 턴 동안 유지되고, 이 적의 다음 턴이 시작될 때 사라진다.
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Defend Action
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Defend Action", fileName = "DefendAction")]
public class DefendAction : EnemyAction
{
    [Min(0)][SerializeField] private int _shieldAmount = 6;

    public int ShieldAmount => _shieldAmount;

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Facing = enemy.Facing;
        intent.Value = _shieldAmount;
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f)
            yield return new WaitForSeconds(enemy.ActionDelay);

        if (enemy.Shield == null)
        {
            Debug.LogWarning($"{enemy.name}: DamageShield 컴포넌트가 없어 방어막을 얻지 못했습니다.", enemy);
            yield break;
        }

        enemy.Shield.Add(intent.Value);
        Debug.Log($"[Enemy] {enemy.name} {DisplayName} → 방어막 +{intent.Value} (현재 {enemy.Shield.Amount})", enemy);
    }
}
