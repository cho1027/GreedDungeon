using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>상태 이상 종류</summary>
public enum StatusType
{
    Decay = 0, // 부패: 턴 종료 시 Value 만큼 피해 (방어도 무시)
    Root,      // 속박: 이동 불가 (방향 전환, 카드는 가능)
    Rage       // 광폭화: 공격력 +Value (적 전용)
}

/// <summary>같은 상태 이상이 이미 걸려 있을 때 처리</summary>
public enum StatusStackRule
{
    Refresh = 0,     // 남은 턴/수치 중 큰 값으로 갱신 (중복 적용 X)
    AddTurns,        // 남은 턴에 더한다
    IgnoreIfPresent  // 이미 있으면 무시
}

[Serializable]
public class StatusInstance
{
    public StatusType Type;
    public int Turns;
    public int Value;
}

/// <summary>
/// 상태 이상 보관 및 턴 처리. 플레이어와 적 모두에게 붙는다 (없으면 BattleTurnManager / EnemyController 가 자동으로 붙인다).
///  - Apply(종류, 턴, 수치) 로 건다
///  - TickTurnEnd 에서 부패 피해를 주고 남은 턴을 1 줄인다
///    플레이어: 플레이어 턴 종료 시 / 적: 자기 행동이 끝날 때
/// </summary>
[DisallowMultipleComponent]
public class StatusEffectHolder : MonoBehaviour
{
    [SerializeField] private bool _log = true;

    private readonly List<StatusInstance> _statuses = new List<StatusInstance>();

    public IReadOnlyList<StatusInstance> Statuses => _statuses;

    /// <summary>상태 이상이 추가/변경/제거됐을 때</summary>
    public event Action Changed;

    public static StatusEffectHolder GetOrAdd(GameObject go)
    {
        if (go == null) return null;
        StatusEffectHolder holder = go.GetComponent<StatusEffectHolder>();
        return holder != null ? holder : go.AddComponent<StatusEffectHolder>();
    }

    public static string NameOf(StatusType type)
    {
        switch (type)
        {
            case StatusType.Decay: return "부패";
            case StatusType.Root: return "속박";
            case StatusType.Rage: return "광폭화";
            default: return type.ToString();
        }
    }

    // ───────────────────────── 조회 ─────────────────────────

    public bool Has(StatusType type) => Find(type) != null;

    public int GetTurns(StatusType type)
    {
        StatusInstance s = Find(type);
        return s != null ? s.Turns : 0;
    }

    public int GetValue(StatusType type)
    {
        StatusInstance s = Find(type);
        return s != null ? s.Value : 0;
    }

    private StatusInstance Find(StatusType type)
    {
        for (int i = 0; i < _statuses.Count; i++)
            if (_statuses[i].Type == type) return _statuses[i];
        return null;
    }

    // ───────────────────────── 적용 / 제거 ─────────────────────────

    public void Apply(StatusType type, int turns, int value = 0, StatusStackRule rule = StatusStackRule.Refresh)
    {
        if (turns <= 0) return;

        StatusInstance existing = Find(type);
        if (existing == null)
        {
            _statuses.Add(new StatusInstance { Type = type, Turns = turns, Value = value });
        }
        else
        {
            switch (rule)
            {
                case StatusStackRule.IgnoreIfPresent:
                    return;
                case StatusStackRule.AddTurns:
                    existing.Turns += turns;
                    existing.Value = Mathf.Max(existing.Value, value);
                    break;
                default:
                    existing.Turns = Mathf.Max(existing.Turns, turns);
                    existing.Value = Mathf.Max(existing.Value, value);
                    break;
            }
        }

        if (_log) Debug.Log($"[Status] {name} {NameOf(type)} {turns}턴 (수치 {value})", this);
        Changed?.Invoke();
    }

    public void Remove(StatusType type)
    {
        if (_statuses.RemoveAll(s => s.Type == type) > 0) Changed?.Invoke();
    }

    public void ClearAll()
    {
        if (_statuses.Count == 0) return;
        _statuses.Clear();
        Changed?.Invoke();
    }

    // ───────────────────────── 턴 처리 ─────────────────────────

    /// <summary>턴 종료: 부패 피해 → 모든 상태 이상 남은 턴 -1 → 끝난 것 제거</summary>
    public void TickTurnEnd(IDamageable target)
    {
        if (_statuses.Count == 0) return;

        StatusInstance decay = Find(StatusType.Decay);
        if (decay != null && decay.Value > 0 && target != null && !target.IsDead)
        {
            if (_log) Debug.Log($"[Status] {name} 부패 피해 {decay.Value}", this);
            target.TakeDamage(new DamageInfo(decay.Value, DamageType.Status, null));
        }

        for (int i = _statuses.Count - 1; i >= 0; i--)
        {
            _statuses[i].Turns--;
            if (_statuses[i].Turns <= 0) _statuses.RemoveAt(i);
        }

        Changed?.Invoke();
    }

    /// <summary>"부패 2턴 · 속박 1턴" 같은 표시용 문자열 (없으면 빈 문자열)</summary>
    public string Describe(string separator = " · ")
    {
        if (_statuses.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (StatusInstance s in _statuses)
        {
            if (sb.Length > 0) sb.Append(separator);
            sb.Append(NameOf(s.Type));
            if (s.Type == StatusType.Rage) sb.Append($" +{s.Value}");
            sb.Append($" {s.Turns}턴");
        }
        return sb.ToString();
    }
}
