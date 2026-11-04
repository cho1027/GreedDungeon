using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 체력과 피해/사망 처리. 행동 로직(EnemyController)과 분리되어 있다.
/// - 피해는 TakeDamage(DamageInfo) 로 들어오고, 같은 오브젝트의 방어막(DamageShield 등)이 먼저 흡수한다.
/// - 체력이 0이 되면 한 번만 사망 처리하고, 방에서 제거한다 (DungeonRoom.RemoveSpawned → 막힌 칸도 풀림).
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable, IRoomSpawned
{
    [Min(1)][SerializeField] private int _maxHealth = 20;

    [Header("사망")]
    [Tooltip("사망 시 방의 생성 목록에서 제거하고 막힌 칸을 푼다")]
    [SerializeField] private bool _removeFromRoomOnDeath = true;
    [Tooltip("사망 시 오브젝트 삭제. 끄면 비활성화만 한다 (사망 연출을 따로 붙일 때)")]
    [SerializeField] private bool _destroyOnDeath = true;

    [Header("디버그")]
    [SerializeField] private bool _logChanges = true;
    [Tooltip("우클릭 메뉴 'Debug/피해 주기' 에 쓰는 피해량")]
    [Min(1)][SerializeField] private int _debugDamage = 10;

    private readonly List<IDamageAbsorber> _absorbers = new List<IDamageAbsorber>();
    private DungeonRoom _room;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => _maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>체력이 바뀌었을 때 (현재, 최대)</summary>
    public event Action<int, int> HealthChanged;

    /// <summary>피해를 받았을 때 (방어막에만 막혀도 발생)</summary>
    public event Action<DamageInfo, DamageResult> Damaged;

    /// <summary>사망했을 때 (한 번만, 오브젝트가 제거되기 직전)</summary>
    public event Action<EnemyHealth> Died;

    /// <summary>어떤 적이든 사망했을 때 (처치 수 집계 등, GameManager 가 사용)</summary>
    public static event Action<EnemyHealth> AnyDied;

    private void Awake()
    {
        CurrentHealth = _maxHealth;

        foreach (IDamageAbsorber absorber in GetComponents<IDamageAbsorber>())
            _absorbers.Add(absorber);
        _absorbers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public void OnRoomSpawned(DungeonRoom room, Vector2Int cell, RoomSpawnEntry entry)
    {
        _room = room;
    }

    /// <summary>피해 처리 공통 함수. 방어막 흡수 → 체력 감소 → 0 이하면 사망</summary>
    public DamageResult TakeDamage(DamageInfo info)
    {
        if (IsDead || info.Amount <= 0) return DamageResult.None;

        int remaining = info.Amount;
        int absorbed = 0;

        for (int i = 0; i < _absorbers.Count && remaining > 0; i++)
        {
            IDamageAbsorber absorber = _absorbers[i];
            if (absorber == null || (absorber is UnityEngine.Object obj && obj == null)) continue;

            int taken = Mathf.Clamp(absorber.Absorb(remaining, info), 0, remaining);
            remaining -= taken;
            absorbed += taken;
        }

        int before = CurrentHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth - remaining, 0, _maxHealth);
        int lost = before - CurrentHealth;
        bool killed = CurrentHealth <= 0;

        if (_logChanges)
            Debug.Log($"[EnemyHealth] {name} 피해 {info.Amount} (방어막 흡수 {absorbed}, 체력 -{lost}) → {CurrentHealth} / {_maxHealth}", this);

        if (lost > 0) HealthChanged?.Invoke(CurrentHealth, _maxHealth);

        var result = new DamageResult(info.Amount, absorbed, lost, killed);
        Damaged?.Invoke(info, result);

        if (killed) Die();
        return result;
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        if (_logChanges) Debug.Log($"[EnemyHealth] {name} 사망", this);
        Died?.Invoke(this);
        AnyDied?.Invoke(this);

        if (_removeFromRoomOnDeath && _room != null)
        {
            _room.RemoveSpawned(gameObject, _destroyOnDeath);
            if (!_destroyOnDeath) gameObject.SetActive(false);
        }
        else if (_destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    [ContextMenu("Debug/피해 주기")]
    private void DebugDamage()
    {
        if (!Application.isPlaying) return;
        TakeDamage(new DamageInfo(_debugDamage, DamageType.Other));
    }
}
