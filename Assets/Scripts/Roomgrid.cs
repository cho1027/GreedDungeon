using System.Collections.Generic;
using UnityEngine;

public enum DoorDirection
{
    North = 0, // 위쪽 문  (5, 7)
    South = 1, // 아래쪽 문 (5, -1)
    East = 2,  // 오른쪽 문 (11, 3)
    West = 3   // 왼쪽 문  (-1, 3)
}

/// <summary>
/// 방 프리팹에 붙는 이동 판정용 그리드. (배경 이미지와는 별개)
/// - 좌표는 왼쪽 아래 바닥 타일이 (0, 0)
/// - 기본 11 x 7 은 "바닥"만의 크기이며 벽은 포함하지 않는다
/// - 벽은 바닥 바깥 한 겹(-1 ~ width/height)에 있고, 문은 그 벽 위 네 방향 중앙에 있다
/// - 방 중심이 프리팹 Transform 위치
/// </summary>
public class RoomGrid : MonoBehaviour
{
    /// <summary>벽 두께(칸 수)</summary>
    public const int WallThickness = 1;

    [Header("바닥 그리드 크기 (벽 제외)")]
    [SerializeField] private int _width = 11;
    [SerializeField] private int _height = 7;
    [SerializeField] private float _cellSize = 1f;

    [Tooltip("배경 이미지와 그리드가 안 맞을 때 그리드 중심을 옮기는 값 (로컬 좌표)")]
    [SerializeField] private Vector2 _centerOffset = Vector2.zero;

    [Header("옵션")]
    [SerializeField] private bool _drawGizmos = true;

    private readonly bool[] _doorOpen = new bool[4];
    private readonly HashSet<Vector2Int> _blocked = new HashSet<Vector2Int>();

    public int Width => _width;
    public int Height => _height;
    public float CellSize => _cellSize;
    public Vector2Int Center => new Vector2Int(_width / 2, _height / 2);

    /// <summary>그리드 중심의 월드 좌표 (카메라 맞출 때 사용)</summary>
    public Vector3 WorldCenter => transform.TransformPoint(_centerOffset);

    /// <summary>프리팹 Scale까지 반영한 타일 1칸의 월드 크기</summary>
    public float WorldCellSize => _cellSize * Mathf.Abs(transform.lossyScale.x);

    /// <summary>프리팹 Scale까지 반영한 바닥 영역의 월드 크기 (벽 제외)</summary>
    public Vector2 FloorWorldSize => new Vector2(
        _width * _cellSize * Mathf.Abs(transform.lossyScale.x),
        _height * _cellSize * Mathf.Abs(transform.lossyScale.y));

    /// <summary>프리팹 Scale까지 반영한 방 전체의 월드 크기 (벽 포함). 방 간격/카메라 계산에 사용</summary>
    public Vector2 WorldSize => new Vector2(
        (_width + WallThickness * 2) * _cellSize * Mathf.Abs(transform.lossyScale.x),
        (_height + WallThickness * 2) * _cellSize * Mathf.Abs(transform.lossyScale.y));

    private void OnValidate()
    {
        _width = Mathf.Max(1, _width);
        _height = Mathf.Max(1, _height);
        _cellSize = Mathf.Max(0.01f, _cellSize);
    }

    // ───────────────────────── 좌표 변환 ─────────────────────────

    private Vector2 CellToLocal(Vector2Int cell)
    {
        return new Vector2(
            (cell.x - (_width - 1) * 0.5f) * _cellSize,
            (cell.y - (_height - 1) * 0.5f) * _cellSize) + _centerOffset;
    }

    /// <summary>벽/문 칸(바닥 바깥 좌표)도 변환 가능</summary>
    public Vector3 GridToWorld(Vector2Int cell)
    {
        return transform.TransformPoint(CellToLocal(cell));
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.x -= _centerOffset.x;
        local.y -= _centerOffset.y;

        int x = Mathf.RoundToInt(local.x / _cellSize + (_width - 1) * 0.5f);
        int y = Mathf.RoundToInt(local.y / _cellSize + (_height - 1) * 0.5f);
        return new Vector2Int(x, y);
    }

    // ───────────────────────── 문 ─────────────────────────

    /// <summary>문 칸 좌표. 바닥 바깥 벽 위에 있다 (예: 왼쪽 문 = (-1, 3))</summary>
    public Vector2Int GetDoorCell(DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.North: return new Vector2Int(_width / 2, _height);
            case DoorDirection.South: return new Vector2Int(_width / 2, -1);
            case DoorDirection.East: return new Vector2Int(_width, _height / 2);
            default: return new Vector2Int(-1, _height / 2); // West
        }
    }

    /// <summary>문 앞 바닥 칸 (문으로 나가기 직전 타일)</summary>
    public Vector2Int GetDoorFrontCell(DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.North: return new Vector2Int(_width / 2, _height - 1);
            case DoorDirection.South: return new Vector2Int(_width / 2, 0);
            case DoorDirection.East: return new Vector2Int(_width - 1, _height / 2);
            default: return new Vector2Int(0, _height / 2); // West
        }
    }

    public bool TryGetDoorAt(Vector2Int cell, out DoorDirection dir)
    {
        for (int i = 0; i < 4; i++)
        {
            if (GetDoorCell((DoorDirection)i) == cell)
            {
                dir = (DoorDirection)i;
                return true;
            }
        }

        dir = default;
        return false;
    }

    public bool IsDoorOpen(DoorDirection dir)
    {
        return _doorOpen[(int)dir];
    }

    public void SetDoorOpen(DoorDirection dir, bool open)
    {
        _doorOpen[(int)dir] = open;
    }

    public void SetDoors(bool north, bool south, bool east, bool west)
    {
        _doorOpen[(int)DoorDirection.North] = north;
        _doorOpen[(int)DoorDirection.South] = south;
        _doorOpen[(int)DoorDirection.East] = east;
        _doorOpen[(int)DoorDirection.West] = west;
    }

    // ───────────────────────── 이동 판정 ─────────────────────────

    /// <summary>바닥 영역(11x7) 안인지</summary>
    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
    }

    public bool IsWalkable(Vector2Int cell)
    {
        // 문 칸: 열려 있을 때만 통과
        if (TryGetDoorAt(cell, out DoorDirection dir))
            return _doorOpen[(int)dir];

        // 그 외 바닥 바깥은 전부 벽
        if (!InBounds(cell)) return false;

        return !_blocked.Contains(cell);
    }

    /// <summary>장애물/적 등으로 타일을 막거나 푼다</summary>
    public void SetBlocked(Vector2Int cell, bool blocked)
    {
        if (blocked) _blocked.Add(cell);
        else _blocked.Remove(cell);
    }

    public void ClearBlocked()
    {
        _blocked.Clear();
    }

    // ───────────────────────── 에디터 기즈모 ─────────────────────────

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 size = new Vector3(_cellSize * 0.95f, _cellSize * 0.95f, 0.01f);

        for (int x = -WallThickness; x < _width + WallThickness; x++)
        {
            for (int y = -WallThickness; y < _height + WallThickness; y++)
            {
                var cell = new Vector2Int(x, y);
                Vector2 local = CellToLocal(cell);
                Vector3 pos = new Vector3(local.x, local.y, 0f);

                if (TryGetDoorAt(cell, out DoorDirection dir))
                {
                    // 플레이 중에는 열림(초록)/닫힘(빨강), 에디터에서는 노랑
                    if (Application.isPlaying)
                        Gizmos.color = _doorOpen[(int)dir] ? new Color(0.2f, 1f, 0.2f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f);
                    else
                        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);
                }
                else if (!InBounds(cell))
                {
                    Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f); // 벽
                }
                else
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.25f);     // 바닥
                }

                Gizmos.DrawWireCube(pos, size);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}