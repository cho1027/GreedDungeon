using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 이동 길찾기를 Halim 의 A* (GridPathfinder) 로 바꾼다.
///  - EnemyController 의 추적 이동은 GridPathfinder.TryFindPath 를 직접 쓴다.
///  - 패턴 에셋의 이동 단계(MoveStep 등 EnemyPathfinding.FindPath 를 쓰는 곳)도
///    게임 시작 시 EnemyPathfinding.Solver 에 A* 를 연결해 같은 A* 를 쓰게 한다.
/// </summary>
public static class GridPathfinderHook
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        EnemyPathfinding.Solver = Solve;
    }

    /// <summary>EnemyPathfinding 약속(시작 칸 제외, 목표 칸 포함, 실패 시 false)에 맞춰 A* 결과를 넘긴다</summary>
    private static bool Solve(RoomGrid grid, Vector2Int start, Vector2Int goal,
                              Func<Vector2Int, bool> isWalkable, List<Vector2Int> result)
    {
        result.Clear();
        if (!GridPathfinder.TryFindPath(grid, start, goal, out List<Vector2Int> path)) return false;
        result.AddRange(path);
        return true;
    }
}
