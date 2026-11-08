using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ───────────────────────── 범위 ─────────────────────────

public enum Ch1AreaCenter
{
    Self = 0,        // 자기 칸 기준
    Player,          // 예고 시점의 플레이어 칸 기준
    NearestBodyTile  // 플레이어와 가장 가까운 신체 타일 기준
}

/// <summary>
/// 사각형/마름모 범위 공격.
///  - 역병 구울 부패 분출: 자기 주변 1, 4, 부패 지대 2턴
///  - 사령술사 사령탄: 플레이어 칸 3x3(중심 포함), 4 / 중심 칸 8
///  - 사령술사 죽음의 파동: 자기 주변 2 (5x5), 6
///  - 시체왕 시체 분쇄: 자기 3x3, 12 / 부패의 파동: 자기 5x5, 0, 부패 2턴
///  - 시체왕 2페이즈 난타: 마름모 1 (상하좌우), 5x2 / 부패 폭발: 3x3, 10, 부패 2턴
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Area Attack", fileName = "AreaAttack")]
public class Ch1AreaAttack : Ch1AttackAction
{
    [Header("범위")]
    [SerializeField] private Ch1AreaCenter _center = Ch1AreaCenter.Self;
    [Tooltip("1 = 3x3, 2 = 5x5")]
    [Min(1)][SerializeField] private int _radius = 1;
    [Tooltip("마름모 (상하좌우 걸음 수 기준). 끄면 사각형")]
    [SerializeField] private bool _diamond = false;
    [Tooltip("중심 칸 포함 (중심이 자기 칸이면 무시)")]
    [SerializeField] private bool _includeCenter = false;
    [Tooltip("막힌 칸(몬스터, 신체 타일, 장애물)은 범위에서 뺀다")]
    [SerializeField] private bool _stopAtObstacles = true;
    [Tooltip("0 보다 크면 중심 칸에 맞았을 때 이 피해를 준다 (사령탄 1x1 = 8)")]
    [Min(0)][SerializeField] private int _centerDamage = 0;

    [Header("부패 지대")]
    [Tooltip("0 보다 크면 범위 칸에 부패 지대를 만든다 (유지 턴)")]
    [Min(0)][SerializeField] private int _rotZoneTurns = 0;
    [Min(0)][SerializeField] private int _rotZoneDamage = 2;

    protected override void BuildCells(EnemyController enemy, EnemyIntent intent)
    {
        Ch1.FaceTarget(enemy);

        Vector2Int center;
        switch (_center)
        {
            case Ch1AreaCenter.Player:
                center = enemy.IsPlayerInRoom ? enemy.Player.Cell : enemy.Cell;
                break;
            case Ch1AreaCenter.NearestBodyTile:
                center = Ch1.NearestBodyCell(enemy);
                break;
            default:
                center = enemy.Cell;
                break;
        }

        bool includeCenter = _includeCenter && center != enemy.Cell;
        Ch1.AddSquare(enemy.Room.Grid, center, _radius, includeCenter, _diamond, _stopAtObstacles, intent.Cells);
        intent.PlanData = center;

        if (_centerDamage > 0)
            intent.ValueLabel = $"{Mathf.Max(0, _centerDamage + (_addStrength ? enemy.Strength : 0))}/{intent.Value}";
    }

    protected override int DamageAt(EnemyController enemy, EnemyIntent intent, Vector2Int playerCell)
    {
        if (_centerDamage > 0 && intent.PlanData is Vector2Int center && center == playerCell)
            return Mathf.Max(0, _centerDamage + (_addStrength ? enemy.Strength : 0));
        return intent.Value;
    }

    protected override IEnumerator AfterAttack(EnemyController enemy, EnemyIntent intent)
    {
        if (_rotZoneTurns > 0 && enemy.Room != null)
        {
            HazardZones zones = HazardZones.Get(enemy.Room);
            foreach (Vector2Int c in intent.Cells) zones.AddRotZone(c, _rotZoneTurns, _rotZoneDamage);
        }
        yield break;
    }
}
