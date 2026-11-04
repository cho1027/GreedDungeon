using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 이동용 길찾기 연결 지점.
/// 기본은 BFS(너비 우선 탐색)로 동작하고, A* 를 만들면 Solver 에 넣기만 하면 모든 적 이동이 A* 를 쓴다.
///
/// A* 연결 예 (할림):
///   EnemyPathfinding.Solver = MyAStar.FindPath;   // 게임 시작 시 한 번 (예: [RuntimeInitializeOnLoadMethod])
///
/// 약속 (BFS 와 A* 가 똑같이 지켜야 하는 것)
///  - result 는 먼저 비우고, 시작 칸은 넣지 않고, 목표 칸은 넣는다 (start 다음 칸 ~ goal)
///  - isWalkable(칸) 이 false 인 칸은 지나가지 않는다. 단, goal 칸은 막혀 있어도 도착 칸으로 인정한다
///    (플레이어 칸이 goal 인 경우. 실제 이동은 MoveStep 이 플레이어 바로 앞에서 멈춘다)
///  - 상하좌우 4방향만 이동
///  - 길이 없으면 false
/// </summary>
public static class EnemyPathfinding
{
    public delegate bool PathSolver(RoomGrid grid, Vector2Int start, Vector2Int goal,
                                    Func<Vector2Int, bool> isWalkable, List<Vector2Int> result);

    /// <summary>A* 등 다른 길찾기로 바꾸고 싶을 때 넣는다. null 이면 BFS</summary>
    public static PathSolver Solver;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public static IReadOnlyList<Vector2Int> FourDirections => Directions;

    /// <summary>start → goal 경로를 result 에 담는다 (start 제외, goal 포함)</summary>
    public static bool FindPath(RoomGrid grid, Vector2Int start, Vector2Int goal,
                                Func<Vector2Int, bool> isWalkable, List<Vector2Int> result)
    {
        result.Clear();
        if (grid == null || isWalkable == null) return false;
        if (start == goal) return true;

        PathSolver solver = Solver;
        return solver != null
            ? solver(grid, start, goal, isWalkable, result)
            : Bfs(grid, start, goal, isWalkable, result);
    }

    /// <summary>기본 길찾기 (가중치 없는 격자에서는 최단 경로)</summary>
    public static bool Bfs(RoomGrid grid, Vector2Int start, Vector2Int goal,
                           Func<Vector2Int, bool> isWalkable, List<Vector2Int> result)
    {
        result.Clear();
        if (start == goal) return true;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;
                if (cameFrom.ContainsKey(next)) continue;
                if (!grid.InBounds(next)) continue;
                if (next != goal && !isWalkable(next)) continue;

                cameFrom[next] = current;

                if (next == goal)
                {
                    // goal 에서 거꾸로 따라가며 경로 복원
                    Vector2Int cell = goal;
                    while (cell != start)
                    {
                        result.Add(cell);
                        cell = cameFrom[cell];
                    }
                    result.Reverse();
                    return true;
                }

                queue.Enqueue(next);
            }
        }

        return false;
    }

    /// <summary>상하좌우 걸음 수 거리</summary>
    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
