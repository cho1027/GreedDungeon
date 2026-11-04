using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>패턴 공격 범위 모양</summary>
public enum PatternShapeType
{
    Line = 0, // 정면 직선 range 칸
    Wide,     // 정면으로 여러 줄 (Side Lines = 1 이면 3줄)
    Cross,    // 상하좌우 4방향 직선
    Around,   // 자기 주변 사각형 (range 1 = 주변 8칸)
    Diamond,  // 자기 주변 마름모 (상하좌우 걸음 수 range 이내)
    Custom    // 직접 칸을 찍어서 만든 모양 (인스펙터 격자에서 클릭)
}

/// <summary>
/// 공격 범위 모양 데이터. 인스펙터에서 미리보기 격자로 모양을 바로 확인할 수 있다 (위쪽 = 바라보는 방향).
/// 좌표는 "적 기준, 바라보는 방향 기준": x = 오른쪽(+)/왼쪽(-), y = 앞(+)/뒤(-), 적 자신은 (0, 0).
/// 적이 어느 방향을 보든 자동으로 회전된다.
/// </summary>
[Serializable]
public class PatternShape
{
    public PatternShapeType type = PatternShapeType.Line;

    [Tooltip("사거리 / 크기 (Custom 에는 쓰지 않음)")]
    [Min(1)] public int range = 3;

    [Tooltip("Wide 전용: 정면 줄 양옆으로 더할 줄 수 (1 → 3줄, 2 → 5줄)")]
    [Min(0)] public int sideLines = 1;

    [Tooltip("장애물/다른 몬스터에 막힌다. 직선형(Line, Wide, Cross)은 막힌 칸 뒤도 맞지 않고, 나머지는 막힌 칸만 빠진다")]
    public bool stopAtObstacles = true;

    [Tooltip("Custom 전용: 맞는 칸 목록 (적 기준, x = 오른쪽+, y = 앞+). 인스펙터 격자를 클릭해서 편집")]
    public List<Vector2Int> customCells = new List<Vector2Int>();

    public bool IsRay => type == PatternShapeType.Line || type == PatternShapeType.Wide || type == PatternShapeType.Cross;

    /// <summary>실제 방에서 맞는 칸을 계산한다 (result 는 먼저 비워진다)</summary>
    public void Calculate(RoomGrid grid, Vector2Int origin, Vector2Int facing, List<Vector2Int> result)
    {
        result.Clear();
        if (grid == null) return;
        Calculate(grid.InBounds, grid.IsWalkable, origin, facing, result);
    }

    /// <summary>
    /// 바닥 판정을 직접 넘겨서 계산 (미리보기, 테스트용).
    /// inBounds: 바닥 안인지, walkable: 막히지 않았는지
    /// </summary>
    public void Calculate(Func<Vector2Int, bool> inBounds, Func<Vector2Int, bool> walkable,
                          Vector2Int origin, Vector2Int facing, List<Vector2Int> result)
    {
        result.Clear();
        if (facing == Vector2Int.zero) facing = Vector2Int.down;

        // 바라보는 방향 기준 오른쪽
        Vector2Int right = new Vector2Int(facing.y, -facing.x);
        int r = Mathf.Max(1, range);

        switch (type)
        {
            case PatternShapeType.Line:
                AddRay(inBounds, walkable, origin, facing, r, result);
                break;

            case PatternShapeType.Wide:
                for (int k = -sideLines; k <= sideLines; k++)
                    AddRay(inBounds, walkable, origin + right * k, facing, r, result);
                break;

            case PatternShapeType.Cross:
                AddRay(inBounds, walkable, origin, Vector2Int.up, r, result);
                AddRay(inBounds, walkable, origin, Vector2Int.down, r, result);
                AddRay(inBounds, walkable, origin, Vector2Int.left, r, result);
                AddRay(inBounds, walkable, origin, Vector2Int.right, r, result);
                break;

            case PatternShapeType.Around:
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                        if (x != 0 || y != 0) AddCell(inBounds, walkable, origin + new Vector2Int(x, y), result);
                break;

            case PatternShapeType.Diamond:
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                        if ((x != 0 || y != 0) && Mathf.Abs(x) + Mathf.Abs(y) <= r)
                            AddCell(inBounds, walkable, origin + new Vector2Int(x, y), result);
                break;

            case PatternShapeType.Custom:
                if (customCells == null) break;
                foreach (Vector2Int local in customCells)
                {
                    if (local == Vector2Int.zero) continue; // 자기 칸은 제외
                    AddCell(inBounds, walkable, origin + right * local.x + facing * local.y, result);
                }
                break;
        }
    }

    /// <summary>start 다음 칸부터 dir 방향으로 count 칸</summary>
    private void AddRay(Func<Vector2Int, bool> inBounds, Func<Vector2Int, bool> walkable,
                        Vector2Int start, Vector2Int dir, int count, List<Vector2Int> result)
    {
        Vector2Int cell = start;
        for (int i = 0; i < count; i++)
        {
            cell += dir;
            if (!inBounds(cell)) break;
            if (stopAtObstacles && !walkable(cell)) break;
            if (!result.Contains(cell)) result.Add(cell);
        }
    }

    private void AddCell(Func<Vector2Int, bool> inBounds, Func<Vector2Int, bool> walkable,
                         Vector2Int cell, List<Vector2Int> result)
    {
        if (!inBounds(cell)) return;
        if (stopAtObstacles && !walkable(cell)) return;
        if (!result.Contains(cell)) result.Add(cell);
    }

    public string Describe()
    {
        switch (type)
        {
            case PatternShapeType.Line: return $"직선 {range}칸";
            case PatternShapeType.Wide: return $"{sideLines * 2 + 1}줄 x {range}칸";
            case PatternShapeType.Cross: return $"십자 {range}칸";
            case PatternShapeType.Around: return $"주변 {range}칸";
            case PatternShapeType.Diamond: return $"마름모 {range}칸";
            default: return $"지정 {(customCells != null ? customCells.Count : 0)}칸";
        }
    }
}
