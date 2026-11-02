using System.Collections.Generic;
using UnityEngine;

/// <summary>카드를 낼 때 효과에 넘겨주는 정보 (누가, 어느 방에서, 어떤 카드를 냈는지)</summary>
public class CardContext
{
    public CardContext(CardData card, BattleTurnManager manager, GridPlayer player, DungeonRoom room)
    {
        Card = card;
        Manager = manager;
        Player = player;
        Room = room;
    }

    public CardData Card { get; }
    public BattleTurnManager Manager { get; }
    public GridPlayer Player { get; }
    public DungeonRoom Room { get; }

    /// <summary>이 칸에 있는, 피해를 받을 수 있는 살아 있는 대상(적 등)을 result 에 추가한다</summary>
    public void CollectDamageablesAt(Vector2Int cell, List<IDamageable> result)
    {
        if (Room == null) return;

        IReadOnlyList<SpawnedEntity> spawned = Room.Spawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            SpawnedEntity s = spawned[i];
            if (s == null || s.Object == null || !s.Object.activeInHierarchy) continue;

            // 이동하는 적이 생겨도 맞도록 EnemyController 의 현재 칸을 우선 사용
            Vector2Int c = s.Cell;
            EnemyController enemy = s.Object.GetComponentInChildren<EnemyController>();
            if (enemy != null) c = enemy.Cell;
            if (c != cell) continue;

            IDamageable target = s.Object.GetComponentInChildren<IDamageable>();
            if (target == null || target.IsDead) continue;
            if (!result.Contains(target)) result.Add(target);
        }
    }
}

/// <summary>
/// 카드 효과 하나 (공격, 방어, 드로우 등). 에셋으로 만들어 CardData 의 Effects 목록에 넣는다.
/// 에셋은 여러 카드가 공유하므로 실행 중 상태를 필드에 저장하지 않는다.
/// 새 효과는 이 클래스를 상속해 Apply 만 구현하면 된다.
/// </summary>
public abstract class CardEffect : ScriptableObject
{
    /// <summary>카드가 사용됐을 때 호출된다</summary>
    public abstract void Apply(CardContext context);
}
