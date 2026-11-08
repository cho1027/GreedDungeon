using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 소환 ─────────────────────────

/// <summary>
/// 사령술사 망자 소환: 빈 인접 칸에 일반 몬스터 1마리 (목록 중 무작위). 동시에 최대 N마리.
/// 빈 칸이 없거나 제한에 걸리면 CanUse = false (선택기가 건너뛴다).
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Summon", fileName = "Summon")]
public class Ch1Summon : EnemyAction
{
    [Tooltip("소환할 몬스터 프리팹 (무작위로 하나)")]
    [SerializeField] private List<GameObject> _prefabs = new List<GameObject>();
    [Min(1)][SerializeField] private int _maxAlive = 2;
    [Range(0.3f, 1f)][SerializeField] private float _fill = 0.9f;

    private const string Key = "Ch1Summoned";

    public override bool CanUse(EnemyController enemy)
    {
        if (_prefabs.Count == 0 || enemy.Room == null) return false;
        if (CountAlive(enemy) >= _maxAlive) return false;
        return Ch1.TryFindFreeNeighbor(enemy, out _);
    }

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Facing = enemy.Facing;
        intent.Value = 1;
        intent.ValueLabel = $"{CountAlive(enemy)}/{_maxAlive}";
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f) yield return new WaitForSeconds(enemy.ActionDelay);
        if (!CanUse(enemy)) yield break;

        Ch1.TryFindFreeNeighbor(enemy, out Vector2Int cell);
        GameObject prefab = PickPrefab();
        if (prefab == null) yield break;

        GameObject go = RoomSpawnUtil.SpawnMonster(enemy.Room, prefab, cell, _fill);
        if (go == null) yield break;

        List<GameObject> list = GetList(enemy);
        list.Add(go);

        // 전투 중에 생겼으므로 바로 첫 행동을 예고하게 한다 (이번 적 턴에는 행동하지 않는다)
        if (enemy.Manager != null)
            foreach (IEnemyCombatListener l in go.GetComponentsInChildren<IEnemyCombatListener>())
                l.OnCombatStarted(enemy.Manager);

        Debug.Log($"[Enemy] {enemy.name} {DisplayName} → {go.name} ({cell})", enemy);
    }

    private GameObject PickPrefab()
    {
        var valid = _prefabs.FindAll(p => p != null);
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private static List<GameObject> GetList(EnemyController enemy)
    {
        if (!enemy.Memory.TryGetValue(Key, out object o) || !(o is List<GameObject> list))
        {
            list = new List<GameObject>();
            enemy.Memory[Key] = list;
        }
        list.RemoveAll(g => g == null || !g.activeInHierarchy);
        return list;
    }

    public static int CountAlive(EnemyController enemy) => GetList(enemy).Count;
}
