using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어 체력 데이터와 피해/회복/사망 처리.
/// - 모든 피해는 TakeDamage(DamageInfo) 한 곳으로 들어온다 (적 공격, 함정, 상태 이상 등).
/// - 피해는 IDamageAbsorber(방어막 등)가 먼저 흡수하고, 남은 피해만 체력에 적용된다.
/// - 체력이 0이 되면 한 번만 사망 처리하고 Died 이벤트를 보낸다.
/// UI 표시는 PlayerHealthUI, 행동 차단은 BattleTurnManager 가 담당한다.
/// GridPlayer 와 같은 오브젝트에 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [Min(1)][SerializeField] private int _maxHealth = 20;
    [Tooltip("게임 시작 시 체력. 최대 체력보다 크면 최대 체력으로 맞춘다")]
    [Min(1)][SerializeField] private int _startHealth = 20;

    [Header("게임 오버 연결")]
    [Tooltip("사망했을 때 실행할 것 (게임 오버 화면 켜기 등). 코드에서는 Died 이벤트를 사용해도 된다")]
    [SerializeField] private UnityEvent _onDied = new UnityEvent();

    [Header("디버그")]
    [SerializeField] private bool _logChanges = true;

    private readonly List<IDamageAbsorber> _absorbers = new List<IDamageAbsorber>();
    private bool _initialized;

    public BattleCardState CardState { get; set; }

    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>현재 체력 또는 최대 체력이 바뀌었을 때 (현재, 최대)</summary>
    public event Action<int, int> HealthChanged;

    /// <summary>피해를 받았을 때 (흡수만 되고 체력이 그대로여도 발생)</summary>
    public event Action<DamageInfo, DamageResult> Damaged;

    /// <summary>회복했을 때 (실제 회복량)</summary>
    public event Action<int> Healed;

    /// <summary>사망했을 때 (한 번만 발생)</summary>
    public event Action Died;

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        _startHealth = Mathf.Clamp(_startHealth, 1, _maxHealth);
    }

    private void Awake()
    {
        Initialize();

        // 같은 오브젝트에 붙은 방어막 등 자동 등록
        foreach (IDamageAbsorber absorber in GetComponents<IDamageAbsorber>())
            RegisterAbsorber(absorber);
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        MaxHealth = Mathf.Max(1, _maxHealth);
        CurrentHealth = Mathf.Clamp(_startHealth, 1, MaxHealth);
        IsDead = false;
    }

    // ───────────────────────── 피해 ─────────────────────────

    /// <summary>
    /// 피해 처리 공통 함수. 방어막 흡수 → 체력 감소 → 0 이하면 사망 순서로 처리한다.
    /// 이미 사망했거나 피해량이 0 이하면 아무것도 하지 않는다.
    /// </summary>
    public DamageResult TakeDamage(DamageInfo info)
    {
        Initialize();
        if (IsDead || info.Amount <= 0) return DamageResult.None;

        int remaining = CardState?.ReduceDamage(info.Amount) ?? info.Amount;
        int absorbed = 0;

        // 1) 방어막 등이 먼저 흡수
        for (int i = 0; i < _absorbers.Count && remaining > 0; i++)
        {
            IDamageAbsorber absorber = _absorbers[i];
            if (absorber == null || (absorber is UnityEngine.Object obj && obj == null)) continue;

            int taken = Mathf.Clamp(absorber.Absorb(remaining, info), 0, remaining);
            remaining -= taken;
            absorbed += taken;
        }

        // 2) 남은 피해만 체력에 적용
        int before = CurrentHealth;
        if (remaining > 0) SetCurrentHealth(CurrentHealth - remaining);
        int lost = before - CurrentHealth;

        bool killed = CurrentHealth <= 0;
        var result = new DamageResult(info.Amount, absorbed, lost, killed);

        if (_logChanges)
        {
            string from = info.Source != null ? info.Source.name : info.Type.ToString();
            Debug.Log($"[PlayerHealth] {from} 에게 피해 {info.Amount} (흡수 {absorbed}, 체력 -{lost}) → {CurrentHealth} / {MaxHealth}", this);
        }

        Damaged?.Invoke(info, result);

        // 3) 0 이하면 즉시 사망
        if (killed) Die();

        return result;
    }

    // ───────────────────────── 회복 / 최대 체력 ─────────────────────────

    /// <summary>체력 회복. 최대 체력을 넘지 않는다. 실제 회복량을 반환 (사망 상태면 0)</summary>
    public bool TrySpendHealth(int amount)
    {
        if (IsDead || amount <= 0 || CurrentHealth <= amount) return false;
        SetCurrentHealth(CurrentHealth - amount);
        return true;
    }

    public int Heal(int amount)
    {
        Initialize();
        if (IsDead || amount <= 0) return 0;

        int before = CurrentHealth;
        SetCurrentHealth(CurrentHealth + amount);
        int healed = CurrentHealth - before;

        if (healed > 0)
        {
            if (_logChanges) Debug.Log($"[PlayerHealth] 회복 +{healed} → {CurrentHealth} / {MaxHealth}", this);
            Healed?.Invoke(healed);
        }
        return healed;
    }

    /// <summary>
    /// 최대 체력 변경. 현재 체력이 새 최대 체력보다 크면 깎인다.
    /// fillToMax 가 true 면 현재 체력을 최대 체력으로 채운다.
    /// </summary>
    public void SetMaxHealth(int value, bool fillToMax = false)
    {
        Initialize();
        MaxHealth = Mathf.Max(1, value);

        int target = fillToMax && !IsDead ? MaxHealth : CurrentHealth;
        CurrentHealth = Mathf.Clamp(target, 0, MaxHealth);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>최대 체력 증가 (체력 증가 효과용). healSameAmount 면 늘어난 만큼 현재 체력도 회복</summary>
    public void IncreaseMaxHealth(int amount, bool healSameAmount = true)
    {
        if (amount <= 0) return;

        SetMaxHealth(MaxHealth + amount);
        if (healSameAmount) Heal(amount);
    }

    /// <summary>
    /// 체력을 직접 지정한다 (GameManager 가 씬이 바뀔 때 이전 체력을 옮겨올 때 사용).
    /// 현재 체력은 1 ~ 최대 체력으로 맞추고, 사망 상태는 해제된다.
    /// </summary>
    public void SetHealth(int current, int max)
    {
        Initialize();
        MaxHealth = Mathf.Max(1, max);
        CurrentHealth = Mathf.Clamp(current, 1, MaxHealth);
        IsDead = false;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>시작 상태로 되돌린다 (다시 시작할 때 사용). 사망 상태도 해제된다</summary>
    public void ResetHealth()
    {
        MaxHealth = Mathf.Max(1, _maxHealth);
        CurrentHealth = Mathf.Clamp(_startHealth, 1, MaxHealth);
        IsDead = false;
        _initialized = true;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    // ───────────────────────── 방어막 등 흡수 ─────────────────────────

    public void RegisterAbsorber(IDamageAbsorber absorber)
    {
        if (absorber == null || _absorbers.Contains(absorber)) return;

        _absorbers.Add(absorber);
        _absorbers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public void UnregisterAbsorber(IDamageAbsorber absorber)
    {
        _absorbers.Remove(absorber);
    }

    // ───────────────────────── 내부 ─────────────────────────

    private void SetCurrentHealth(int value)
    {
        int clamped = Mathf.Clamp(value, 0, MaxHealth);
        if (clamped == CurrentHealth) return;

        CurrentHealth = clamped;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private void Die()
    {
        if (IsDead) return; // 중복 사망 처리 방지
        IsDead = true;

        if (_logChanges) Debug.Log("[PlayerHealth] 플레이어 사망 → 게임 오버", this);

        Died?.Invoke();
        _onDied?.Invoke();
    }
}
