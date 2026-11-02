using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 바라보는 방향으로 공격하는 카드 효과.
///  - "공격" 카드: Shape = Line, Damage = 6, Range = 1 (전방 1칸)
/// 만들기: Project 우클릭 → Create → GridDungeon → Card Effect → Attack
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Card Effect/Attack", fileName = "AttackCardEffect")]
public class AttackCardEffect : CardEffect
{
    [Min(0)][SerializeField] private int _damage = 6;
    [Tooltip("Line = 정면 직선, Horizontal = 정면 + 왼쪽 앞 + 오른쪽 앞")]
    [SerializeField] private AttackShape _shape = AttackShape.Line;
    [Min(1)][SerializeField] private int _range = 1;
    [Tooltip("켜면 장애물·몬스터 칸에서 공격이 막혀 그 뒤 칸은 맞지 않는다 (몬스터 칸 자체도 막히므로 보통은 끈다)")]
    [SerializeField] private bool _stopAtObstacles = false;

    public int Damage => _damage;
    public int Range => _range;
    public AttackShape Shape => _shape;

    public override void Apply(CardContext context)
    {
        GridPlayer player = context.Player;
        DungeonRoom room = context.Room;

        if (player == null || room == null || player.CurrentRoom != room.Grid)
        {
            Debug.Log($"[Card] {name}: 공격할 방이 없습니다.");
            return;
        }

        var cells = new List<Vector2Int>();
        AttackArea.Calculate(room.Grid, _shape, player.Cell, player.Facing, _range, _stopAtObstacles, cells);

        // 피해 도중 적이 죽으면 방 목록이 바뀌므로 대상을 먼저 모은다
        var targets = new List<IDamageable>();
        foreach (Vector2Int cell in cells)
            context.CollectDamageablesAt(cell, targets);

        if (targets.Count == 0)
        {
            Debug.Log($"[Card] {name}: 방향 {player.Facing} 에 대상 없음 (빗나감)");
            return;
        }

        foreach (IDamageable target in targets)
        {
            if (target == null || target.IsDead) continue;
            target.TakeDamage(DamageInfo.Attack(_damage, player.gameObject));
        }
    }
}
