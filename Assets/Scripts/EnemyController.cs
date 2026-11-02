using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 기본 적. 모든 몬스터가 공통으로 쓰는 "행동 예고 → 적 턴에 실행 → 다음 행동 결정" 흐름만 담당한다.
/// - 무엇을 할지: EnemyActionSelector (선택 로직)
/// - 어떻게 할지: EnemyAction (실행 로직, 공격·방어 등)
/// - 체력/방어막/사망: EnemyHealth, DamageShield
/// - 표시: EnemyIntentView (머리 위 아이콘·수치), AttackRangeIndicator (바닥 범위)
///
/// 턴 흐름
///  1) 전투 시작 → 첫 행동 확정, 아이콘과 공격 범위 예고 (플레이어 턴 동안 보임)
///  2) 적 턴 → 이전 턴의 방어막 소멸 → 예고한 행동 그대로 실행 → 범위 표시 제거
///  3) 다음 행동 확정 → 아이콘·범위 갱신 → 플레이어 턴
///
/// 몬스터 프리팹에 붙이고, RoomData 의 Spawns 에 Kind = Monster 로 등록한다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour, IEnemyTurnActor, IEnemyCombatListener, IRoomSpawned
{
    [Header("행동 선택")]
    [SerializeField] private EnemyActionSelector _selector;

    [Header("방향")]
    [Tooltip("처음 바라보는 방향")]
    [SerializeField] private DoorDirection _initialFacing = DoorDirection.South;
    [Tooltip("바라보는 방향이 왼쪽이면 스프라이트를 좌우 반전 (선택)")]
    [SerializeField] private SpriteRenderer _bodySprite;
    [SerializeField] private bool _flipWhenFacingLeft = true;

    [Header("연출")]
    [Tooltip("행동 하나를 실행하기 전 대기 시간 (예고 확인용)")]
    [Min(0f)][SerializeField] private float _actionDelay = 0.35f;

    [Header("구성 요소 (비워두면 이 오브젝트와 자식에서 찾는다)")]
    [SerializeField] private EnemyHealth _health;
    [SerializeField] private DamageShield _shield;
    [SerializeField] private EnemyIntentView _intentView;
    [SerializeField] private AttackRangeIndicator _rangeIndicator;

    private bool _hasCell;

    public DungeonRoom Room { get; private set; }

    /// <summary>방 안 격자 좌표 (이동 행동이 생기면 그 행동에서 갱신)</summary>
    public Vector2Int Cell { get; private set; }

    /// <summary>바라보는 방향 (상하좌우 단위 벡터, GridPlayer.TryMove 와 같은 체계)</summary>
    public Vector2Int Facing { get; private set; }

    public BattleTurnManager Manager { get; private set; }
    public GridPlayer Player => Manager != null ? Manager.Player : null;
    public EnemyHealth Health => _health;
    public DamageShield Shield => _shield;
    public float ActionDelay => _actionDelay;

    /// <summary>현재 예고 중인 다음 행동 (이 적 전용)</summary>
    public EnemyIntent CurrentIntent { get; private set; }

    /// <summary>선택 규칙용 기록: 마지막으로 실행한 행동, 연속 횟수, 총 실행 횟수</summary>
    public EnemyAction LastAction { get; private set; }
    public int ConsecutiveCount { get; private set; }
    public int ActionCount { get; private set; }

    public bool IsAlive => _health == null || !_health.IsDead;

    /// <summary>다음 행동이 확정되거나 지워졌을 때 (null = 예고 없음)</summary>
    public event Action<EnemyIntent> IntentChanged;

    // ───────────────────────── 초기화 ─────────────────────────

    private void Awake()
    {
        if (_health == null) _health = GetComponent<EnemyHealth>();
        if (_shield == null) _shield = GetComponent<DamageShield>();
        if (_intentView == null) _intentView = GetComponentInChildren<EnemyIntentView>(true);
        if (_rangeIndicator == null) _rangeIndicator = GetComponentInChildren<AttackRangeIndicator>(true);
        if (_bodySprite == null) _bodySprite = GetComponent<SpriteRenderer>();

        Face(AttackArea.ToVector(_initialFacing));
    }

    private void OnEnable()
    {
        if (_health != null) _health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (_health != null) _health.Died -= OnDied;
        ClearIntent();
    }

    public void OnRoomSpawned(DungeonRoom room, Vector2Int cell, RoomSpawnEntry entry)
    {
        Room = room;
        Cell = cell;
        _hasCell = true;
    }

    // ───────────────────────── 턴 ─────────────────────────

    public void OnCombatStarted(BattleTurnManager manager)
    {
        Manager = manager;

        // 이전 전투(방을 나갔다 온 경우)의 방어막, 예고는 버리고 새로 시작
        if (_shield != null) _shield.Clear();
        PlanNextAction();
    }

    public IEnumerator TakeTurn(BattleTurnManager manager)
    {
        Manager = manager;
        if (!CheckReady()) yield break;

        // 1) 방어막 소멸: 지난 적 턴에 얻은 방어막은 플레이어 턴 1번 동안만 유지
        if (_shield != null) _shield.Clear();

        // 예고 없이 턴이 온 경우(전투 도중 생겨난 적 등): 이번 턴은 예고만 하고 넘긴다
        EnemyIntent intent = CurrentIntent;
        if (intent == null || intent.Action == null)
        {
            PlanNextAction();
            yield break;
        }

        // 2) 예고한 행동 그대로 실행
        IEnumerator routine = intent.Action.Execute(this, intent);
        if (routine != null) yield return StartCoroutine(routine);

        RecordAction(intent.Action);

        // 3) 범위 표시 제거
        ClearIntent();

        if (!IsAlive || manager.IsPlayerDead) yield break;

        // 4) 다음 행동 결정 + 아이콘·범위 갱신
        PlanNextAction();
    }

    // ───────────────────────── 행동 결정 / 표시 ─────────────────────────

    /// <summary>다음 행동을 고르고, 방향·범위·수치를 확정해 예고한다</summary>
    public void PlanNextAction()
    {
        ClearIntent();
        if (!CheckReady()) return;

        if (_selector == null)
        {
            Debug.LogWarning($"{name}: Action Selector 가 연결되지 않아 행동하지 않습니다.", this);
            return;
        }

        EnemyAction action = _selector.Choose(this);
        if (action == null) return;

        var intent = new EnemyIntent(action);
        action.Plan(this, intent);
        CurrentIntent = intent;

        if (_intentView != null) _intentView.Show(intent);
        if (_rangeIndicator != null && Room != null && intent.Cells.Count > 0)
            _rangeIndicator.Show(Room.Grid, intent.Cells);

        IntentChanged?.Invoke(intent);
    }

    /// <summary>예고(아이콘, 범위 표시)를 지운다</summary>
    public void ClearIntent()
    {
        bool had = CurrentIntent != null;
        CurrentIntent = null;

        if (_intentView != null) _intentView.Hide();
        if (_rangeIndicator != null) _rangeIndicator.Hide();

        if (had) IntentChanged?.Invoke(null);
    }

    /// <summary>바라보는 방향을 바꾼다 (상하좌우만)</summary>
    public void Face(Vector2Int dir)
    {
        if (dir == Vector2Int.zero) return;
        Facing = dir;

        if (_bodySprite != null && _flipWhenFacingLeft && dir.x != 0)
            _bodySprite.flipX = dir.x < 0;
    }

    // ───────────────────────── 내부 ─────────────────────────

    private void RecordAction(EnemyAction action)
    {
        ConsecutiveCount = action == LastAction ? ConsecutiveCount + 1 : 1;
        LastAction = action;
        ActionCount++;
    }

    private bool CheckReady()
    {
        if (_hasCell && Room != null) return true;

        Debug.LogWarning($"{name}: 방 정보가 없습니다. RoomData 의 Spawns 로 생성된 몬스터인지 확인하세요.", this);
        return false;
    }

    private void OnDied(EnemyHealth health)
    {
        ClearIntent();
    }
}
