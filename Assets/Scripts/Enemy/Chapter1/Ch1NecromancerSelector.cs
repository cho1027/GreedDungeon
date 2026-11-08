using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사령술사
///  1. 사령탄 / 생명력 흡수 / 죽음의 파동을 가방(테트리스 7-bag 방식)에서 하나씩 꺼내 쓴다
///  2. 가방을 다 쓰면 망자 소환. 첫 행동은 반드시 망자 소환
///  3. 소환할 칸이 없거나 제한(2마리)에 걸리면 소환을 건너뛴다
///  4. 플레이어가 직선 3칸 안에 있으면 생명력 흡수를 우선 사용 (가방에 있으면 가방에서 뺀다)
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Necromancer Selector", fileName = "NecromancerSelector")]
public class Ch1NecromancerSelector : EnemyActionSelector
{
    [SerializeField] private List<EnemyAction> _bag = new List<EnemyAction>();
    [SerializeField] private EnemyAction _lifeDrain;
    [Min(1)][SerializeField] private int _drainRange = 3;
    [SerializeField] private EnemyAction _summon;

    private class State
    {
        public readonly List<EnemyAction> Bag = new List<EnemyAction>();
        public bool Started;
        public bool SummonPending;
    }

    private const string Key = "Ch1NecromancerBag";

    public override EnemyAction Choose(EnemyController enemy)
    {
        State s = GetState(enemy);

        // 첫 행동 / 가방을 다 쓴 뒤 → 망자 소환 (못 하면 건너뛴다)
        if (!s.Started || s.SummonPending)
        {
            s.Started = true;
            s.SummonPending = false;
            if (_summon != null && _summon.CanUse(enemy)) return _summon;
        }

        if (s.Bag.Count == 0) Refill(s);

        EnemyAction picked;
        if (_lifeDrain != null && Ch1Select.IsAligned(enemy, _drainRange))
        {
            picked = _lifeDrain;
            s.Bag.Remove(_lifeDrain);
        }
        else
        {
            int index = Random.Range(0, s.Bag.Count);
            picked = s.Bag.Count > 0 ? s.Bag[index] : null;
            if (picked != null) s.Bag.RemoveAt(index);
        }

        if (s.Bag.Count == 0) s.SummonPending = true;
        return picked;
    }

    private void Refill(State s)
    {
        s.Bag.Clear();
        foreach (EnemyAction a in _bag) if (a != null) s.Bag.Add(a);
        if (_lifeDrain != null && !s.Bag.Contains(_lifeDrain)) s.Bag.Add(_lifeDrain);
    }

    private static State GetState(EnemyController enemy)
    {
        if (enemy.Memory.TryGetValue(Key, out object o) && o is State s) return s;
        s = new State();
        enemy.Memory[Key] = s;
        return s;
    }
}
