using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 턴제용 그리드 이동 플레이어.
/// WASD / 방향키를 한 번 누를 때마다 한 칸 이동한다.
/// 이동 가능 여부는 현재 방의 RoomGrid.IsWalkable 로 판정한다.
/// </summary>
public class GridPlayer : MonoBehaviour
{
    [SerializeField] private float _moveTime = 0.1f;
    [Tooltip("방에 들어올 때 그 방 중심으로 카메라를 옮긴다 (비워두면 무시)")]
    [SerializeField] private Camera _camera;
    [Tooltip("방 크기에 맞게 카메라 Size를 자동 조절 (Orthographic 전용)")]
    [SerializeField] private bool _fitCamera = true;
    [Tooltip("방 가장자리 바깥으로 보여줄 여백 (타일 칸 수)")]
    [SerializeField] private float _cameraPadding = 0.5f;
    [Tooltip("스프라이트 크기를 타일 1칸에 맞춰 자동으로 스케일")]
    [SerializeField] private bool _fitToCell = true;
    [Range(0.3f, 1f)]
    [SerializeField] private float _cellFill = 0.9f;

    public RoomGrid CurrentRoom { get; private set; }
    public Vector2Int Cell { get; private set; }
    public bool IsMoving { get; private set; }

    /// <summary>한 칸 이동을 시작할 때 (이동 후 좌표)</summary>
    public event Action<Vector2Int> Moved;

    /// <summary>열린 문 타일에 도착했을 때</summary>
    public event Action<DoorDirection> DoorEntered;

    /// <summary>방에 배치 (생성 직후나 방 이동 시 호출)</summary>
    public void EnterRoom(RoomGrid room, Vector2Int cell)
    {
        StopAllCoroutines();
        IsMoving = false;

        CurrentRoom = room;
        Cell = cell;

        if (_fitToCell) FitToCell(room);

        Vector3 p = room.GridToWorld(cell);
        transform.position = new Vector3(p.x, p.y, transform.position.z);

        if (_camera != null)
        {
            Vector3 c = room.WorldCenter;
            _camera.transform.position = new Vector3(c.x, c.y, _camera.transform.position.z);

            if (_fitCamera && _camera.orthographic)
            {
                Vector2 size = room.WorldSize;
                float pad = _cameraPadding * room.WorldCellSize;
                float byHeight = size.y * 0.5f + pad;
                float byWidth = (size.x * 0.5f + pad) / _camera.aspect;
                _camera.orthographicSize = Mathf.Max(byHeight, byWidth);
            }
        }
    }

    // 스프라이트가 타일 1칸 안에 들어오도록 스케일 조정
    private void FitToCell(RoomGrid room)
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        transform.localScale = Vector3.one;
        Vector3 size = sr.bounds.size;
        float biggest = Mathf.Max(size.x, size.y);
        if (biggest <= 0f) return;

        float scale = room.WorldCellSize * _cellFill / biggest;
        transform.localScale = Vector3.one * scale;
    }

    private void Update()
    {
        if (CurrentRoom == null || IsMoving) return;

        Vector2Int dir = ReadInput();
        if (dir != Vector2Int.zero)
            TryMove(dir);
    }

    public bool TryMove(Vector2Int dir)
    {
        if (CurrentRoom == null || IsMoving) return false;

        Vector2Int target = Cell + dir;
        if (!CurrentRoom.IsWalkable(target)) return false;

        Cell = target;
        StartCoroutine(MoveRoutine(CurrentRoom.GridToWorld(target)));
        return true;
    }

    private IEnumerator MoveRoutine(Vector3 worldTarget)
    {
        IsMoving = true;

        Vector3 from = transform.position;
        Vector3 to = new Vector3(worldTarget.x, worldTarget.y, from.z);

        float t = 0f;
        while (t < _moveTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / _moveTime));
            yield return null;
        }

        transform.position = to;
        IsMoving = false;

        Moved?.Invoke(Cell);

        if (CurrentRoom.TryGetDoorAt(Cell, out DoorDirection door))
            DoorEntered?.Invoke(door);
    }

    private Vector2Int ReadInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2Int.zero;

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return Vector2Int.up;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return Vector2Int.down;
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return Vector2Int.right;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return Vector2Int.left;
#else
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return Vector2Int.right;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return Vector2Int.left;
#endif
        return Vector2Int.zero;
    }
}