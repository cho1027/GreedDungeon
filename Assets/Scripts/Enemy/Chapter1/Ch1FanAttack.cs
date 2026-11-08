using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 부채꼴 ─────────────────────────

/// <summary>
/// 전방 1칸 + 좌우 앞 1칸 부채꼴 (정면 3칸).
///  - 망자의 병사 강공격: 7 / 묘지의 수호자 내려찍기: 12
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Fan Attack", fileName = "FanAttack")]
public class Ch1FanAttack : Ch1AttackAction
{
    [Tooltip("양옆 칸(바로 왼쪽/오른쪽)도 포함")]
    [SerializeField] private bool _includeSides = false;

    protected override void BuildCells(EnemyController enemy, EnemyIntent intent)
    {
        Ch1.FaceTarget(enemy);
        Vector2Int f = enemy.Facing;
        Vector2Int left = new Vector2Int(-f.y, f.x);
        RoomGrid grid = enemy.Room.Grid;

        var cells = new List<Vector2Int> { enemy.Cell + f, enemy.Cell + f + left, enemy.Cell + f - left };
        if (_includeSides)
        {
            cells.Add(enemy.Cell + left);
            cells.Add(enemy.Cell - left);
        }

        foreach (Vector2Int c in cells)
            if (grid.InBounds(c) && grid.IsWalkable(c) && !intent.Cells.Contains(c)) intent.Cells.Add(c);
    }
}
