using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 방패 강타 ─────────────────────────

/// <summary>
/// 묘지의 수호자 방패 강타: 정면 1칸, 피해 = 실행 시점의 방어막 수치.
/// 맞으면 1턴 속박 + 한 칸 밀려나고 수호자가 그 자리로 한 칸 전진. 밀려날 칸이 벽/막힌 칸이면 둘 다 움직이지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Shield Bash", fileName = "ShieldBash")]
public class Ch1ShieldBash : Ch1AttackAction
{
    [Header("방패 강타")]
    [Tooltip("방어막 수치에 더할 피해")]
    [SerializeField] private int _bonusDamage = 0;
    [Min(0)][SerializeField] private int _knockback = 1;

    protected override void BuildCells(EnemyController enemy, EnemyIntent intent)
    {
        Ch1.FaceTarget(enemy);
        Vector2Int c = enemy.Cell + enemy.Facing;
        if (enemy.Room.Grid.InBounds(c)) intent.Cells.Add(c);

        int shield = enemy.Shield != null ? enemy.Shield.Amount : 0;
        intent.Value = Mathf.Max(0, shield + _bonusDamage + (_addStrength ? enemy.Strength : 0));
        intent.ValueLabel = null;
    }

    protected override int DamageAt(EnemyController enemy, EnemyIntent intent, Vector2Int playerCell)
    {
        int shield = enemy.Shield != null ? enemy.Shield.Amount : 0;
        return Mathf.Max(0, shield + _bonusDamage + (_addStrength ? enemy.Strength : 0));
    }

    protected override void OnHit(EnemyController enemy, EnemyIntent intent, DamageResult result)
    {
        base.OnHit(enemy, intent, result);

        GridPlayer player = enemy.Player;
        if (player == null || _knockback <= 0) return;

        Vector2Int dir = intent.Facing;
        Vector2Int from = player.Cell;
        Vector2Int to = from + dir * _knockback;

        // 밀려날 칸이 바닥 밖 / 막힌 칸이면 둘 다 그대로
        if (!player.ForceMove(to)) return;

        enemy.StartCoroutine(enemy.StepTo(from));
    }

    protected override IEnumerator AfterAttack(EnemyController enemy, EnemyIntent intent)
    {
        // 전진 이동이 끝날 때까지 잠깐 기다린다
        yield return new WaitForSeconds(enemy.MoveTime);
    }
}
