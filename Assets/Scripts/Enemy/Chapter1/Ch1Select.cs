using System.Collections.Generic;
using UnityEngine;

// 1챕터 몬스터 행동 알고리즘 (기획서의 "행동 알고리즘" 항목)
// 망자의 병사는 기존 SequenceActionSelector (공격 → 강공격 → 방어) 를 그대로 쓴다.

/// <summary>선택기 공통 도구</summary>
public static class Ch1Select
{
    public static EnemyAction RandomUsable(EnemyController e, IList<EnemyAction> actions, EnemyAction exclude = null)
    {
        var usable = new List<EnemyAction>();
        foreach (EnemyAction a in actions)
            if (a != null && a != exclude && a.CanUse(e)) usable.Add(a);
        return usable.Count > 0 ? usable[Random.Range(0, usable.Count)] : null;
    }

    /// <summary>같은 줄(가로/세로)에 있고 거리가 maxRange 이내인지</summary>
    public static bool IsAligned(EnemyController e, int maxRange)
    {
        if (!e.IsPlayerInRoom) return false;
        Vector2Int d = e.Player.Cell - e.Cell;
        if (d.x != 0 && d.y != 0) return false;
        int dist = Mathf.Abs(d.x) + Mathf.Abs(d.y);
        return dist >= 1 && dist <= maxRange;
    }
}
