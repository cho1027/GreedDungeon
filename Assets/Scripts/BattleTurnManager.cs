using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>현재 방의 진행 상태</summary>
public enum BattlePhase
{
    Exploration = 0, // 살아 있는 적 없음: 코스트 무한, 턴 없음
    PlayerTurn,      // 전투 중, 플레이어 행동 단계
    EnemyTurn        // 전투 중, 적 행동 단계 (플레이어 입력 불가)
}

/// <summary>
/// 코스트 / 플레이어 턴 / 적 턴 / 방 상태(전투·탐험)를 한곳에서 관리한다.
/// - RoomTransitionController 의 RoomEntered / RoomExited 로 방 상태를 판정한다.
/// - GridPlayer.CanMoveByInput / ManualMoveStarted 로 이동 코스트를 확인·차감한다.
/// - HandUI.CanUseCard / CardUseStarted 로 카드 코스트를 확인·차감한다.
/// - 살아 있는 적 = 현재 방 DungeonRoom.Spawned 중 kind 가 Monster 이고, 오브젝트가 남아 있고 활성 상태인 것
///   (IEnemyTurnActor 가 있으면 IsAlive 도 확인).
/// - PlayerHealth 가 사망하면 이동, 카드 사용, 턴 종료를 모두 막고 진행 중인 적 턴을 멈춘다.
///
/// 플레이어 방어도 / 상태 이상
/// - 플레이어에 DamageShield(방어도)와 StatusEffectHolder(상태 이상)를 자동으로 붙인다.
/// - 방어도: 적 공격 피해를 먼저 막는다. 플레이어 턴이 시작될 때 사라진다.
/// - 플레이어 턴 종료: 부패 지대 피해 → 상태 이상 처리(부패 피해, 남은 턴 감소)
/// - 적 턴 시작: 지연 공격(대지 붕괴 등) 발동
/// - 속박(Root) 상태에서는 이동할 수 없다 (방향 전환, 카드는 가능)
/// 씬에 하나만 둔다.
/// </summary>
public class BattleTurnManager : MonoBehaviour
{
    [Header("기존 시스템 연결")]
    [SerializeField] private RoomTransitionController _roomController;
    [SerializeField] private GridPlayer _player;
    [SerializeField] private HandUI _hand;
    [Tooltip("비워두면 Player 오브젝트에서 찾는다")]
    [SerializeField] private PlayerHealth _playerHealth;

    [Header("UI")]
    [Tooltip("코스트를 표시할 텍스트 (예: 3 / 3, ∞)")]
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _endTurnButton;
    [Tooltip("턴 종료 버튼을 숨길 때 끌 오브젝트. 비워두면 버튼 오브젝트 자체를 끈다")]
    [SerializeField] private GameObject _endTurnButtonRoot;

    [Header("코스트")]
    [Min(1)][SerializeField] private int _maxCost = 3;
    [Min(0)][SerializeField] private int _moveCost = 1;
    [Tooltip("탐험 상태에서 표시할 문자. 폰트에 ∞ 글리프가 없으면 다른 문자로 바꾸세요")]
    [SerializeField] private string _infiniteText = "∞";

    [Header("적 턴")]
    [Tooltip("턴 종료를 누른 뒤 적이 행동하기 시작할 때까지의 대기 시간")]
    [Min(0f)][SerializeField] private float _enemyTurnStartDelay = 0.2f;
    [Tooltip("IEnemyTurnActor 가 없는 몬스터가 '행동'하는 데 쓰는 임시 대기 시간")]
    [Min(0f)][SerializeField] private float _placeholderActionTime = 0.3f;

    [Header("전투 종료")]
    [Tooltip("전투 중 적이 모두 사라지면 DungeonRoom.SetCleared() 를 호출해 잠긴 문을 연다")]
    [SerializeField] private bool _clearRoomWhenEnemiesDefeated = true;

    private Coroutine _enemyTurnRoutine;
    private readonly List<GameObject> _enemyBuffer = new List<GameObject>();
    private readonly List<IEnemyTurnActor> _actorBuffer = new List<IEnemyTurnActor>();
    private DamageShield _playerShield;
    private StatusEffectHolder _playerStatus;
    private bool _cutscene;

    public BattlePhase Phase { get; private set; } = BattlePhase.Exploration;
    public int CurrentCost { get; private set; }
    public int CardStrength { get; private set; }
    private int _nextAttackStrength;

    public void AddNextAttackStrength(int amount) => _nextAttackStrength += amount;

    public int ConsumeNextAttackStrength()
    {
        int amount = _nextAttackStrength;
        _nextAttackStrength = 0;
        return amount;
    }
    public int CardDexterity { get; private set; }
    public void AddCardStrength(int amount) => CardStrength += amount;
    public void AddCardDexterity(int amount) => CardDexterity += amount;
    public BattleCardState CardState { get; } = new BattleCardState();

    private int _nextTurnCostPenalty;
    private int _turnCostPenalty;

    public int MaxCost => Mathf.Max(0, _maxCost - _turnCostPenalty);
    public DungeonRoom CurrentRoom { get; private set; }
    public GridPlayer Player => _player;
    public RoomEconomy Economy { get; set; }
    public PlayerHealth PlayerHealth => _playerHealth;

    /// <summary>플레이어 방어도 (방어 카드가 여기에 쌓는다)</summary>
    public DamageShield PlayerShield => _playerShield;

    /// <summary>플레이어 상태 이상 (부패, 속박 ...)</summary>
    public StatusEffectHolder PlayerStatus => _playerStatus;

    /// <summary>플레이어가 사망했는지 (사망 후에는 어떤 행동도 할 수 없다)</summary>
    public bool IsPlayerDead => _playerHealth != null && _playerHealth.IsDead;

    /// <summary>연출(보스 페이즈 전환 등) 중이면 true. 이동, 카드, 턴 종료가 막힌다</summary>
    public bool IsCutscene
    {
        get => _cutscene;
        set
        {
            _cutscene = value;
            RefreshUI();
        }
    }

    public bool IsInCombat => Phase != BattlePhase.Exploration;
    public bool IsPlayerTurn => Phase == BattlePhase.PlayerTurn;
    public bool IsEnemyTurn => Phase == BattlePhase.EnemyTurn;

    /// <summary>상태가 바뀌었을 때 (탐험 / 플레이어 턴 / 적 턴)</summary>
    public event Action<BattlePhase> PhaseChanged;

    /// <summary>코스트가 바뀌었을 때 (현재 코스트)</summary>
    public event Action<int> CostChanged;

    /// <summary>플레이어 턴 시작 (코스트 회복 직후). 카드 드로우 등을 여기에 연결하면 된다</summary>
    public event Action PlayerTurnStarted;

    /// <summary>플레이어 턴 종료 (턴 종료 버튼). 남은 손패 버리기 등을 여기에 연결하면 된다</summary>
    public event Action PlayerTurnEnded;

    /// <summary>적 턴 시작</summary>
    public event Action EnemyTurnStarted;

    /// <summary>전투 시작 (방에 살아 있는 적이 생겼을 때)</summary>
    public event Action<DungeonRoom> CombatStarted;

    /// <summary>전투 종료 (전투 중 적이 모두 사라졌을 때). 보상 지급 등을 여기에 연결하면 된다</summary>
    public event Action<DungeonRoom> CombatEnded;

    // ───────────────────────── 연결 ─────────────────────────

    private void Awake()
    {
        if (_playerHealth == null && _player != null)
            _playerHealth = _player.GetComponent<PlayerHealth>();

        if (_playerHealth != null) _playerHealth.CardState = CardState;

        // 플레이어 방어도 / 상태 이상 (씬에 따로 붙이지 않아도 되게 자동으로)
        if (_player != null)
        {
            _playerShield = _player.GetComponent<DamageShield>();
            if (_playerShield == null) _playerShield = _player.gameObject.AddComponent<DamageShield>();
            _playerStatus = StatusEffectHolder.GetOrAdd(_player.gameObject);

            if (_playerHealth != null) _playerHealth.RegisterAbsorber(_playerShield);
        }
    }

    private void OnEnable()
    {
        if (_roomController != null)
        {
            _roomController.RoomEntered += OnRoomEntered;
            _roomController.RoomExited += OnRoomExited;
        }
        else
        {
            Debug.LogWarning("BattleTurnManager: Room Controller 가 연결되지 않았습니다.", this);
        }

        if (_player != null)
        {
            _player.CanMoveByInput = CanPlayerMove;
            _player.CanTurnByInput = CanAct;
            _player.ManualMoveStarted += OnPlayerMoveStarted;
        }
        else
        {
            Debug.LogWarning("BattleTurnManager: Player 가 연결되지 않았습니다.", this);
        }

        if (_hand != null)
        {
            _hand.CanUseCard = CanPlayCard;
            _hand.CardUseStarted += OnCardUseStarted;
        }
        else
        {
            Debug.LogWarning("BattleTurnManager: Hand UI 가 연결되지 않았습니다.", this);
        }

        if (_endTurnButton != null)
            _endTurnButton.onClick.AddListener(EndPlayerTurn);

        if (_playerHealth != null)
            _playerHealth.Died += OnPlayerDied;
        else
            Debug.LogWarning("BattleTurnManager: PlayerHealth 를 찾지 못했습니다. 사망 시 행동 차단이 동작하지 않습니다.", this);

        RefreshUI();
    }

    private void OnDisable()
    {
        if (_roomController != null)
        {
            _roomController.RoomEntered -= OnRoomEntered;
            _roomController.RoomExited -= OnRoomExited;
        }

        if (_player != null)
        {
            if (_player.CanMoveByInput == (Func<bool>)CanPlayerMove) _player.CanMoveByInput = null;
            if (_player.CanTurnByInput == (Func<bool>)CanAct) _player.CanTurnByInput = null;
            _player.ManualMoveStarted -= OnPlayerMoveStarted;
        }

        if (_hand != null)
        {
            if (_hand.CanUseCard == (Func<CardData, bool>)CanPlayCard) _hand.CanUseCard = null;
            _hand.CardUseStarted -= OnCardUseStarted;
        }

        if (_endTurnButton != null)
            _endTurnButton.onClick.RemoveListener(EndPlayerTurn);

        if (_playerHealth != null)
            _playerHealth.Died -= OnPlayerDied;

        StopEnemyTurn();
    }

    private void Start()
    {
        CurrentCost = _maxCost;
        RefreshUI();
    }

    private void Update()
    {
        // 카드 효과, 직접 Destroy 등으로 적이 사라지거나 새로 생긴 경우를 매 프레임 확인
        RefreshCombatState();
    }

    // ───────────────────────── 방 입장 / 퇴장 ─────────────────────────

    private void OnRoomEntered(DungeonRoom room)
    {
        // 이전 방의 턴, 코스트를 전부 버리고 새 방 기준으로 다시 시작
        StopEnemyTurn();
        CurrentRoom = room;

        if (HasAliveEnemies())
            StartCombat();
        else
            SetExploration(false);
    }

    private void OnRoomExited(DungeonRoom room)
    {
        StopEnemyTurn();
        CardState.Reset();
        HazardZones.Find(room)?.ClearAll();
        CurrentRoom = null;
        SetExploration(false); // 방 이동 연출 동안은 ∞ 표시, 버튼 숨김
    }

    // ───────────────────────── 사망 ─────────────────────────

    private void OnPlayerDied()
    {
        // 키 입력도 잠근다 (CanAct 에서도 막지만 이중으로)
        if (_player != null) _player.InputLocked = true;

        // 적 턴 코루틴은 여기서 멈추지 않는다: 적 행동 도중(코루틴 안)에서 호출될 수 있으므로
        // EnemyTurnRoutine 이 다음 확인 지점에서 스스로 끝낸다.
        RefreshUI();
    }

    // ───────────────────────── 상태 전환 ─────────────────────────

    /// <summary>현재 방의 적 생존 여부에 맞춰 전투/탐험 상태를 갱신한다</summary>
    public void RefreshCombatState()
    {
        if (IsPlayerDead) return;
        if (CurrentRoom == null) return;
        if (_cutscene) return;
        if (_roomController != null && _roomController.IsTransitioning) return;

        // 적 턴 진행 중에는 코루틴이 끝날 때 직접 판정한다
        if (Phase == BattlePhase.EnemyTurn) return;

        bool alive = HasAliveEnemies();

        if (Phase == BattlePhase.PlayerTurn && !alive)
            SetExploration(true);
        else if (Phase == BattlePhase.Exploration && alive)
            StartCombat();
    }

    private void StartCombat()
    {
        CardStrength = 0;
        _nextAttackStrength = 0;
        CardDexterity = 0;
        CardState.Reset();
        CombatStarted?.Invoke(CurrentRoom);
        NotifyEnemiesCombatStarted();
        BeginPlayerTurn();
    }

    /// <summary>살아 있는 적들에게 전투 시작을 알려 첫 행동을 예고하게 한다</summary>
    private void NotifyEnemiesCombatStarted()
    {
        _enemyBuffer.Clear();
        CollectAliveEnemies(_enemyBuffer);
        var enemies = new List<GameObject>(_enemyBuffer);

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            foreach (IEnemyCombatListener listener in enemy.GetComponentsInChildren<IEnemyCombatListener>())
                listener.OnCombatStarted(this);
        }
    }

    private void BeginPlayerTurn()
    {
        // 지난 플레이어 턴에 얻은 방어도는 여기서 사라진다
        if (_playerShield != null) _playerShield.TickTurn();

        SetPhase(BattlePhase.PlayerTurn);
        _turnCostPenalty = _nextTurnCostPenalty;
        _nextTurnCostPenalty = 0;
        SetCost(MaxCost + (CardState.HasGenerator ? 1 : 0));
        if (_player != null)
        {
            CardState.BeginTurn(_player.Cell); // 방어막은 위의 TickTurn 에서 정리 (1턴짜리는 여기서 사라짐)
        }
        PlayerTurnStarted?.Invoke();
    }

    /// <param name="combatWon">전투 중 적을 모두 처치해서 넘어온 경우 true</param>
    private void SetExploration(bool combatWon)
    {
        CardState.Reset();
        _turnCostPenalty = 0;
        _nextTurnCostPenalty = 0;
        SetPhase(BattlePhase.Exploration);
        SetCost(_maxCost); // 내부 값만 초기화 (화면에는 ∞)

        // 전투가 끝나면 방어도, 상태 이상, 바닥 효과를 정리한다
        if (_playerShield != null) _playerShield.Clear();
        if (_playerStatus != null) _playerStatus.ClearAll();
        HazardZones.Find(CurrentRoom)?.ClearAll();

        if (combatWon && CurrentRoom != null)
        {
            if (_clearRoomWhenEnemiesDefeated) CurrentRoom.SetCleared();
            CombatEnded?.Invoke(CurrentRoom);
        }
    }

    private void SetPhase(BattlePhase phase)
    {
        bool changed = Phase != phase;
        Phase = phase;
        RefreshUI();
        if (changed) PhaseChanged?.Invoke(phase);
    }

    private void SetCost(int value)
    {
        CurrentCost = Mathf.Max(0, value);
        RefreshUI();
        CostChanged?.Invoke(CurrentCost);
    }

    // ───────────────────────── 코스트 확인 / 차감 ─────────────────────────

    /// <summary>플레이어가 지금 행동(이동, 카드)을 할 수 있는 상태인지 (코스트 제외)</summary>
    public bool CanAct()
    {
        if (Economy != null && Economy.BlocksAction) return false;
        if (IsPlayerDead) return false;
        if (_cutscene) return false;
        if (Phase == BattlePhase.EnemyTurn) return false;
        if (_roomController != null && _roomController.IsTransitioning) return false;
        return true;
    }

    /// <summary>이 비용을 지금 낼 수 있는지. 탐험 상태에서는 항상 true</summary>
    public bool CanAfford(int cost)
    {
        if (!CanAct()) return false;
        if (Phase == BattlePhase.Exploration) return true;
        return Mathf.Max(0, cost) <= CurrentCost;
    }

    /// <summary>비용을 낸다. 낼 수 없으면 차감하지 않고 false. 탐험 상태에서는 차감 없이 true</summary>
    public void ApplyManaSurge()
    {
        _nextTurnCostPenalty++;
        GainCost(2);
    }

    public void GainCost(int amount)
    {
        if (IsPlayerTurn) SetCost(CurrentCost + amount);
    }

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost)) return false;
        if (Phase == BattlePhase.PlayerTurn && cost > 0)
            SetCost(CurrentCost - cost);
        return true;
    }

    /// <summary>속박 등으로 이동이 막혔는지</summary>
    public bool IsPlayerRooted => Phase != BattlePhase.Exploration &&
                                  _playerStatus != null && _playerStatus.Has(StatusType.Root);

    private bool CanPlayerMove()
    {
        RefreshCombatState();
        if (IsPlayerRooted)
        {
            Debug.Log("[Battle] 속박 상태라 이동할 수 없습니다.");
            return false;
        }
        return CanAfford(_moveCost);
    }

    private void OnPlayerMoveStarted()
    {
        // 이동은 이미 실행됨 → 코스트만 차감 (CanPlayerMove 에서 확인했으므로 부족할 일 없음)
        TrySpend(_moveCost);
    }

    private bool CanPlayCard(CardData data)
    {
        RefreshCombatState();
        return CanAfford(GetCardCost(data));
    }

    private void OnCardUseStarted(CardData data)
    {
        TrySpend(GetCardCost(data));
    }

    private static int GetCardCost(CardData data)
    {
        return data != null ? Mathf.Max(0, data.Cost) : 0;
    }

    // ───────────────────────── 턴 종료 / 적 턴 ─────────────────────────

    /// <summary>턴 종료 버튼. 플레이어 턴일 때만 동작하고, 적 턴이 겹쳐 실행되지 않는다</summary>
    public void EndPlayerTurn()
    {
        if (IsPlayerDead) return;
        if (_cutscene) return;
        if (Phase != BattlePhase.PlayerTurn) return;
        if (_enemyTurnRoutine != null) return;
        if (_roomController != null && _roomController.IsTransitioning) return;

        // 즉시 적 턴으로 바꿔서 연타/추가 입력을 막는다
        SetPhase(BattlePhase.EnemyTurn);
        PlayerTurnEnded?.Invoke();
        CardState.EndTurn(this);

        // 플레이어 턴 종료 효과: 부패 지대 → 상태 이상 (부패 피해, 남은 턴 감소)
        ApplyPlayerTurnEndEffects();
        if (IsPlayerDead)
        {
            RefreshUI();
            return;
        }

        _enemyTurnRoutine = StartCoroutine(EnemyTurnRoutine());
    }

    private void ApplyPlayerTurnEndEffects()
    {
        HazardZones.Find(CurrentRoom)?.OnPlayerTurnEnd(_player, _playerHealth);
        if (IsPlayerDead) return;

        if (_playerStatus != null) _playerStatus.TickTurnEnd(_playerHealth);
    }

    private IEnumerator EnemyTurnRoutine()
    {
        EnemyTurnStarted?.Invoke();

        // 플레이어가 이동 중이면 도착할 때까지 기다린다
        while (_player != null && _player.IsMoving) yield return null;

        if (_enemyTurnStartDelay > 0f)
            yield return new WaitForSeconds(_enemyTurnStartDelay);

        // 지난 적 턴에 예고해 둔 지연 공격(대지 붕괴 등) 발동
        HazardZones.Find(CurrentRoom)?.OnEnemyTurnStart(_player, _playerHealth);

        // 턴 시작 시점에 살아 있는 적 목록 (행동 중 목록이 바뀌어도 안전하게)
        _enemyBuffer.Clear();
        if (!IsPlayerDead) CollectAliveEnemies(_enemyBuffer);
        var enemies = new List<GameObject>(_enemyBuffer);

        foreach (GameObject enemy in enemies)
        {
            if (!IsEnemyAlive(enemy)) continue;

            // 보스 페이즈 전환 등 연출이 끝날 때까지 기다린다
            while (_cutscene) yield return null;

            _actorBuffer.Clear();
            enemy.GetComponentsInChildren(false, _actorBuffer);

            if (_actorBuffer.Count == 0)
            {
                // 아직 행동 규칙이 없는 몬스터: 임시로 잠깐 대기
                if (_placeholderActionTime > 0f)
                    yield return new WaitForSeconds(_placeholderActionTime);
                if (IsPlayerDead) break;
                continue;
            }

            var actors = new List<IEnemyTurnActor>(_actorBuffer);
            foreach (IEnemyTurnActor actor in actors)
            {
                if (actor == null || !actor.IsAlive) continue;
                if (!(actor is MonoBehaviour mb) || mb == null) continue;

                IEnumerator action = actor.TakeTurn(this);
                if (action != null) yield return StartCoroutine(action);

                if (IsPlayerDead) break; // 사망하면 남은 적은 행동하지 않는다
            }

            if (IsPlayerDead) break;
        }

        while (_cutscene) yield return null;

        _enemyTurnRoutine = null;

        // 사망했으면 플레이어 턴으로 돌아가지 않는다 (적 턴 상태로 멈춰 입력 불가 유지)
        if (IsPlayerDead)
        {
            RefreshUI();
            yield break;
        }

        // 적 턴 중에 적이 모두 사라졌으면 탐험, 아니면 플레이어 턴으로
        if (CurrentRoom != null && !HasAliveEnemies())
            SetExploration(true);
        else
            BeginPlayerTurn();
    }

    private void StopEnemyTurn()
    {
        if (_enemyTurnRoutine != null)
        {
            StopCoroutine(_enemyTurnRoutine);
            _enemyTurnRoutine = null;
        }
    }

    // ───────────────────────── 적 판정 ─────────────────────────

    /// <summary>현재 방에 살아 있는 적이 있는지</summary>
    public bool HasAliveEnemies()
    {
        if (CurrentRoom == null) return false;

        IReadOnlyList<SpawnedEntity> spawned = CurrentRoom.Spawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            SpawnedEntity s = spawned[i];
            if (s == null || s.Entry == null || s.Entry.kind != RoomSpawnKind.Monster) continue;
            if (IsEnemyAlive(s.Object)) return true;
        }
        return false;
    }

    private void CollectAliveEnemies(List<GameObject> result)
    {
        if (CurrentRoom == null) return;

        IReadOnlyList<SpawnedEntity> spawned = CurrentRoom.Spawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            SpawnedEntity s = spawned[i];
            if (s == null || s.Entry == null || s.Entry.kind != RoomSpawnKind.Monster) continue;
            if (IsEnemyAlive(s.Object)) result.Add(s.Object);
        }
    }

    private static bool IsEnemyAlive(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy) return false;

        IEnemyTurnActor actor = enemy.GetComponentInChildren<IEnemyTurnActor>();
        return actor == null || actor.IsAlive;
    }

    // ───────────────────────── UI ─────────────────────────

    private void RefreshUI()
    {
        if (_costText != null)
        {
            _costText.text = Phase == BattlePhase.Exploration
                ? _infiniteText
                : $"{CurrentCost} / {MaxCost}";
        }

        GameObject buttonRoot = _endTurnButtonRoot != null
            ? _endTurnButtonRoot
            : (_endTurnButton != null ? _endTurnButton.gameObject : null);

        if (buttonRoot != null)
        {
            bool show = Phase != BattlePhase.Exploration;          // 탐험 상태에서는 숨김
            if (buttonRoot.activeSelf != show) buttonRoot.SetActive(show);
        }

        if (_endTurnButton != null)
            _endTurnButton.interactable = Phase == BattlePhase.PlayerTurn && !IsPlayerDead && !_cutscene; // 적 턴, 사망, 연출 중 비활성
    }
}
