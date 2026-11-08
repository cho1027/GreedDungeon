using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 직선 ─────────────────────────

/// <summary>
/// 직선 공격 (플레이어 쪽으로 돈 뒤 정면 min~max 칸).
///  - 망자의 병사 공격: 1~2칸, 5 / 역병 구울 할퀴기: 1칸, 6
///  - 부패한 사수 일반 사격: 3~5칸, 5 / 관통 사격: 1~5칸, 8, 장애물 무시
///  - 사령술사 생명력 흡수: 1~3칸, 5, 흡수 / 시체왕 2페이즈 육체 돌진: 1~3칸, 12, 공격 후 2칸 이동
///  - 시체왕 1페이즈 육체 강타: 플레이어와 가장 가까운 신체 타일에서 1~3칸, 10
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Line Attack", fileName = "LineAttack")]
public class Ch1LineAttack : Ch1AttackAction
{
    [Header("직선 범위")]
    [Min(1)][SerializeField] private int _minRange = 1;
    [Min(1)][SerializeField] private int _maxRange = 2;
    [Tooltip("장애물/다른 몬스터에서 막힌다. 끄면 관통 (경로의 모든 칸 공격)")]
    [SerializeField] private bool _stopAtObstacles = true;
    [Tooltip("다칸 몬스터면 플레이어와 가장 가까운 신체 타일에서 쏜다 (시체왕 육체 강타)")]
    [SerializeField] private bool _fromNearestBodyTile = false;

    public int MinRange => _minRange;
    public int MaxRange => Mathf.Max(_minRange, _maxRange);

    protected override void BuildCells(EnemyController enemy, EnemyIntent intent)
    {
        Vector2Int origin = _fromNearestBodyTile ? Ch1.NearestBodyCell(enemy) : enemy.Cell;
        Vector2Int dir = enemy.IsPlayerInRoom ? AttackArea.DirectionTowards(origin, enemy.Player.Cell) : enemy.Facing;
        enemy.Face(dir);

        Ch1.AddLine(enemy.Room.Grid, origin, dir, _minRange, MaxRange, _stopAtObstacles, intent.Cells);
    }
}
