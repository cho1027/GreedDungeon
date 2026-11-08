using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>한 판(런)이 어떻게 끝났는지</summary>
public enum RunOutcome
{
    None = 0,
    Cleared,  // 보스 처치
    GameOver  // 플레이어 사망
}

/// <summary>결과 화면에 넘겨줄 정보</summary>
[Serializable]
public struct RunResult
{
    public RunOutcome Outcome;
    public int KillCount;
    public int Gold;
    public int Stage;
    public float PlayTime; // 초

    public bool IsCleared => Outcome == RunOutcome.Cleared;
}

/// <summary>
/// 한 판 동안 유지되는 데이터(체력, 덱, 골드, 처치 수)와 런의 시작/끝을 관리한다. 씬이 바뀌어도 사라지지 않는다.
///
/// 다른 사람이 쓰는 방법 (전부 GameManager.Instance 로 접근, 씬에 없으면 자동 생성)
///  - 골드:   AddGold(n), TrySpendGold(n)                 → 전투 보상, 상점, 보물방
///  - 덱:     AddCard(card), RemoveCard(card), Deck      → 카드 보상, 카드 제거
///  - 체력:   Heal(n), HealPercent(30), IncreaseMaxHealth(n) → 휴식방
///  - 결과:   RunEnded 이벤트 / LastResult               → 결과 화면 (클리어/게임오버, 처치 수)
///  - 버튼:   StartGame(), RestartRun(), GoToTitle(), QuitGame() → 시작, 다시하기, 타이틀로, 종료
///
/// 자동으로 처리하는 것
///  - 적 사망 → 처치 수 +1 (EnemyHealth.AnyDied)
///  - 플레이어 사망 → 게임 오버
///  - 보스방(RoomType.Boss) 전투 종료 또는 Is Boss 인 적 사망 → 스테이지 클리어
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    private static bool _quitting;

    /// <summary>씬에 GameManager 가 없으면 기본값으로 하나 만든다</summary>
    public static GameManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                _instance = FindAnyObjectByType<GameManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    /// <summary>Instance 를 만들지 않고 있는지만 확인할 때</summary>
    public static bool HasInstance => _instance != null;

    [Header("시작 상태 (다시하기 하면 이 값으로 돌아간다)")]
    [Tooltip("0 이면 씬의 PlayerHealth 설정값(Max/Start Health)을 그대로 쓴다")]
    [Min(0)][SerializeField] private int _startMaxHealth = 0;
    [Tooltip("0 이면 최대 체력으로 시작")]
    [Min(0)][SerializeField] private int _startHealth = 0;
    [Min(0)][SerializeField] private int _startGold = 0;
    [Tooltip("시작 덱. 비워두면 씬의 DeckManager 시작 덱을 그대로 가져온다")]
    [SerializeField] private List<DeckEntry> _startingDeck = new List<DeckEntry>();

    [Header("씬 이름 (Build Settings 에 등록된 이름)")]
    [Tooltip("다시하기 때 불러올 게임 씬. 비워두면 지금 씬을 다시 불러온다")]
    [SerializeField] private string _gameplayScene = "Main";
    [Tooltip("타이틀 씬. 비워두면 GoToTitle 은 게임 씬을 다시 불러온다")]
    [SerializeField] private string _titleScene = "";
    [Tooltip("결과 씬. 비워두면 씬을 바꾸지 않고 RunEnded 이벤트만 보낸다 (결과 패널을 같은 씬에 둘 때)")]
    [SerializeField] private string _resultScene = "";

    [Header("런 종료")]
    [Tooltip("보스 처치/사망 후 결과 화면을 띄우기까지 기다리는 시간 (초, 일시정지 영향 없음)")]
    [Min(0f)][SerializeField] private float _resultDelay = 1.0f;
    [SerializeField] private UnityEvent _onStageCleared = new UnityEvent();
    [SerializeField] private UnityEvent _onGameOver = new UnityEvent();

    [Header("디버그")]
    [SerializeField] private bool _log = true;

    private readonly List<CardData> _deck = new List<CardData>();
    private bool _healthInitialized;
    private float _runStartTime;
    private Coroutine _endRoutine;

    // 지금 씬의 연결 대상
    private PlayerHealth _playerHealth;
    private BattleTurnManager _battle;
    private GridPlayer _player;

    // ───────────────────────── 상태 ─────────────────────────

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public int Gold { get; private set; }
    public int KillCount { get; private set; }
    public int Stage { get; private set; } = 1;
    public IReadOnlyList<CardData> Deck => _deck;

    public bool IsRunOver { get; private set; }
    public RunResult LastResult { get; private set; }
    public float PlayTime => Time.time - _runStartTime;

    // ───────────────────────── 이벤트 ─────────────────────────

    public event Action<int, int> HealthChanged; // 현재, 최대
    public event Action<int> GoldChanged;
    public event Action<int> KillCountChanged;
    public event Action DeckChanged;

    /// <summary>새 판 시작 (게임 시작, 다시하기)</summary>
    public event Action RunStarted;

    /// <summary>
    /// 판이 끝났을 때 (클리어/게임오버). 결과 화면은 여기에 연결한다.
    /// Result Delay 만큼 기다린 뒤 호출된다.
    /// </summary>
    public event Action<RunResult> RunEnded;

    // ───────────────────────── 생명주기 ─────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // 이미 이전 씬에서 넘어온 GameManager 가 있다 → 새로 생긴 것은 버린다
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        StartNewRun();
    }

    private void OnEnable()
    {
        if (_instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnemyHealth.AnyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnemyHealth.AnyDied -= OnEnemyDied;
        UnbindScene();
    }

    private void Start()
    {
        // 처음 씬은 sceneLoaded 보다 늦게 만들어질 수 있어 한 번 더 연결
        BindScene();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindScene();
    }

    // ───────────────────────── 런 시작 / 초기화 ─────────────────────────

    /// <summary>모든 값을 시작 상태로 되돌린다 (씬은 다시 불러오지 않는다)</summary>
    public void StartNewRun()
    {
        if (_endRoutine != null)
        {
            StopCoroutine(_endRoutine);
            _endRoutine = null;
        }

        IsRunOver = false;
        LastResult = default;
        Stage = 1;
        KillCount = 0;
        Gold = _startGold;
        _runStartTime = Time.time;

        if (_startMaxHealth > 0)
        {
            MaxHealth = _startMaxHealth;
            CurrentHealth = _startHealth > 0 ? Mathf.Min(_startHealth, MaxHealth) : MaxHealth;
            _healthInitialized = true;
        }
        else
        {
            // 씬의 PlayerHealth 값을 처음 연결할 때 가져온다
            _healthInitialized = false;
        }

        _deck.Clear();
        AddEntries(_startingDeck);

        if (_log) Debug.Log("[GameManager] 새 판 시작", this);

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        GoldChanged?.Invoke(Gold);
        KillCountChanged?.Invoke(KillCount);
        DeckChanged?.Invoke();
        RunStarted?.Invoke();
    }

    /// <summary>다시하기: 값을 초기화하고 게임 씬을 처음부터 다시 불러온다</summary>
    public void RestartRun()
    {
        StartNewRun();
        Time.timeScale = 1f; // 일시정지 상태에서 눌러도 풀리도록
        LoadScene(string.IsNullOrEmpty(_gameplayScene) ? SceneManager.GetActiveScene().name : _gameplayScene);
    }

    /// <summary>타이틀로: 값을 초기화하고 타이틀 씬으로 (타이틀 씬이 없으면 게임 씬)</summary>
    public void GoToTitle()
    {
        StartNewRun();
        Time.timeScale = 1f;
        string scene = !string.IsNullOrEmpty(_titleScene) ? _titleScene : _gameplayScene;
        LoadScene(string.IsNullOrEmpty(scene) ? SceneManager.GetActiveScene().name : scene);
    }

    /// <summary>타이틀의 시작 버튼: 새 판으로 게임 씬 불러오기</summary>
    public void StartGame()
    {
        RestartRun();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        UnbindScene();
        SceneManager.LoadScene(sceneName);
    }

    // ───────────────────────── 씬 연결 ─────────────────────────

    /// <summary>지금 씬의 PlayerHealth / BattleTurnManager 를 찾아 연결한다 (여러 번 불러도 안전)</summary>
    public void BindScene()
    {
        PlayerHealth health = FindAnyObjectByType<PlayerHealth>();
        BattleTurnManager battle = FindAnyObjectByType<BattleTurnManager>();
        GridPlayer player = FindAnyObjectByType<GridPlayer>();

        if (health == _playerHealth && battle == _battle && player == _player) return;

        UnbindScene();
        _playerHealth = health;
        _battle = battle;
        _player = player;

        if (_playerHealth != null)
        {
            if (_healthInitialized)
            {
                // 이전 씬(또는 시작 설정)의 체력을 이 씬의 플레이어에게 적용
                _playerHealth.SetHealth(CurrentHealth, MaxHealth);
            }
            else
            {
                // 처음: 씬에 설정된 체력을 기준으로 삼는다
                MaxHealth = _playerHealth.MaxHealth;
                CurrentHealth = _playerHealth.CurrentHealth;
                _healthInitialized = true;
                HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }

            _playerHealth.HealthChanged += OnPlayerHealthChanged;
            _playerHealth.Died += OnPlayerDied;
        }

        if (_battle != null)
            _battle.CombatEnded += OnCombatEnded;
    }

    private void UnbindScene()
    {
        if (_playerHealth != null)
        {
            _playerHealth.HealthChanged -= OnPlayerHealthChanged;
            _playerHealth.Died -= OnPlayerDied;
        }

        if (_battle != null)
            _battle.CombatEnded -= OnCombatEnded;

        _playerHealth = null;
        _battle = null;
        _player = null;
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        CurrentHealth = current;
        MaxHealth = max;
        HealthChanged?.Invoke(current, max);
    }

    private void OnPlayerDied()
    {
        CurrentHealth = 0;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        EndRun(RunOutcome.GameOver);
    }

    private void OnCombatEnded(DungeonRoom room)
    {
        if (_battle.Economy == null && room != null && room.Data != null && room.Data.RoomType == RoomType.Boss)
            NotifyBossDefeated();
    }

    private void OnEnemyDied(EnemyHealth enemy)
    {
        if (IsRunOver) return;

        KillCount++;
        KillCountChanged?.Invoke(KillCount);
    }

    // ───────────────────────── 체력 ─────────────────────────

    /// <summary>체력 회복 (휴식방 등). 실제 회복량을 반환</summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || IsRunOver) return 0;

        if (_playerHealth != null) return _playerHealth.Heal(amount); // HealthChanged 로 값이 따라온다

        int before = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        return CurrentHealth - before;
    }

    /// <summary>최대 체력의 percent% 만큼 회복 (휴식방: HealPercent(30) 등)</summary>
    public int HealPercent(float percent)
    {
        return Heal(Mathf.CeilToInt(MaxHealth * Mathf.Max(0f, percent) / 100f));
    }

    /// <summary>최대 체력 증가. healSameAmount 면 늘어난 만큼 회복도 한다</summary>
    public void IncreaseMaxHealth(int amount, bool healSameAmount = true)
    {
        if (amount <= 0) return;

        if (_playerHealth != null)
        {
            _playerHealth.IncreaseMaxHealth(amount, healSameAmount);
            return;
        }

        MaxHealth += amount;
        if (healSameAmount) CurrentHealth += amount;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    // ───────────────────────── 골드 ─────────────────────────

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        if (_log) Debug.Log($"[GameManager] 골드 +{amount} → {Gold}", this);
        GoldChanged?.Invoke(Gold);
    }

    public bool CanAfford(int price) => price <= Gold;

    /// <summary>골드를 쓴다. 부족하면 쓰지 않고 false</summary>
    public bool TrySpendGold(int amount)
    {
        if (amount < 0) return false;
        if (amount > Gold) return false;

        Gold -= amount;
        if (_log) Debug.Log($"[GameManager] 골드 -{amount} → {Gold}", this);
        GoldChanged?.Invoke(Gold);
        return true;
    }

    // ───────────────────────── 덱 ─────────────────────────

    /// <summary>덱에 카드 추가 (전투 보상, 상점, 보물방). 다음 전투부터 들어간다</summary>
    public void AddCard(CardData card)
    {
        if (card == null) return;
        _deck.Add(card);
        if (_log) Debug.Log($"[GameManager] 덱에 '{card.CardName}' 추가 ({_deck.Count}장)", this);
        DeckChanged?.Invoke();
    }

    /// <summary>덱에서 카드 한 장 제거 (상점 카드 제거). 없으면 false</summary>
    public bool RemoveCard(CardData card)
    {
        if (card == null || !_deck.Remove(card)) return false;
        if (_log) Debug.Log($"[GameManager] 덱에서 '{card.CardName}' 제거 ({_deck.Count}장)", this);
        DeckChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 덱이 비어 있을 때만 시작 덱을 채운다.
    /// DeckManager 가 시작할 때 자기 시작 덱으로 호출한다 (GameManager 에 시작 덱을 넣지 않은 경우용).
    /// </summary>
    public void SeedDeckIfEmpty(IEnumerable<DeckEntry> entries)
    {
        if (_deck.Count > 0 || entries == null) return;
        AddEntries(entries);
        DeckChanged?.Invoke();
    }

    private void AddEntries(IEnumerable<DeckEntry> entries)
    {
        if (entries == null) return;
        foreach (DeckEntry entry in entries)
        {
            if (entry == null || entry.card == null) continue;
            for (int i = 0; i < entry.count; i++) _deck.Add(entry.card);
        }
    }

    // ───────────────────────── 런 종료 ─────────────────────────

    /// <summary>보스를 쓰러뜨렸을 때 (중복 호출 안전)</summary>
    public void NotifyBossDefeated()
    {
        EndRun(RunOutcome.Cleared);
    }

    /// <summary>판을 끝낸다. 플레이어 입력과 턴 진행을 멈추고, 잠시 뒤 결과를 알린다</summary>
    public void EndRun(RunOutcome outcome)
    {
        if (IsRunOver || outcome == RunOutcome.None) return;
        IsRunOver = true;

        LastResult = new RunResult
        {
            Outcome = outcome,
            KillCount = KillCount,
            Gold = Gold,
            Stage = Stage,
            PlayTime = PlayTime
        };

        if (_log) Debug.Log($"[GameManager] 판 종료: {outcome} (처치 {KillCount}, 골드 {Gold})", this);

        // 더 이상 조작/적 턴이 진행되지 않게
        if (_player != null) _player.InputLocked = true;
        if (_battle != null && outcome == RunOutcome.Cleared) _battle.enabled = false; // 적 턴 코루틴도 멈춘다

        if (_endRoutine != null) StopCoroutine(_endRoutine);
        _endRoutine = StartCoroutine(EndRunRoutine(outcome));
    }

    private IEnumerator EndRunRoutine(RunOutcome outcome)
    {
        if (_resultDelay > 0f) yield return new WaitForSecondsRealtime(_resultDelay);
        _endRoutine = null;

        if (outcome == RunOutcome.Cleared) _onStageCleared?.Invoke();
        else _onGameOver?.Invoke();

        RunEnded?.Invoke(LastResult);

        if (!string.IsNullOrEmpty(_resultScene))
            LoadScene(_resultScene);
    }

    // ───────────────────────── 디버그 (컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("Debug/골드 +50")]
    private void DebugGold() => AddGold(50);

    [ContextMenu("Debug/스테이지 클리어")]
    private void DebugClear() => EndRun(RunOutcome.Cleared);

    [ContextMenu("Debug/게임 오버")]
    private void DebugGameOver() => EndRun(RunOutcome.GameOver);

    [ContextMenu("Debug/다시하기")]
    private void DebugRestart() => RestartRun();
}
