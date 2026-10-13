using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

/// <summary>
/// 아이작 스타일 2D 던전 생성기.
///  1) 중앙에서 시작해 BFS(큐) 방식으로 방을 확장한다.
///     - 이미 방이 있는 칸은 건너뜀
///     - 새 칸의 이웃 방이 2개 이상이면 건너뜀 (고리 방지)
///     - 확률(기본 50%)로 확장 건너뜀 → 불규칙한 모양
///  2) 막다른 방(이웃 1개) 중 시작점에서 가장 먼 곳 = 보스방, 나머지 막다른 방 = 특수방
///  3) 일반방은 프리팹 풀에서 "아직 안 나온 것" 위주로 뽑는다 (다 나오면 풀 리셋)
///  4) 이웃한 방이 있는 방향의 문만 연다
/// </summary>
public class DungeonGenerator2D : MonoBehaviour
{
    [Serializable]
    public struct SpecialRoom
    {
        public string label;        // 인스펙터 구분용 (상점, 보너스, 힐링 ...)
        public DungeonRoom prefab;
        [Min(1)] public int count;  // 몇 개 만들지 (막다른 방에 배치됨)
    }

    [Header("그리드")]
    [SerializeField] private int _gridX = 9;
    [SerializeField] private int _gridY = 9;

    [Header("생성 설정")]
    [SerializeField] private int _roomCount = 12;               // 시작방/보스방/특수방 포함 총 방 개수
    [Range(0f, 1f)]
    [SerializeField] private float _expandChance = 0.5f;        // 이웃 칸에 방을 만들 확률 (아이작: 50%)
    [SerializeField] private int _maxTryCount = 2000;           // 한 번의 시도 안에서의 반복 상한
    [SerializeField] private int _maxRegenerateCount = 50;      // 실패 시 처음부터 다시 만들 최대 횟수
    [Tooltip("켜면 방 프리팹의 실제 크기(Scale 포함) + Room Gap 으로 간격을 자동 계산한다")]
    [SerializeField] private bool _autoSpacing = true;
    [Tooltip("방과 방 사이 여백 (타일 칸 수 단위). 옆 방이 카메라에 걸리면 값을 키우세요")]
    [SerializeField] private Vector2 _roomGapCells = new Vector2(10f, 8f);
    [SerializeField] private Vector2 _roomSpacing = new Vector2(20f, 12f); // 수동 간격 (자동 간격이 꺼져 있을 때)

    [Header("프리팹")]
    [SerializeField] private DungeonRoom _startRoomPrefab;
    [SerializeField] private DungeonRoom _bossRoomPrefab;
    [SerializeField] private List<SpecialRoom> _specialRooms = new List<SpecialRoom>();
    [Tooltip("일반 방 프리팹들. 안 나온 프리팹 위주로 선택됨")]
    [SerializeField] private List<DungeonRoom> _normalRoomPrefabs = new List<DungeonRoom>();

    [Header("플레이어")]
    [Tooltip("던전 생성 후 시작방 중앙에 배치할 플레이어")]
    [SerializeField] private GridPlayer _player;

    // 북(+y) 남(-y) 동(+x) 서(-x)
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left
    };

    private bool[,] _map;
    private int[,] _distance;                       // 시작점으로부터의 거리
    private DungeonRoom[,] _rooms;
    private List<Vector2Int> _placed;
    private Dictionary<Vector2Int, DungeonRoom> _specialAssign;  // 특수방(보스 포함) 배정 결과
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<DungeonRoom> _unusedPool = new List<DungeonRoom>();

    private Vector2Int StartCell => new Vector2Int(_gridX / 2, _gridY / 2);

    private int SpecialTotalCount
    {
        get
        {
            int total = 1; // 보스
            foreach (var s in _specialRooms) total += Mathf.Max(1, s.count);
            return total;
        }
    }

    private void OnValidate()
    {
        _gridX = Mathf.Max(1, _gridX);
        _gridY = Mathf.Max(1, _gridY);
        // 시작방 + 특수방들 + (일반방 최소 0)
        _roomCount = Mathf.Clamp(_roomCount, 1, _gridX * _gridY);
    }

    private void Start()
    {
        Generate();
    }

    private void Update()
    {
#if UNITY_EDITOR
#if ENABLE_INPUT_SYSTEM
        // 새 Input System 사용 시
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Generate();
#else
        // 기존 Input Manager 사용 시
        if (Input.GetKeyDown(KeyCode.E))
            Generate();
#endif
#endif
    }

    // ───────────────────────── 진입점 ─────────────────────────

    [ContextMenu("Generate")]
    public void Generate()
    {
        ClearAll();

        bool success = false;
        for (int i = 0; i < _maxRegenerateCount; i++)
        {
            if (TryGenerateMap() && TryAssignSpecialRooms())
            {
                success = true;
                break;
            }
        }

        if (!success)
        {
            Debug.LogWarning("DungeonGenerator2D: 조건을 만족하는 맵을 만들지 못했습니다. " +
                             "방 개수를 늘리거나 특수방 개수를 줄여 보세요.");
            return;
        }

        SpawnRooms();
        PlacePlayer();
    }

    [ContextMenu("Clear")]
    public void ClearAll()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] == null) continue;

            if (Application.isPlaying) Destroy(_spawned[i]);
            else DestroyImmediate(_spawned[i]);
        }
        _spawned.Clear();
    }

    // ───────────────────────── 1. 아이작식 맵 생성 ─────────────────────────

    private bool TryGenerateMap()
    {
        _map = new bool[_gridX, _gridY];
        _placed = new List<Vector2Int>(_roomCount);

        Vector2Int start = StartCell;
        _map[start.x, start.y] = true;
        _placed.Add(start);

        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        int tryCount = 0;
        while (_placed.Count < _roomCount && tryCount < _maxTryCount)
        {
            tryCount++;

            // 큐가 비었는데 아직 방이 모자라면, 만들어진 방들을 다시 큐에 넣어 재확장
            if (queue.Count == 0)
            {
                foreach (var cell in _placed) queue.Enqueue(cell);
            }

            Vector2Int current = queue.Dequeue();

            // 방향 순서를 섞어서 한쪽으로 치우치지 않게
            Shuffle(Directions, out Vector2Int[] dirs);

            foreach (var dir in dirs)
            {
                if (_placed.Count >= _roomCount) break;

                Vector2Int next = current + dir;

                if (!InBounds(next)) continue;
                if (_map[next.x, next.y]) continue;
                if (CountNeighbors(next) > 1) continue;      // 고리 방지
                if (Random.value > _expandChance) continue;   // 확률적으로 건너뜀

                _map[next.x, next.y] = true;
                _placed.Add(next);
                queue.Enqueue(next);
            }
        }

        if (_placed.Count < _roomCount) return false;

        ComputeDistances();
        return true;
    }

    private void ComputeDistances()
    {
        _distance = new int[_gridX, _gridY];
        for (int x = 0; x < _gridX; x++)
            for (int y = 0; y < _gridY; y++)
                _distance[x, y] = -1;

        var queue = new Queue<Vector2Int>();
        Vector2Int start = StartCell;
        _distance[start.x, start.y] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (var dir in Directions)
            {
                Vector2Int n = cur + dir;
                if (!HasRoom(n.x, n.y)) continue;
                if (_distance[n.x, n.y] != -1) continue;

                _distance[n.x, n.y] = _distance[cur.x, cur.y] + 1;
                queue.Enqueue(n);
            }
        }
    }

    // ───────────────────────── 2. 특수방 배정 (막다른 방) ─────────────────────────

    private bool TryAssignSpecialRooms()
    {
        _specialAssign = new Dictionary<Vector2Int, DungeonRoom>();

        // 시작방을 제외한 막다른 방(이웃 1개) 목록
        var deadEnds = new List<Vector2Int>();
        foreach (var cell in _placed)
        {
            if (cell == StartCell) continue;
            if (CountNeighbors(cell) == 1) deadEnds.Add(cell);
        }

        if (deadEnds.Count < SpecialTotalCount) return false;

        // 보스방: 시작점에서 가장 먼 막다른 방
        Vector2Int boss = deadEnds[0];
        foreach (var cell in deadEnds)
        {
            if (_distance[cell.x, cell.y] > _distance[boss.x, boss.y])
                boss = cell;
        }
        _specialAssign[boss] = _bossRoomPrefab;
        deadEnds.Remove(boss);

        // 나머지 특수방: 남은 막다른 방에 랜덤 배치
        foreach (var special in _specialRooms)
        {
            int count = Mathf.Max(1, special.count);
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, deadEnds.Count);
                _specialAssign[deadEnds[idx]] = special.prefab;
                deadEnds.RemoveAt(idx);
            }
        }

        return true;
    }

    // ───────────────────────── 3. 방 생성 + 문 연결 ─────────────────────────

    private void SpawnRooms()
    {
        _rooms = new DungeonRoom[_gridX, _gridY];
        RefillPool();
        Vector2 spacing = GetSpacing();

        foreach (var cell in _placed)
        {
            DungeonRoom prefab = GetPrefab(cell);
            if (prefab == null)
            {
                Debug.LogError($"DungeonGenerator2D: ({cell.x}, {cell.y}) 방에 쓸 프리팹이 지정되지 않았습니다.");
                continue;
            }

            DungeonRoom room = Instantiate(prefab, transform);
            room.transform.localPosition = new Vector3(cell.x * spacing.x, cell.y * spacing.y, 0f);
            room.transform.localRotation = Quaternion.identity;
            room.name = $"{prefab.name} ({cell.x}, {cell.y})";

            _rooms[cell.x, cell.y] = room;
            _spawned.Add(room.gameObject);
        }

        // 이웃한 방이 있는 방향의 문만 열기
        foreach (var cell in _placed)
        {
            DungeonRoom room = _rooms[cell.x, cell.y];
            if (room == null) continue;

            room.SetDoor(
                HasRoom(cell.x, cell.y + 1),   // north
                HasRoom(cell.x, cell.y - 1),   // south
                HasRoom(cell.x + 1, cell.y),   // east
                HasRoom(cell.x - 1, cell.y));  // west
        }
    }

    // 방 프리팹의 실제 월드 크기 + 여백으로 방 간격 계산 (프리팹 Scale이 커도 안 겹침)
    private Vector2 GetSpacing()
    {
        if (!_autoSpacing) return _roomSpacing;

        DungeonRoom reference = _startRoomPrefab != null ? _startRoomPrefab : null;
        if (reference == null && _normalRoomPrefabs.Count > 0) reference = _normalRoomPrefabs[0];
        if (reference == null) return _roomSpacing;

        RoomGrid grid = reference.Grid;
        if (grid == null) return _roomSpacing;

        // 여백은 타일 칸 수 기준이라 프리팹 Scale이 커도 같은 비율로 벌어진다
        return grid.WorldSize + _roomGapCells * grid.WorldCellSize;
    }

    private void PlacePlayer()
    {
        if (_player == null)
        {
            Debug.LogWarning("DungeonGenerator2D: Player 칸이 비어 있어서 플레이어를 배치하지 못했습니다.");
            return;
        }

        if (_rooms == null) return;

        DungeonRoom startRoom = _rooms[StartCell.x, StartCell.y];
        if (startRoom == null)
        {
            Debug.LogWarning("DungeonGenerator2D: 시작방이 생성되지 않아 플레이어를 배치하지 못했습니다. Start Room Prefab을 확인하세요.");
            return;
        }

        _player.EnterRoom(startRoom.Grid, startRoom.Grid.Center);
    }

    /// <summary>그리드 좌표의 방을 반환 (없으면 null)</summary>
    public DungeonRoom GetRoom(Vector2Int cell)
    {
        if (_rooms == null || !InBounds(cell)) return null;
        return _rooms[cell.x, cell.y];
    }

    private DungeonRoom GetPrefab(Vector2Int cell)
    {
        if (cell == StartCell) return _startRoomPrefab;
        if (_specialAssign != null && _specialAssign.TryGetValue(cell, out var special)) return special;
        return PickNormalPrefab();
    }

    // ───────────────────────── 일반방 프리팹 선택: 안 나온 것 위주 ─────────────────────────

    private void RefillPool()
    {
        _unusedPool.Clear();
        foreach (var p in _normalRoomPrefabs)
            if (p != null) _unusedPool.Add(p);
    }

    private DungeonRoom PickNormalPrefab()
    {
        // 한 번씩 다 나왔으면 풀을 다시 채움
        if (_unusedPool.Count == 0) RefillPool();
        if (_unusedPool.Count == 0) return null;

        int idx = Random.Range(0, _unusedPool.Count);
        DungeonRoom picked = _unusedPool[idx];
        _unusedPool.RemoveAt(idx);
        return picked;
    }

    // ───────────────────────── 유틸 ─────────────────────────

    private bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.x < _gridX && p.y >= 0 && p.y < _gridY;
    }

    private bool HasRoom(int x, int y)
    {
        return x >= 0 && x < _gridX && y >= 0 && y < _gridY && _map[x, y];
    }

    private int CountNeighbors(Vector2Int p)
    {
        int count = 0;
        foreach (var dir in Directions)
        {
            Vector2Int n = p + dir;
            if (HasRoom(n.x, n.y)) count++;
        }
        return count;
    }

    private static void Shuffle(Vector2Int[] source, out Vector2Int[] result)
    {
        result = (Vector2Int[])source.Clone();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
    }
}