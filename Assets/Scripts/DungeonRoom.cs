using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>RoomData 로 생성된 물체(몬스터, 상자 등)에 붙이면 생성 직후 자기 좌표를 알려준다</summary>
public interface IRoomSpawned
{
    void OnRoomSpawned(DungeonRoom room, Vector2Int cell, RoomSpawnEntry entry);
}

/// <summary>방이 생성해 둔 물체 기록</summary>
public class SpawnedEntity
{
    public GameObject Object;
    public Vector2Int Cell;
    public RoomSpawnEntry Entry;
}

[RequireComponent(typeof(RoomGrid))]
public class DungeonRoom : MonoBehaviour
{
    [Header("출입구 오브젝트")]
    [SerializeField] private GameObject northDoor;
    [SerializeField] private GameObject southDoor;
    [SerializeField] private GameObject eastDoor;
    [SerializeField] private GameObject westDoor;

    [Header("방 내용 데이터")]
    [SerializeField] private RoomData _data;

    private RoomGrid _grid;
    private readonly List<SpawnedEntity> _spawned = new List<SpawnedEntity>();

    /// <summary>이 방의 이동 판정용 그리드</summary>
    public RoomGrid Grid
    {
        get
        {
            if (_grid == null) _grid = GetComponent<RoomGrid>();
            return _grid;
        }
    }

    public RoomData Data => _data;

    /// <summary>던전 전체 맵에서의 칸 좌표 (생성기가 설정)</summary>
    public Vector2Int MapCell { get; private set; }

    public bool IsVisited { get; private set; }
    public bool IsCleared { get; private set; }
    public IReadOnlyList<SpawnedEntity> Spawned => _spawned;

    /// <summary>방이 클리어되었을 때</summary>
    public event Action<DungeonRoom> Cleared;

    public void SetMapCell(Vector2Int cell)
    {
        MapCell = cell;
    }

    public void SetDoor(bool north, bool south, bool east, bool west)
    {
        if (northDoor != null)
            northDoor.SetActive(north);

        if (southDoor != null)
            southDoor.SetActive(south);

        if (eastDoor != null)
            eastDoor.SetActive(east);

        if (westDoor != null)
            westDoor.SetActive(west);

        // 이동 판정에도 같은 정보를 적용 (열린 문만 통과 가능)
        Grid.SetDoors(north, south, east, west);
    }

    // ───────────────────────── 방문 / 생성 / 클리어 ─────────────────────────

    /// <summary>
    /// 방에 처음 방문할 때 내용을 생성한다.
    /// 처음이면 true, 이미 방문했던 방이면 false (다시 생성하지 않는다)
    /// </summary>
    public bool Visit()
    {
        if (IsVisited) return false;
        IsVisited = true;

        SpawnContents();

        if (_data == null || _data.ClearedOnEnter)
            SetCleared();

        return true;
    }

    private void SpawnContents()
    {
        if (_data == null) return;

        foreach (RoomSpawnEntry entry in _data.Spawns)
        {
            if (entry == null || entry.prefab == null) continue;

            if (!Grid.InBounds(entry.cell))
            {
                Debug.LogWarning($"{name}: 생성 좌표 {entry.cell} 가 바닥 범위를 벗어나 건너뜁니다.", this);
                continue;
            }

            if (IsDoorFrontCell(entry.cell))
                Debug.LogWarning($"{name}: 생성 좌표 {entry.cell} 는 문 바로 앞 칸이라 입장이 막힐 수 있습니다.", this);

            GameObject go = Instantiate(entry.prefab, Grid.GridToWorld(entry.cell), Quaternion.identity, transform);

            if (entry.fitToCell)
                Grid.FitToCell(go.transform, entry.fillRatio);

            if (entry.blocksMovement)
                Grid.SetBlocked(entry.cell, true);

            _spawned.Add(new SpawnedEntity { Object = go, Cell = entry.cell, Entry = entry });

            foreach (IRoomSpawned callback in go.GetComponentsInChildren<IRoomSpawned>())
                callback.OnRoomSpawned(this, entry.cell, entry);
        }
    }

    private bool IsDoorFrontCell(Vector2Int cell)
    {
        for (int i = 0; i < 4; i++)
        {
            if (Grid.GetDoorFrontCell((DoorDirection)i) == cell) return true;
        }
        return false;
    }

    /// <summary>생성된 물체를 제거하고 막아둔 칸을 푼다 (몬스터 처치, 상자 개봉 등)</summary>
    public void RemoveSpawned(GameObject go, bool destroy = true)
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i].Object != go) continue;

            if (_spawned[i].Entry.blocksMovement)
                Grid.SetBlocked(_spawned[i].Cell, false);

            _spawned.RemoveAt(i);
            break;
        }

        if (destroy && go != null) Destroy(go);
    }

    /// <summary>방 클리어 처리. 잠긴 문이 풀리고 Cleared 이벤트가 발생한다</summary>
    public void SetCleared()
    {
        if (IsCleared) return;

        IsCleared = true;
        Grid.DoorsLocked = false;
        Cleared?.Invoke(this);
    }

    // ───────────────────────── 에디터 표시 ─────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        float size = Grid.WorldCellSize * 0.7f;
        foreach (RoomSpawnEntry entry in _data.Spawns)
        {
            if (entry == null) continue;

            switch (entry.kind)
            {
                case RoomSpawnKind.Monster: Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f); break;
                case RoomSpawnKind.Chest: Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f); break;
                case RoomSpawnKind.Obstacle: Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.6f); break;
                default: Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f); break;
            }

            Gizmos.DrawCube(Grid.GridToWorld(entry.cell), new Vector3(size, size, 0.01f));
        }
    }
}