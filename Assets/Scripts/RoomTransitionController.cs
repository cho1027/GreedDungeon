using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 이동을 담당한다.
///  1) 플레이어가 열린 문 칸에 도착하면 이동 시작
///  2) 다음 방의 내용(몬스터/상자 등)을 생성 (처음 방문일 때만)
///  3) 플레이어를 숨기고 카메라를 옆 방으로 슬라이드
///  4) 다음 방의 반대편 문에서 플레이어가 나타나 한 칸 걸어 들어옴
///  5) RoomData 의 입장 트리거 실행 (전투 시작, 보스 연출 등) → 끝나면 조작 가능
/// 다른 시스템은 아래 이벤트를 구독해서 방 이동에 반응할 수 있다.
/// </summary>
public class RoomTransitionController : MonoBehaviour
{
    [SerializeField] private DungeonGenerator2D _generator;
    [SerializeField] private GridPlayer _player;
    [Tooltip("비워두면 GridPlayer 에 지정된 카메라를 사용")]
    [SerializeField] private Camera _camera;

    [Header("연출")]
    [SerializeField] private float _slideTime = 0.6f;
    [SerializeField] private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>현재 플레이어가 있는 방</summary>
    public DungeonRoom CurrentRoom { get; private set; }

    /// <summary>방 이동 연출 중 (입장 트리거 실행 중 포함)</summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>방 이동 시작 (이전 방, 다음 방, 나가는 문 방향). 카메라가 움직이기 직전</summary>
    public event Action<DungeonRoom, DungeonRoom, DoorDirection> TransitionStarted;

    /// <summary>방을 떠날 때 (퇴장 트리거 실행 후)</summary>
    public event Action<DungeonRoom> RoomExited;

    /// <summary>방에 들어왔을 때 (입장 트리거 실행 직전). 시작방에서도 한 번 발생</summary>
    public event Action<DungeonRoom> RoomEntered;

    private Camera Cam => _camera != null ? _camera : (_player != null ? _player.Camera : null);

    private void OnEnable()
    {
        if (_generator == null || _player == null)
        {
            Debug.LogWarning("RoomTransitionController: Generator 와 Player 를 지정해야 합니다.", this);
            return;
        }

        _generator.Generated += OnGenerated;
        _player.DoorEntered += OnDoorEntered;
    }

    private void OnDisable()
    {
        if (_generator != null) _generator.Generated -= OnGenerated;
        if (_player != null) _player.DoorEntered -= OnDoorEntered;
    }

    // 던전이 (재)생성되었을 때: 시작방에서 시작
    private void OnGenerated(DungeonRoom startRoom)
    {
        StopAllCoroutines();
        IsTransitioning = false;

        _player.SetVisible(true);
        _player.InputLocked = true;

        bool first = startRoom.Visit();
        StartCoroutine(BeginRoom(startRoom, first));
    }

    private void OnDoorEntered(DoorDirection dir)
    {
        if (IsTransitioning || CurrentRoom == null) return;

        DungeonRoom next = _generator.GetNeighbor(CurrentRoom, dir);
        if (next == null) return;

        StartCoroutine(TransitionRoutine(CurrentRoom, next, dir));
    }

    // ───────────────────────── 방 이동 ─────────────────────────

    private IEnumerator TransitionRoutine(DungeonRoom from, DungeonRoom to, DoorDirection dir)
    {
        IsTransitioning = true;
        _player.InputLocked = true;

        // 퇴장 트리거
        if (from.Data != null)
        {
            var exitCtx = new RoomContext(from, _player, this, false);
            yield return StartCoroutine(RunTriggers(from.Data.ExitTriggers, exitCtx, false));
        }

        RoomExited?.Invoke(from);
        TransitionStarted?.Invoke(from, to, dir);

        // 카메라가 도착하기 전에 다음 방 내용을 미리 생성 (슬라이드 중 보이도록)
        bool firstVisit = to.Visit();

        // 플레이어는 슬라이드 동안 보이지 않는다
        _player.SetVisible(false);
        yield return StartCoroutine(SlideCamera(to.Grid));

        // 반대편 문에서 나타나 한 칸 걸어 들어온다
        DoorDirection entry = Opposite(dir);
        Vector2Int doorCell = to.Grid.GetDoorCell(entry);
        Vector2Int frontCell = to.Grid.GetDoorFrontCell(entry);

        _player.SetVisible(true);
        _player.EnterRoom(to.Grid, doorCell, false);
        _player.TryMove(frontCell - doorCell);
        while (_player.IsMoving) yield return null;

        // 입장 트리거 (전투 시작, 보스 연출 등)
        yield return StartCoroutine(BeginRoom(to, firstVisit));

        IsTransitioning = false;
    }

    private IEnumerator SlideCamera(RoomGrid to)
    {
        Camera cam = Cam;
        if (cam == null) yield break;

        Vector3 fromPos = cam.transform.position;
        float fromSize = cam.orthographicSize;

        // 플레이 영역 여백이 반영된 목표 위치/Size
        _player.GetCameraTarget(to, out Vector3 toPos, out float toSize);
        toPos.z = fromPos.z;
        if (!cam.orthographic) toSize = fromSize;

        float t = 0f;
        while (t < _slideTime)
        {
            t += Time.deltaTime;
            float k = _slideCurve.Evaluate(Mathf.Clamp01(t / _slideTime));
            cam.transform.position = Vector3.LerpUnclamped(fromPos, toPos, k);
            if (cam.orthographic) cam.orthographicSize = Mathf.LerpUnclamped(fromSize, toSize, k);
            yield return null;
        }

        cam.transform.position = toPos;
        if (cam.orthographic) cam.orthographicSize = toSize;
    }

    // ───────────────────────── 방 입장 처리 ─────────────────────────

    private IEnumerator BeginRoom(DungeonRoom room, bool firstVisit)
    {
        CurrentRoom = room;
        _player.InputLocked = true;

        RoomEntered?.Invoke(room);

        if (room.Data != null)
        {
            var ctx = new RoomContext(room, _player, this, firstVisit);
            yield return StartCoroutine(RunTriggers(room.Data.EnterTriggers, ctx, true));
        }

        _player.InputLocked = false;
    }

    private IEnumerator RunTriggers(IReadOnlyList<RoomTrigger> triggers, RoomContext ctx, bool entering)
    {
        if (triggers == null) yield break;

        for (int i = 0; i < triggers.Count; i++)
        {
            RoomTrigger trigger = triggers[i];
            if (trigger == null) continue;

            yield return StartCoroutine(entering ? trigger.OnRoomEnter(ctx) : trigger.OnRoomExit(ctx));
        }
    }

    private static DoorDirection Opposite(DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.North: return DoorDirection.South;
            case DoorDirection.South: return DoorDirection.North;
            case DoorDirection.East: return DoorDirection.West;
            default: return DoorDirection.East;
        }
    }
}