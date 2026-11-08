using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 1챕터 몬스터 행동 모음 (기획서: 망자의 병사 / 부패한 사수 / 역병 구울 / 묘지의 수호자 / 사령술사 / 시체왕).
// 모두 EnemyAction 이라서 기존 Sequence / Weighted 선택기에도 그대로 넣을 수 있다.
// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Ch1 → ...
// (메뉴 GreedDungeon → 1챕터 몬스터 만들기 를 누르면 기획서 수치로 전부 자동 생성된다)

// ───────────────────────── 공통 ─────────────────────────

/// <summary>
/// 공격 행동 공통: 피해 계산(힘 포함), 다단 히트, 적중 시 부패/속박 부여, 생명력 흡수, 공격 후 이동.
/// 범위는 자식 클래스가 BuildCells 에서 정한다. 예고한 칸 그대로 판정한다.
/// </summary>
public abstract class Ch1AttackAction : EnemyAction
{
    [Header("공격")]
    [Min(0)][SerializeField] protected int _damage = 5;
    [Tooltip("같은 범위를 몇 번 때리는지 (난타 = 2)")]
    [Min(1)][SerializeField] protected int _hits = 1;
    [Tooltip("적의 힘(광폭화 포함)을 피해에 더한다")]
    [SerializeField] protected bool _addStrength = true;
    [Min(0f)][SerializeField] protected float _hitInterval = 0.15f;

    [Header("적중 시 상태 이상")]
    [Tooltip("부패: 턴 종료마다 피해")]
    [Min(0)][SerializeField] protected int _decayTurns = 0;
    [Min(0)][SerializeField] protected int _decayDamage = 2;
    [Tooltip("속박: 이동 불가")]
    [Min(0)][SerializeField] protected int _rootTurns = 0;

    [Header("특수")]
    [Tooltip("체력에 실제로 들어간 피해만큼 회복 (방어도로 막힌 피해는 제외)")]
    [SerializeField] protected bool _lifeSteal = false;
    [Tooltip("공격 후 플레이어 쪽으로 이동하는 칸 수 (육체 돌진 = 2)")]
    [Min(0)][SerializeField] protected int _moveTowardAfter = 0;

    public int BaseDamage => _damage;

    protected int PlannedDamage(EnemyController e) => Mathf.Max(0, _damage + (_addStrength ? e.Strength : 0));

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        intent.Cells.Clear();
        intent.Value = PlannedDamage(enemy);
        intent.ValueLabel = _hits > 1 ? $"{intent.Value}x{_hits}" : null;

        if (enemy.Room != null) BuildCells(enemy, intent);
        intent.Facing = enemy.Facing;
    }

    /// <summary>공격 범위를 intent.Cells 에 채운다 (방향을 바꿀 거면 여기서 enemy.Face)</summary>
    protected abstract void BuildCells(EnemyController enemy, EnemyIntent intent);

    /// <summary>플레이어가 맞은 칸에 따라 피해를 바꿀 때 (사령탄 중심 칸 등)</summary>
    protected virtual int DamageAt(EnemyController enemy, EnemyIntent intent, Vector2Int playerCell) => intent.Value;

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f) yield return new WaitForSeconds(enemy.ActionDelay);
        enemy.Face(intent.Facing);

        for (int i = 0; i < _hits; i++)
        {
            if (!Ch1.PlayerReachable(enemy)) break;

            Vector2Int playerCell = enemy.Player.Cell;
            bool hit = intent.Cells.Contains(playerCell);
            Debug.Log($"[Enemy] {enemy.name} {DisplayName} {(_hits > 1 ? $"{i + 1}/{_hits} " : "")}→ {(hit ? "적중" : "빗나감")}", enemy);

            if (hit)
            {
                DamageResult result = Ch1.HitPlayer(enemy, DamageAt(enemy, intent, playerCell));
                OnHit(enemy, intent, result);
            }

            if (i < _hits - 1 && _hitInterval > 0f) yield return new WaitForSeconds(_hitInterval);
        }

        yield return AfterAttack(enemy, intent);

        if (_moveTowardAfter > 0 && enemy.IsAlive)
            yield return enemy.StartCoroutine(enemy.MoveTowardPlayer(_moveTowardAfter));
    }

    protected virtual void OnHit(EnemyController enemy, EnemyIntent intent, DamageResult result)
    {
        GridPlayer player = enemy.Player;
        if (player == null) return;

        if (_decayTurns > 0)
            StatusEffectHolder.GetOrAdd(player.gameObject).Apply(StatusType.Decay, _decayTurns, _decayDamage);
        if (_rootTurns > 0)
            StatusEffectHolder.GetOrAdd(player.gameObject).Apply(StatusType.Root, _rootTurns);

        if (_lifeSteal && result.HealthLost > 0 && enemy.Health != null)
            enemy.Health.Heal(result.HealthLost);
    }

    protected virtual IEnumerator AfterAttack(EnemyController enemy, EnemyIntent intent)
    {
        yield break;
    }
}

/// <summary>1챕터 행동들이 같이 쓰는 계산</summary>
public static class Ch1
{
    public static bool PlayerReachable(EnemyController e) =>
        e != null && e.IsPlayerInRoom && (e.Manager == null || !e.Manager.IsPlayerDead);

    public static PlayerHealth PlayerHealthOf(EnemyController e)
    {
        if (e.Manager != null && e.Manager.PlayerHealth != null) return e.Manager.PlayerHealth;
        return e.Player != null ? e.Player.GetComponent<PlayerHealth>() : null;
    }

    public static DamageResult HitPlayer(EnemyController e, int damage)
    {
        PlayerHealth health = PlayerHealthOf(e);
        if (health == null || damage <= 0) return DamageResult.None;
        return health.TakeDamage(DamageInfo.Attack(damage, e.gameObject));
    }

    public static void FaceTarget(EnemyController e)
    {
        if (e.IsPlayerInRoom) e.Face(AttackArea.DirectionTowards(e.Cell, e.Player.Cell));
    }

    /// <summary>origin 다음 칸부터 dir 방향으로 minRange~maxRange 칸</summary>
    public static void AddLine(RoomGrid grid, Vector2Int origin, Vector2Int dir, int minRange, int maxRange,
                               bool stopAtObstacles, List<Vector2Int> result)
    {
        Vector2Int cell = origin;
        for (int i = 1; i <= maxRange; i++)
        {
            cell += dir;
            if (!grid.InBounds(cell)) break;
            if (stopAtObstacles && !grid.IsWalkable(cell)) break;
            if (i >= minRange && !result.Contains(cell)) result.Add(cell);
        }
    }

    /// <summary>center 기준 사각형(반지름 r). 막힌 칸(몬스터, 신체 타일, 장애물)은 stopAtObstacles 면 빠진다</summary>
    public static void AddSquare(RoomGrid grid, Vector2Int center, int radius, bool includeCenter, bool diamond,
                                 bool stopAtObstacles, List<Vector2Int> result)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0 && !includeCenter) continue;
                if (diamond && Mathf.Abs(x) + Mathf.Abs(y) > radius) continue;

                Vector2Int c = center + new Vector2Int(x, y);
                if (!grid.InBounds(c)) continue;
                if (stopAtObstacles && !grid.IsWalkable(c) && !(c == center && includeCenter)) continue;
                if (!result.Contains(c)) result.Add(c);
            }
        }
    }

    /// <summary>
    /// 다칸 몬스터(시체왕 1페이즈)면 신체 타일 중 플레이어와 가장 가까운 칸, 아니면 자기 칸.
    /// 파괴된 신체 타일은 목록에 없으므로 자동으로 제외된다.
    /// </summary>
    public static Vector2Int NearestBodyCell(EnemyController e)
    {
        IGridFootprint footprint = e.GetComponent<IGridFootprint>();
        if (footprint == null || !e.IsPlayerInRoom) return e.Cell;

        Vector2Int best = e.Cell;
        float bestDistance = float.MaxValue;
        foreach (Vector2Int c in footprint.OccupiedCells)
        {
            float d = (c - e.Player.Cell).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = c;
            }
        }
        return best;
    }

    /// <summary>
    /// 시체왕 1페이즈면 플레이어와 가장 가까운 시체 타일, 아니면 NearestBodyCell.
    /// 파괴된 시체 타일은 목록에 없으므로 자동으로 제외된다.
    /// </summary>
    public static Vector2Int NearestCorpseTile(EnemyController e)
    {
        CorpseKingBoss boss = e.GetComponent<CorpseKingBoss>();
        if (boss != null && e.IsPlayerInRoom && boss.TryGetNearestCorpseTile(e.Player.Cell, out Vector2Int cell))
            return cell;
        return NearestBodyCell(e);
    }

    /// <summary>빈 인접 칸 (상하좌우 먼저, 그다음 대각선)</summary>
    public static bool TryFindFreeNeighbor(EnemyController e, out Vector2Int cell)
    {
        cell = e.Cell;
        if (e.Room == null) return false;

        var candidates = new List<Vector2Int>();
        foreach (Vector2Int d in EnemyPathfinding.FourDirections) candidates.Add(e.Cell + d);
        candidates.Add(e.Cell + new Vector2Int(1, 1));
        candidates.Add(e.Cell + new Vector2Int(-1, 1));
        candidates.Add(e.Cell + new Vector2Int(1, -1));
        candidates.Add(e.Cell + new Vector2Int(-1, -1));

        foreach (Vector2Int c in candidates)
        {
            if (!e.CanEnter(c)) continue;
            cell = c;
            return true;
        }
        return false;
    }
}
