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

    [Header("카메라 플레이 영역 여백 (화면 비율 0~1)")]
    [Tooltip("UI가 차지하는 영역을 비워두는 값. 방은 남은 영역 안에 맞춰서 보인다")]
    [Range(0f, 0.8f)][SerializeField] private float _marginLeft = 0f;
    [Range(0f, 0.8f)][SerializeField] private float _marginRight = 0f;
    [Range(0f, 0.8f)][SerializeField] private float _marginTop = 0f;
    [Range(0f, 0.8f)][SerializeField] private float _marginBottom = 0.3f;
    [Tooltip("스프라이트 크기를 타일 1칸에 맞춰 자동으로 스케일")]
    [SerializeField] private bool _fitToCell = true;
    [Range(0.3f, 1f)]
    [SerializeField] private float _cellFill = 0.9f;

    public RoomGrid CurrentRoom { get; private set; }
    public Vector2Int Cell { get; private set; }
    public bool IsMoving { get; private set; }
    public Camera Camera => _camera;

    /// <summary>true 면 키 입력을 받지 않는다 (방 이동 연출, 전투 시작 연출 등)</summary>
    public bool InputLocked { get; set; }

    /// <summary>한 칸 이동을 마쳤을 때 (이동 후 좌표)</summary>
    public event Action<Vector2Int> Moved;

    /// <summary>열린 문 칸에 도착했을 때</summary>
    public event Action<DoorDirection> DoorEntered;

    /// <summary>방에 배치 (생성 직후나 방 이동 시 호출)</summary>
    public void EnterRoom(RoomGrid room, Vector2Int cell, bool snapCamera = true)
    {
        StopAllCoroutines();
        IsMoving = false;

        CurrentRoom = room;
        Cell = cell;

        if (_fitToCell) room.FitToCell(transform, _cellFill);

        Vector3 p = room.GridToWorld(cell);
        transform.position = new Vector3(p.x, p.y, transform.position.z);

        if (snapCamera) SnapCameraTo(room);
    }

    /// <summary>카메라를 그 방 중심으로 즉시 옮긴다</summary>
    public void SnapCameraTo(RoomGrid room)
    {
        if (_camera == null) return;

        GetCameraTarget(room, out Vector3 pos, out float size);
        _camera.transform.position = pos;

        if (_camera.orthographic)
            _camera.orthographicSize = size;
    }

    /// <summary>그 방 전체가 화면에 들어오는 카메라 Size</summary>
    public float GetCameraSize(RoomGrid room)
    {
        GetCameraTarget(room, out _, out float size);
        return size;
    }

    /// <summary>
    /// 그 방을 보여줄 카메라의 위치와 Size.
    /// 방은 화면에서 여백(UI 영역)을 뺀 "플레이 영역" 안에 들어오고 그 영역의 중앙에 놓인다.
    /// </summary>
    public void GetCameraTarget(RoomGrid room, out Vector3 position, out float size)
    {
        Vector3 center = room.WorldCenter;
        position = new Vector3(center.x, center.y, _camera != null ? _camera.transform.position.z : 0f);
        size = _camera != null ? _camera.orthographicSize : 0f;

        if (_camera == null || !_fitCamera || !_camera.orthographic) return;

        float l = _marginLeft, r = _marginRight, t = _marginTop, b = _marginBottom;
        float areaW = Mathf.Max(0.1f, 1f - l - r);   // 플레이 영역 가로 (화면 비율)
        float areaH = Mathf.Max(0.1f, 1f - t - b);   // 플레이 영역 세로 (화면 비율)

        // 방(벽 포함) + 여백이 플레이 영역 안에 딱 들어오는 Size
        Vector2 roomSize = room.WorldSize;
        float pad = _cameraPadding * room.WorldCellSize;
        float needW = roomSize.x + pad * 2f;
        float needH = roomSize.y + pad * 2f;
        size = Mathf.Max(needH / (2f * areaH), needW / (2f * _camera.aspect * areaW));

        // 플레이 영역의 중앙이 방 중심에 오도록 카메라를 이동
        float areaCenterX = (1f + l - r) * 0.5f;
        float areaCenterY = (1f + b - t) * 0.5f;
        position.x = center.x - (areaCenterX - 0.5f) * 2f * size * _camera.aspect;
        position.y = center.y - (areaCenterY - 0.5f) * 2f * size;
    }

    /// <summary>플레이어 스프라이트 표시/숨김 (방 이동 연출용)</summary>
    public void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }

    private void Update()
    {
        if (CurrentRoom == null || IsMoving || InputLocked) return;

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