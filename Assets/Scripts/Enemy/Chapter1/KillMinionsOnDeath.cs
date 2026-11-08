using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사령술사: 이 적이 죽으면 같은 방의 일반 몬스터(Rank = Normal)가 모두 죽는다.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class KillMinionsOnDeath : MonoBehaviour, IRoomSpawned
{
    [Tooltip("켜면 이 적이 소환한 몬스터만 죽인다. 끄면 방의 일반 몬스터 전부")]
    [SerializeField] private bool _onlySummoned = false;

    private EnemyHealth _health;
    private DungeonRoom _room;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (_health != null) _health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (_health != null) _health.Died -= OnDied;
    }

    public void OnRoomSpawned(DungeonRoom room, Vector2Int cell, RoomSpawnEntry entry)
    {
        _room = room;
    }

    private void OnDied(EnemyHealth health)
    {
        if (_room == null) return;

        var victims = new List<EnemyHealth>();
        EnemyController self = GetComponent<EnemyController>();
        object summoned = null;
        if (_onlySummoned && self != null) self.Memory.TryGetValue("Ch1Summoned", out summoned);

        foreach (SpawnedEntity s in _room.Spawned)
        {
            if (s == null || s.Object == null || s.Object == gameObject) continue;
            EnemyController ec = s.Object.GetComponentInChildren<EnemyController>();
            if (ec == null || ec.Rank != EnemyRank.Normal || ec.Health == null || ec.Health.IsDead) continue;
            if (_onlySummoned && !(summoned is List<GameObject> list && list.Contains(s.Object))) continue;
            victims.Add(ec.Health);
        }

        if (victims.Count > 0) Debug.Log($"[Enemy] {name} 사망 → 일반 몬스터 {victims.Count}마리 함께 사망", this);
        foreach (EnemyHealth v in victims) v.Kill();
    }
}
