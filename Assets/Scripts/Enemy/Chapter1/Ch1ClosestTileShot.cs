using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 대각선 사격 ─────────────────────────

/// <summary>
/// 부패한 사수 대각선 사격: 자기 주변 3x3 안에서 플레이어와 가장 가까운 한 칸을 쏜다. 7
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Closest Tile Shot", fileName = "ClosestTileShot")]
public class Ch1ClosestTileShot : Ch1AttackAction
{
    [Min(1)][SerializeField] private int _radius = 1;

    protected override void BuildCells(EnemyController enemy, EnemyIntent intent)
    {
        Ch1.FaceTarget(enemy);
        if (!enemy.IsPlayerInRoom) return;

        RoomGrid grid = enemy.Room.Grid;
        Vector2Int target = enemy.Player.Cell;
        Vector2Int best = enemy.Cell;
        float bestDistance = float.MaxValue;

        for (int x = -_radius; x <= _radius; x++)
        {
            for (int y = -_radius; y <= _radius; y++)
            {
                if (x == 0 && y == 0) continue;
                Vector2Int c = enemy.Cell + new Vector2Int(x, y);
                if (!grid.InBounds(c)) continue;
                if (!grid.IsWalkable(c) && c != target) continue;

                float d = (c - target).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
        }

        if (best != enemy.Cell) intent.Cells.Add(best);
    }
}
