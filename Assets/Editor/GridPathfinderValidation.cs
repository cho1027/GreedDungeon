using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>씬을 변경하지 않고 임시 오브젝트로 경로 탐색을 검증하는 Editor 전용 도구입니다.</summary>
public static class GridPathfinderValidation
{
    [MenuItem("Tools/Grid Dungeon/Validate A Star")]
    public static void Run()
    {
        var root = new GameObject("A Star validation");
        try
        {
            RoomGrid grid = root.AddComponent<RoomGrid>();
            Vector2Int start = new Vector2Int(1, 3);
            Vector2Int goal = new Vector2Int(9, 3);
            Check(grid, start, goal, true, 8); // 장애물 없음
            grid.SetBlocked(new Vector2Int(5, 3), true);
            Check(grid, start, goal, true, 10); // 직선 앞 벽
            grid.ClearBlocked();
            for (int y = 0; y < grid.Height - 1; y++)
                grid.SetBlocked(new Vector2Int(5, y), true);
            Check(grid, start, goal, true, 14); // 위쪽 끝으로 우회
            grid.SetBlocked(new Vector2Int(5, grid.Height - 1), true);
            Check(grid, start, goal, false, 0); // 완전히 차단
            grid.ClearBlocked();
            Check(grid, start, start + Vector2Int.right, true, 1); // 인접
            Check(grid, start, new Vector2Int(grid.Width, 3), false, 0); // 범위 밖
            Check(grid, start, start, true, 0);
            grid.SetBlocked(start, true);
            Check(grid, start, goal, true, 8); // 적 자신의 점유는 허용
            grid.SetBlocked(goal, true);
            Check(grid, start, goal, false, 0);
            Check(null, start, goal, false, 0);

            // 위치와 크기가 바뀐 방에서도 Tilemap 좌표를 올바르게 변환하는지 검사합니다.
            grid.ClearBlocked();
            root.transform.position = new Vector3(10, -4, 0);
            root.transform.localScale = Vector3.one * 2;
            var tileRoot = new GameObject("Tile grid", typeof(Grid));
            tileRoot.transform.SetParent(root.transform, false);
            var wallObject = new GameObject("Walls", typeof(Tilemap));
            wallObject.transform.SetParent(tileRoot.transform, false);
            Tilemap walls = wallObject.GetComponent<Tilemap>();
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var serialized = new SerializedObject(grid);
                serialized.FindProperty("_wallTilemap").objectReferenceValue = walls;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                walls.SetTile(walls.WorldToCell(grid.GridToWorld(new Vector2Int(5, 3))), tile);
                Check(grid, start, goal, true, 10);
                Check(grid, new Vector2Int(5, 3), goal, false, 0);
                serialized.FindProperty("_floorTilemap").objectReferenceValue = walls;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(grid, start, goal, false, 0); // 바닥 타일 누락
            }
            finally { UnityEngine.Object.DestroyImmediate(tile); }
            Debug.Log("A* 검증 통과: 기본 6개 상황 및 입력·점유·Tilemap 좌표 예외.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Check(RoomGrid grid, Vector2Int start, Vector2Int goal,
        bool expected, int length)
    {
        bool found = GridPathfinder.TryFindPath(grid, start, goal, out var path);
        if (found != expected || path == null || path.Count != length)
            throw new Exception($"A* 검증 실패: {start} → {goal}, 성공={found}, 길이={path?.Count}");
        Vector2Int previous = start;
        foreach (Vector2Int cell in path)
        {
            if (GridPathfinder.ManhattanDistance(previous, cell) != 1 || !grid.IsWalkable(cell))
                throw new Exception("경로에 벽 또는 상하좌우가 아닌 이동이 있습니다.");
            previous = cell;
        }
        if (found && previous != goal) throw new Exception("목표에 도착하지 않은 경로입니다.");
    }
}
