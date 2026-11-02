using System.Collections.Generic;
using UnityEngine;

/// <summary>공격 범위 모양</summary>
public enum AttackShape
{
    Line = 0,   // 패턴 A 직선: 바라보는 방향 정면으로 range 칸
    Horizontal  // 패턴 B 가로 범위: 정면 range 칸 × (정면, 왼쪽 앞, 오른쪽 앞)
}

/// <summary>
/// 공격 범위(칸 목록) 계산.
/// 적은 행동이 확정될 때 이 결과를 한 번만 계산해 저장하고, 바닥 표시와 피해 판정에 같은 목록을 쓴다.
/// (그래서 표시된 범위와 실제 판정이 달라질 수 없다)
/// </summary>
public static class AttackArea
{
    /// <summary>
    /// 모양에 맞는 범위를 계산해 result 에 담는다 (result 는 먼저 비워진다).
    /// 바닥 밖(벽, 문)은 포함하지 않는다. stopAtObstacles 면 장애물·다른 몬스터 칸에서 그 줄의 공격이 막힌다.
    /// </summary>
    public static void Calculate(RoomGrid grid, AttackShape shape, Vector2Int origin, Vector2Int facing,
                                 int range, bool stopAtObstacles, List<Vector2Int> result)
    {
        result.Clear();
        if (grid == null || range <= 0 || facing == Vector2Int.zero) return;

        switch (shape)
        {
            case AttackShape.Line:
                AddRay(grid, origin, facing, range, stopAtObstacles, result);
                break;

            case AttackShape.Horizontal:
            {
                // 바라보는 방향 기준 왼쪽/오른쪽
                Vector2Int left = new Vector2Int(-facing.y, facing.x);
                Vector2Int right = -left;

                // 거리 1~range 마다 정면, 왼쪽 앞, 오른쪽 앞 (3줄을 각각 직선처럼 계산)
                AddRay(grid, origin, facing, range, stopAtObstacles, result);
                AddRay(grid, origin + left, facing, range, stopAtObstacles, result);
                AddRay(grid, origin + right, facing, range, stopAtObstacles, result);
                break;
            }
        }
    }

    /// <summary>start 바로 다음 칸부터 dir 방향으로 range 칸 (start 자신은 포함하지 않음)</summary>
    private static void AddRay(RoomGrid grid, Vector2Int start, Vector2Int dir, int range, bool stopAtObstacles, List<Vector2Int> result)
    {
        Vector2Int cell = start;
        for (int i = 0; i < range; i++)
        {
            cell += dir;
            if (!grid.InBounds(cell)) break;                       // 벽 밖
            if (stopAtObstacles && !grid.IsWalkable(cell)) break;  // 장애물, 다른 몬스터 칸

            if (!result.Contains(cell)) result.Add(cell);
        }
    }

    /// <summary>from 에서 to 쪽으로 향하는 상하좌우 방향 (차이가 큰 축 기준, 같으면 가로)</summary>
    public static Vector2Int DirectionTowards(Vector2Int from, Vector2Int to)
    {
        Vector2Int d = to - from;
        if (d == Vector2Int.zero) return Vector2Int.down;

        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return new Vector2Int(d.x > 0 ? 1 : -1, 0);

        return new Vector2Int(0, d.y > 0 ? 1 : -1);
    }

    /// <summary>DoorDirection(북/남/동/서)을 격자 방향으로 변환</summary>
    public static Vector2Int ToVector(DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.North: return Vector2Int.up;
            case DoorDirection.South: return Vector2Int.down;
            case DoorDirection.East: return Vector2Int.right;
            default: return Vector2Int.left;
        }
    }
}
