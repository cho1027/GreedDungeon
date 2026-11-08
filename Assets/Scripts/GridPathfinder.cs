using System.Collections.Generic;
using UnityEngine;

/// <summary>방 내부에서 상하좌우 최단 경로를 찾습니다. 이동이나 턴 진행은 담당하지 않습니다.</summary>
public static class GridPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    /// <summary>탐색 중인 한 칸의 비용과 이전 칸을 보관합니다.</summary>
    private sealed class Node
    {
        public Vector2Int Cell;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public Node Parent;
    }

    /// <summary>
    /// 성공하면 시작 칸을 제외하고 목표 칸을 포함하는 경로를 반환합니다.
    /// 시작과 목표가 같으면 성공과 빈 목록을 반환합니다. 실패해도 목록은 null이 아닙니다.
    /// 시작 칸의 동적 점유는 허용하지만 벽, 바닥 누락, 방 밖은 허용하지 않습니다.
    /// 문을 통한 다른 방 추적은 하지 않습니다. 인접 시 정지는 이동 담당 코드에서 처리합니다.
    /// </summary>
    public static bool TryFindPath(RoomGrid grid, Vector2Int start, Vector2Int goal,
        out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (grid == null || !grid.IsTerrainWalkable(start) || !grid.InBounds(goal))
            return false;
        if (start == goal) return true;
        if (!grid.IsWalkable(goal)) return false;

        // Open List: 앞으로 탐색할 후보. Closed List: 이미 탐색한 칸.
        var open = new List<Node>();
        var closed = new HashSet<Vector2Int>();
        var nodes = new Dictionary<Vector2Int, Node>();
        var first = new Node { Cell = start, GCost = 0, HCost = ManhattanDistance(start, goal) };
        open.Add(first);
        nodes.Add(start, first);

        while (open.Count > 0)
        {
            // 작은 방이므로 복잡한 힙 대신 목록에서 최소 F를 선택합니다.
            Node current = open[0];
            for (int i = 1; i < open.Count; i++)
            {
                Node candidate = open[i];
                if (candidate.FCost < current.FCost ||
                    (candidate.FCost == current.FCost && candidate.HCost < current.HCost))
                    current = candidate;
            }

            open.Remove(current);
            if (current.Cell == goal)
            {
                // Parent를 거슬러 올라간 뒤 뒤집으면 이동 순서가 됩니다.
                for (Node node = current; node.Parent != null; node = node.Parent)
                    path.Add(node.Cell);
                path.Reverse();
                return true;
            }

            closed.Add(current.Cell);
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current.Cell + direction;
                if (!grid.InBounds(next) || closed.Contains(next) || !grid.IsWalkable(next))
                    continue;

                int newG = current.GCost + 1;
                if (!nodes.TryGetValue(next, out Node neighbor))
                {
                    neighbor = new Node
                    {
                        Cell = next, GCost = newG,
                        HCost = ManhattanDistance(next, goal), Parent = current
                    };
                    nodes.Add(next, neighbor);
                    open.Add(neighbor);
                }
                else if (newG < neighbor.GCost)
                {
                    // 더 짧은 경로를 발견하면 비용과 이전 칸을 함께 바꿉니다.
                    neighbor.GCost = newG;
                    neighbor.Parent = current;
                }
            }
        }

        return false;
    }

    /// <summary>대각선 없이 가로·세로로 이동할 때의 예상 거리입니다.</summary>
    public static int ManhattanDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }
}
