using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>적 등급 (사령술사가 죽을 때 "일반 몬스터 모두 사망" 등에 쓴다)</summary>
public enum EnemyRank
{
    Normal = 0,
    Elite,
    Boss
}

/// <summary>행동을 끝낸 뒤 이어서 하는 이동</summary>
public enum EnemyPostMove
{
    None = 0,     // 움직이지 않음 (고정형 보스 등)
    Chase,        // 다음 공격 범위에 플레이어가 없으면(근처에 없으면) 다가간다
    KeepDistance  // 플레이어가 가까우면 후퇴, 사거리 밖이면 다가간다 (원거리형)
}

/// <summary>
/// 기본 적. 모든 몬스터가 공통으로 쓰는 "행동 예고 → 적 턴에 실행 → 다음 행동 결정" 흐름만 담당한다.
/// - 무엇을 할지: EnemyActionSelector (선택 로직)
/// - 어떻게 할지: EnemyAction (실행 로직, 공격·방어 등)
/// - 체력/방어막/사망: EnemyHealth, DamageShield
/// - 상태 이상: StatusEffectHolder (자동으로 붙는다)
/// - 표시: EnemyIntentView (머리 위 아이콘·수치), AttackRangeIndicator (바닥 범위)
///
/// 턴 흐름
///  1) 전투 시작 → 첫 행동 확정, 아이콘과 공격 범위 예고 (플레이어 턴 동안 보임)
///  2) 적 턴 → 방어막 시간 경과 (+ 방어도만큼 방어막) → 예고한 행동 그대로 실행 → 범위 표시 제거
///  3) 다음 행동 확정
///  4) 행동 뒤 이동 (Post Move): 다음 공격 범위에 플레이어가 없으면 다가가거나, 가까우면 후퇴
///     → 이동했으면 같은 행동을 새 위치 기준으로 다시 예고 ("공격 → 이동" 두 번의 행동이 보인다)
///  5) 상태 이상 턴 처리 → 플레이어 턴
///
/// 몬스터 프리팹에 붙이고, RoomData 의 Spawns 에 Kind = Monster 로 등록한다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour, IEnemyTurnActor, IEnemyCombatListener, IRoomSpawned
{
    [Header("행동 선택")]
    [SerializeField] private EnemyActionSelector _selector;

    [Header("등급")]
    [SerializeField] private EnemyRank _rank = EnemyRank.Normal;

    [Header("방향")]
    [Tooltip("처음 바라보는 방향")]
    [SerializeField] private DoorDirection _initialFacing = DoorDirection.South;
    [Tooltip("바라보는 방향이 왼쪽이면 스프라이트를 좌우 반전 (선택)")]
    [SerializeField] private SpriteRenderer _bodySprite;
    [SerializeField] private bool _flipWhenFacingLeft = true;

    [Header("행동 뒤 이동")]
    [Tooltip("None: 이동 안 함 / Chase: 근처에 없으면 다가감 / KeepDistance: 가까우면 후퇴, 멀면 다가감")]
    [SerializeField] private EnemyPostMove _postMove = EnemyPostMove.Chase;
    [Tooltip("다가갈 때 한 번에 움직이는 칸 수")]
    [Min(1)][SerializeField] private int _chaseSteps = 1;
    [Tooltip("다음 행동에 공격 범위가 없을 때(방어 등), 플레이어와의 거리가 이 값 이하면 '근처'로 보고 움직이지 않는다")]
    [Min(1)][SerializeField] private int _nearDistance = 1;
    [Tooltip("KeepDistance: 플레이어와의 거리가 이 값 이하면 후퇴")]
    [Min(1)][SerializeField] private int _retreatDistance = 3;
    [Tooltip("KeepDistance: 후퇴할 때 움직이는 칸 수")]
    [Min(1)][SerializeField] private int _retreatSteps = 2;

    [Header("연출")]
    [Tooltip("행동 하나를 실행하기 전 대기 시간 (예고 확인용)")]
    [Min(0f)][SerializeField] private float _actionDelay = 0.35f;
    [Tooltip("한 칸 이동하는 데 걸리는 시간")]
    [Min(0.01f)][SerializeField] private float _moveTime = 0.12f;
    [Tooltip("행동과 이동 사이 대기 시간")]
    [Min(0f)][SerializeField] private float _postMoveDelay = 0.2f;

    [Header("보스")]
    [Tooltip("켜면 이 적이 죽는 즉시 스테이지 클리어 (남은 부하가 있어도). 보스방 RoomData 의 Room Type 이 Boss 면 방의 적을 모두 잡아도 클리어")]
    [SerializeField] private bool _isBoss;

    [Header("구성 요소 (비워두면 이 오브젝트와 자식에서 찾는다)")]
    [SerializeField] private EnemyHealth _health;
    [SerializeField] private DamageShield _shield;
    [SerializeField] private EnemyIntentView _intentView;
    [SerializeField] private AttackRangeIndicator _rangeIndicator;

    private bool _hasCell;
    private StatusEffectHolder _status;
    private int _baseStrength;
    private int _phaseStartActionCount;

    public DungeonRoom Room { get; private set; }

    /// <summary>방 안 격자 좌표 (이동 행동이 생기면 그 행동에서 갱신)</summary>
    public Vector2Int Cell { get; private set; }

    /// <summary>바라보는 방향 (상하좌우 단위 벡터, GridPlayer.TryMove 와 같은 체계)</summary>
    public Vector2Int Facing { get; private set; }

    public BattleTurnManager Manager { get; private set; }
    public GridPlayer Player => Manager != null ? Manager.Player : null;
    public EnemyHealth Health => _health;
    public DamageShield Shield => _shield;
    public StatusEffectHolder Status => _status;
    public float ActionDelay => _actionDelay;
    public EnemyRank Rank => _rank;
    public SpriteRenderer BodySprite => _bodySprite;

    /// <summary>현재 예고 중인 다음 행동 (이 적 전용)</summary>
    public EnemyIntent CurrentIntent { get; private set; }

    /// <summary>선택 규칙용 기록: 마지막으로 실행한 행동, 연속 횟수, 총 실행 횟수</summary>
    public EnemyAction LastAction { get; private set; }
    public int ConsecutiveCount { get; private set; }
    public int ActionCount { get; private set; }

    /// <summary>현재 페이즈(PhaseActionSelector)에 들어온 뒤 실행한 행동 수. 페이즈가 없으면 ActionCount 와 같다</summary>
    public int PhaseActionCount => ActionCount - _phaseStartActionCount;

    /// <summary>PhaseActionSelector 가 쓰는 현재 페이즈 번호 (-1 = 아직 없음)</summary>
    public int SelectorPhase { get; private set; } = -1;

    /// <summary>힘: 공격 피해에 더해진다 (패턴의 힘 단계로 증감, 광폭화 수치 포함)</summary>
    public int Strength => _baseStrength + (_status != null ? _status.GetValue(StatusType.Rage) : 0);

    /// <summary>방어도: 매 턴 시작마다 이 수치만큼 사라지지 않는 방어막이 쌓인다 (망자의 병사 '방어')</summary>
    public int Armor { get; private set; }

    /// <summary>선택 규칙/행동이 적마다 따로 저장하는 값 (사령술사의 가방, 소환한 병사 목록 등)</summary>
    public readonly Dictionary<string, object> Memory = new Dictionary<string, object>();

    public bool IsBoss => _isBoss;
    public float MoveTime => _moveTime;
    public EnemyPostMove PostMove => _postMove;

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
        _status = StatusEffectHolder.GetOrAdd(gameObject);

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

        // 1) 방어막: 기한이 지난 방어막은 사라지고, 방어도만큼 새로 쌓인다
        if (_shield != null)
        {
            _shield.TickTurn();
            if (Armor > 0) _shield.Add(Armor, 0);
        }

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

        // 5) 행동 뒤 이동 → 이동했으면 같은 행동을 새 위치 기준으로 다시 예고
        if (TryGetPostMove(out bool retreat))
        {
            if (_postMoveDelay > 0f) yield return new WaitForSeconds(_postMoveDelay);

            Vector2Int before = Cell;
            if (retreat) yield return StartCoroutine(MoveAwayFromPlayer(_retreatSteps));
            else yield return StartCoroutine(MoveTowardPlayer(_chaseSteps));

            if (!IsAlive) yield break;
            if (Cell != before) RefreshIntent();
        }

        // 6) 상태 이상 턴 처리 (광폭화 남은 턴 등)
        if (_status != null) _status.TickTurnEnd(_health);
        if (IsAlive && CurrentIntent != null) RefreshIntent(); // 광폭화가 끝나면 예고 수치도 바뀐다
    }

    /// <summary>행동 뒤 이동이 필요한지 (retreat = 후퇴)</summary>
    private bool TryGetPostMove(out bool retreat)
    {
        retreat = false;
        if (_postMove == EnemyPostMove.None || !IsPlayerInRoom) return false;
        if (_status != null && _status.Has(StatusType.Root)) return false;

        int distance = DistanceToPlayer();

        if (_postMove == EnemyPostMove.KeepDistance && distance <= _retreatDistance)
        {
            retreat = true;
            return true;
        }

        EnemyIntent next = CurrentIntent;
        if (next != null && next.Cells.Count > 0)
            return !next.Cells.Contains(Player.Cell); // 다음 공격이 닿지 않으면 다가간다

        return distance > _nearDistance;              // 공격 범위가 없는 행동이면 거리로 판단
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

        ShowIntent(action);
    }

    /// <summary>지금 예고 중인 행동을 현재 위치·상태 기준으로 다시 계산해 예고한다 (선택은 다시 하지 않는다)</summary>
    public void RefreshIntent()
    {
        EnemyAction action = CurrentIntent != null ? CurrentIntent.Action : null;
        if (action == null) return;
        ClearIntent();
        ShowIntent(action);
    }

    private void ShowIntent(EnemyAction action)
    {
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

    // ───────────────────────── 능력치 / 설정 변경 ─────────────────────────

    public void AddStrength(int amount)
    {
        _baseStrength += amount;
    }

    public void AddArmor(int amount)
    {
        Armor = Mathf.Max(0, Armor + amount);
    }

    /// <summary>행동 뒤 이동 방식을 바꾼다 (보스 2페이즈 등)</summary>
    public void SetPostMove(EnemyPostMove mode, int chaseSteps = 1)
    {
        _postMove = mode;
        _chaseSteps = Mathf.Max(1, chaseSteps);
    }

    /// <summary>PhaseActionSelector 가 호출. 새 페이즈부터 행동 순번을 다시 0 으로 셀 수 있게 한다</summary>
    /// <param name="skipNextAction">이번에 페이즈 진입 행동을 하면 true (그 행동은 순번에서 뺀다)</param>
    public void EnterSelectorPhase(int phase, bool skipNextAction)
    {
        SelectorPhase = phase;
        _phaseStartActionCount = ActionCount + (skipNextAction ? 1 : 0);
    }

    // ───────────────────────── 위치 / 이동 ─────────────────────────

    /// <summary>플레이어가 같은 방에 있는지</summary>
    public bool IsPlayerInRoom => Player != null && Room != null && Player.CurrentRoom == Room.Grid;

    /// <summary>플레이어까지 칸 거리 (상하좌우 걸음 수 기준, 같은 방이 아니면 -1)</summary>
    public int DistanceToPlayer()
    {
        if (!IsPlayerInRoom) return -1;
        Vector2Int d = Player.Cell - Cell;
        return Mathf.Abs(d.x) + Mathf.Abs(d.y);
    }

    /// <summary>이 칸으로 들어갈 수 있는지 (바닥 안, 막히지 않음, 플레이어 칸 아님)</summary>
    public bool CanEnter(Vector2Int cell, bool forced = false)
    {
        if (Room == null || (!forced && Manager != null && Manager.CardState.IsRooted(this))) return false;
        RoomGrid grid = Room.Grid;
        if (!grid.InBounds(cell) || !grid.IsWalkable(cell)) return false;
        if (IsPlayerInRoom && Player.Cell == cell) return false;
        if (Manager != null && Manager.CardState.Occupies(cell)) return false;
        return true;
    }

    /// <summary>
    /// 옆 칸으로 한 칸 이동 (격자 정보도 같이 갱신: 막힌 칸, 방 생성 목록 좌표).
    /// 들어갈 수 없는 칸이면 아무것도 하지 않는다.
    /// </summary>
    public IEnumerator StepTo(Vector2Int cell)
    {
        if (!CanEnter(cell)) yield break;

        Vector3 from = transform.position;
        Vector3 to = MoveToCell(cell);
        if (!IsAlive) yield break;

        float t = 0f;
        while (t < _moveTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / _moveTime));
            yield return null;
        }
        transform.position = to;
    }

    public bool TryPush(Vector2Int direction)
    {
        Vector2Int cell = Cell + direction;
        if (!CanEnter(cell, forced: true)) return false;
        Vector3 position = MoveToCell(cell);
        if (IsAlive) transform.position = position;
        return true;
    }

    private Vector3 MoveToCell(Vector2Int cell)
    {
        RoomGrid grid = Room.Grid;
        SpawnedEntity entity = FindSpawnedEntity();
        bool blocks = entity == null || entity.Entry == null || entity.Entry.blocksMovement;

        if (blocks)
        {
            grid.SetBlocked(Cell, false);
            grid.SetBlocked(cell, true);
        }

        Vector2Int dir = cell - Cell;
        if (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) == 1) Face(dir);

        Cell = cell;
        if (entity != null) entity.Cell = cell;
        Manager?.CardState.TriggerTrap(this);
        Vector3 world = grid.GridToWorld(cell);
        return new Vector3(world.x, world.y, transform.position.z);
    }

    /// <summary>연출 없이 바로 그 칸으로 옮긴다 (보스 페이즈 전환 등). 막힌 칸 정보도 갱신</summary>
    public void PlaceAt(Vector2Int cell)
    {
        if (Room == null) return;
        SetCellImmediate(cell, true);
    }

    private void SetCellImmediate(Vector2Int cell, bool moveTransform)
    {
        RoomGrid grid = Room.Grid;
        SpawnedEntity entity = FindSpawnedEntity();
        bool blocks = entity == null || entity.Entry == null || entity.Entry.blocksMovement;

        if (blocks)
        {
            grid.SetBlocked(Cell, false);
            grid.SetBlocked(cell, true);
        }

        Cell = cell;
        if (entity != null) entity.Cell = cell;

        if (moveTransform)
        {
            Vector3 world = grid.GridToWorld(cell);
            transform.position = new Vector3(world.x, world.y, transform.position.z);
        }
    }

    /// <summary>A* 길찾기로 플레이어에게 다가간다 (stopDistance 이하가 되면 멈춤)</summary>
    public IEnumerator MoveTowardPlayer(int steps, int stopDistance = 1)
    {
        for (int i = 0; i < steps; i++)
        {
            if (!IsAlive || !IsPlayerInRoom) yield break;
            if (DistanceToPlayer() <= stopDistance) break;

            Vector2Int next;
            if (!TryGetStepToward(out next)) break;

            yield return StartCoroutine(StepTo(next));
        }

        if (IsAlive && IsPlayerInRoom) Face(AttackArea.DirectionTowards(Cell, Player.Cell));
    }

    /// <summary>플레이어와 멀어지는 쪽으로 이동</summary>
    public IEnumerator MoveAwayFromPlayer(int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            if (!IsAlive || !IsPlayerInRoom) yield break;

            Vector2Int best = Cell;
            int bestDistance = DistanceToPlayer();
            foreach (Vector2Int dir in EnemyPathfinding.FourDirections)
            {
                Vector2Int next = Cell + dir;
                if (!CanEnter(next)) continue;
                int d = EnemyPathfinding.Manhattan(next, Player.Cell);
                if (d > bestDistance)
                {
                    best = next;
                    bestDistance = d;
                }
            }

            if (best == Cell) break; // 더 물러설 곳이 없다
            yield return StartCoroutine(StepTo(best));
        }

        if (IsAlive && IsPlayerInRoom) Face(AttackArea.DirectionTowards(Cell, Player.Cell));
    }

    /// <summary>플레이어 쪽으로 갈 다음 한 칸. A* 경로가 없으면(다른 몬스터에 막힘 등) 가까워지는 옆 칸</summary>
    private bool TryGetStepToward(out Vector2Int next)
    {
        next = Cell;
        RoomGrid grid = Room.Grid;
        Vector2Int player = Player.Cell;

        // A* (GridPathfinder) 로 플레이어까지의 최단 경로를 구하고, 그 첫 칸으로 한 칸 이동한다
        if (GridPathfinder.TryFindPath(grid, Cell, player, out List<Vector2Int> route) &&
            route.Count > 0 && route[0] != player)
        {
            next = route[0];
            return CanEnter(next);
        }

        // 길이 막혔으면 가까워지는 옆 칸으로라도
        int bestDistance = DistanceToPlayer();
        bool found = false;
        foreach (Vector2Int dir in EnemyPathfinding.FourDirections)
        {
            Vector2Int c = Cell + dir;
            if (!CanEnter(c)) continue;
            int d = EnemyPathfinding.Manhattan(c, player);
            if (d < bestDistance)
            {
                bestDistance = d;
                next = c;
                found = true;
            }
        }
        return found;
    }

    private SpawnedEntity FindSpawnedEntity()
    {
        if (Room == null) return null;

        IReadOnlyList<SpawnedEntity> spawned = Room.Spawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            SpawnedEntity s = spawned[i];
            if (s == null || s.Object == null) continue;
            if (s.Object == gameObject || transform.IsChildOf(s.Object.transform)) return s;
        }
        return null;
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

        if (_isBoss) GameManager.Instance.NotifyBossDefeated();
    }
}
