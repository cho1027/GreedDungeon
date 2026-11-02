using System.Collections;
using UnityEngine;

/// <summary>
/// 범위 공격 행동. 모양/피해량/사거리를 바꿔 에셋을 여러 개 만들면 된다.
///  - 패턴 A 직선: Shape = Line, Damage = 5, Range = 3
///  - 패턴 B 가로 범위: Shape = Horizontal, Damage = 7, Range = 3
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Attack Action
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Attack Action", fileName = "AttackAction")]
public class AttackAction : EnemyAction
{
    [Header("공격")]
    [SerializeField] private AttackShape _shape = AttackShape.Line;
    [Min(0)][SerializeField] private int _damage = 5;
    [Min(1)][SerializeField] private int _range = 3;
    [Tooltip("장애물이나 다른 몬스터 칸에서 그 줄의 공격이 막힌다")]
    [SerializeField] private bool _stopAtObstacles = true;

    [Header("방향")]
    [Tooltip("행동이 확정될 때 플레이어 쪽(가로/세로 중 차이가 큰 축)으로 돌아선다. 끄면 지금 바라보는 방향 그대로")]
    [SerializeField] private bool _faceTarget = true;

    public AttackShape Shape => _shape;
    public int Damage => _damage;
    public int Range => _range;

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        GridPlayer player = enemy.Player;
        if (_faceTarget && player != null && player.CurrentRoom == enemy.Room?.Grid)
            enemy.Face(AttackArea.DirectionTowards(enemy.Cell, player.Cell));

        intent.Facing = enemy.Facing;
        intent.Value = _damage;

        if (enemy.Room != null)
            AttackArea.Calculate(enemy.Room.Grid, _shape, enemy.Cell, intent.Facing, _range, _stopAtObstacles, intent.Cells);
    }

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f)
            yield return new WaitForSeconds(enemy.ActionDelay);

        GridPlayer player = enemy.Player;
        if (player == null) yield break;

        // 예고 때 계산해서 바닥에 표시한 칸 목록 그대로 판정한다
        bool hit = player.CurrentRoom == enemy.Room?.Grid && intent.Cells.Contains(player.Cell);
        Debug.Log($"[Enemy] {enemy.name} {DisplayName} ({_shape}, {intent.Cells.Count}칸) → {(hit ? "적중" : "빗나감")}", enemy);

        if (!hit) yield break;

        PlayerHealth health = enemy.Manager != null ? enemy.Manager.PlayerHealth : player.GetComponent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(DamageInfo.Attack(intent.Value, enemy.gameObject));
    }
}
