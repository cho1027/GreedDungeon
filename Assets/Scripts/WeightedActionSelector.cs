using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가중치 무작위 선택. 같은 행동이 너무 연속되지 않도록 제한할 수 있다.
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Weighted Action Selector
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Weighted Action Selector", fileName = "WeightedActionSelector")]
public class WeightedActionSelector : EnemyActionSelector
{
    [Serializable]
    public class Entry
    {
        public EnemyAction action;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();

    [Tooltip("같은 행동을 연속으로 고를 수 있는 최대 횟수 (0 = 제한 없음)")]
    [Min(0)][SerializeField] private int _maxConsecutive = 2;

    public override EnemyAction Choose(EnemyController enemy)
    {
        // 연속 제한을 지키며 고르고, 고를 게 없으면 제한 없이 다시 고른다
        return Pick(enemy, true) ?? Pick(enemy, false);
    }

    private EnemyAction Pick(EnemyController enemy, bool respectLimit)
    {
        float total = 0f;
        foreach (Entry e in _entries)
        {
            if (IsCandidate(e, enemy, respectLimit)) total += e.weight;
        }
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        EnemyAction last = null;
        foreach (Entry e in _entries)
        {
            if (!IsCandidate(e, enemy, respectLimit)) continue;

            last = e.action;
            roll -= e.weight;
            if (roll <= 0f) return e.action;
        }
        return last; // 부동소수점 오차 대비
    }

    private bool IsCandidate(Entry e, EnemyController enemy, bool respectLimit)
    {
        if (e == null || e.action == null || e.weight <= 0f) return false;

        if (respectLimit && _maxConsecutive > 0 &&
            enemy.LastAction == e.action && enemy.ConsecutiveCount >= _maxConsecutive)
            return false;

        return true;
    }
}
