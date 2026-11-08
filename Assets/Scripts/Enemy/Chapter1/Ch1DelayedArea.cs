using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 지연 범위 공격 ─────────────────────────

/// <summary>
/// 시체왕 대지 붕괴: 실행할 때 플레이어와 가장 가까운 시체 타일 기준 3x3 에 경고를 표시하고,
/// 다음 적 턴이 시작될 때 터진다 (그 사이 플레이어 턴에 피할 수 있다). 10
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Delayed Area", fileName = "DelayedArea")]
public class Ch1DelayedArea : EnemyAction
{
    [Min(0)][SerializeField] private int _damage = 10;
    [Min(1)][SerializeField] private int _radius = 1;
    [SerializeField] private Ch1AreaCenter _center = Ch1AreaCenter.NearestBodyTile;
    [SerializeField] private bool _addStrength = true;

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Facing = enemy.Facing;
        intent.Value = Mathf.Max(0, _damage + (_addStrength ? enemy.Strength : 0));
        intent.ValueLabel = $"{intent.Value}!";
        // 범위는 실행할 때 정해지고 바닥 경고로 따로 표시된다
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f) yield return new WaitForSeconds(enemy.ActionDelay);
        if (enemy.Room == null) yield break;

        Vector2Int center;
        switch (_center)
        {
            case Ch1AreaCenter.Player: center = enemy.IsPlayerInRoom ? enemy.Player.Cell : enemy.Cell; break;
            case Ch1AreaCenter.Self: center = enemy.Cell; break;
            default: center = Ch1.NearestCorpseTile(enemy); break;
        }

        var cells = new List<Vector2Int>();
        Ch1.AddSquare(enemy.Room.Grid, center, _radius, true, false, true, cells);
        HazardZones.Get(enemy.Room).AddDelayedStrike(cells, intent.Value, enemy.gameObject);

        Debug.Log($"[Enemy] {enemy.name} {DisplayName} 예고 ({cells.Count}칸) → 다음 적 턴에 발동", enemy);
    }
}
