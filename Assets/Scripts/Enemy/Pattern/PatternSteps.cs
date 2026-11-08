using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 패턴 단계 모음. 새 단계는 PatternStep 을 상속해 이 파일이나 새 파일에 추가하면 된다.

// ───────────────────────── 공격 ─────────────────────────

/// <summary>범위 공격. 모양/피해/타수를 정한다. 예고한 칸 그대로 판정한다</summary>
[Serializable]
public class AttackStep : PatternStep
{
    [Tooltip("공격 범위 (아래 격자 미리보기: 위쪽 = 바라보는 방향)")]
    public PatternShape shape = new PatternShape();

    [Min(0)] public int damage = 5;

    [Tooltip("같은 범위를 몇 번 때리는지 (다단 공격, 방어막도 매번 따로 깎인다)")]
    [Min(1)] public int hits = 1;

    [Tooltip("계획할 때 플레이어 쪽(가로/세로 중 먼 축)으로 돈다. 끄면 그때 바라보던 방향 그대로")]
    public bool faceTarget = true;

    [Tooltip("적의 힘(Strength)을 피해에 더한다")]
    public bool addStrength = true;

    [Tooltip("다단 공격 사이 간격 (초)")]
    [Min(0f)] public float hitInterval = 0.15f;

    public override string Label => "공격";

    public override string Describe()
    {
        string times = hits > 1 ? $"x{hits}" : string.Empty;
        return $"공격 {damage}{times} ({(shape != null ? shape.Describe() : "범위 없음")})";
    }

    public override void Plan(PatternContext ctx, StepPlan plan)
    {
        if (faceTarget && ctx.PlayerInRoom)
            ctx.Facing = AttackArea.DirectionTowards(ctx.Cell, ctx.Player.Cell);

        // 이동하기 전이면 지금 몸을 돌려서 어느 쪽을 칠지 보여준다
        if (!ctx.MovedDuringPlan) ctx.Enemy.Face(ctx.Facing);

        base.Plan(ctx, plan);
        plan.IsAttack = true;
        plan.Hits = Mathf.Max(1, hits);
        plan.Value = Mathf.Max(0, damage + (addStrength ? ctx.PlannedStrength : 0));

        if (ctx.Grid != null && shape != null)
            shape.Calculate(ctx.Grid, ctx.Cell, ctx.Facing, plan.Cells);
    }

    public override IEnumerator Execute(PatternContext ctx, StepPlan plan)
    {
        ctx.Enemy.Face(plan.Facing);

        for (int i = 0; i < plan.Hits; i++)
        {
            if (!ctx.PlayerInRoom || ctx.IsPlayerDead) yield break;

            bool hit = plan.Cells.Contains(ctx.Player.Cell);
            Debug.Log($"[Enemy] {ctx.Enemy.name} {ctx.Pattern.DisplayName} 공격 {i + 1}/{plan.Hits} → {(hit ? "적중" : "빗나감")}", ctx.Enemy);

            ctx.Enemy.Manager?.CardState.HitDevices(plan.Cells, plan.Value);
            if (hit)
            {
                PlayerHealth health = ctx.PlayerHealth;
                if (health != null) health.TakeDamage(DamageInfo.Attack(plan.Value, ctx.Enemy.gameObject));
            }

            if (i < plan.Hits - 1 && hitInterval > 0f)
                yield return new WaitForSeconds(hitInterval);
        }
    }
}

// ───────────────────────── 방어막 ─────────────────────────

/// <summary>방어막 획득. 다음 플레이어 턴 동안 유지되고, 이 적의 다음 턴 시작 때 사라진다</summary>
[Serializable]
public class ShieldStep : PatternStep
{
    [Min(0)] public int amount = 6;

    public override string Label => "방어막";
    public override string Describe() => $"방어막 {amount}";

    public override void Plan(PatternContext ctx, StepPlan plan)
    {
        base.Plan(ctx, plan);
        plan.IsShield = true;
        plan.Value = amount;
    }

    public override IEnumerator Execute(PatternContext ctx, StepPlan plan)
    {
        if (ctx.Enemy.Shield == null)
        {
            Debug.LogWarning($"{ctx.Enemy.name}: DamageShield 컴포넌트가 없어 방어막을 얻지 못했습니다.", ctx.Enemy);
            yield break;
        }

        ctx.Enemy.Shield.Add(plan.Value);
    }
}

// ───────────────────────── 이동 ─────────────────────────

public enum PatternMoveMode
{
    TowardPlayer = 0, // 추적 (길찾기)
    AwayFromPlayer,   // 거리 벌리기
    Charge            // 돌진: 한 방향으로 막힐 때까지
}

public enum PatternMoveTiming
{
    Auto = 0,          // 뒤에 공격 예고가 있으면 계획한 경로 그대로, 없으면 적 턴에 다시 계산
    FixedWhenPlanned,  // 항상 예고 시점에 계산한 경로 그대로
    RecalculateOnTurn  // 항상 적 턴에 플레이어 위치로 다시 계산
}

/// <summary>
/// 이동. 추적은 EnemyPathfinding (기본 BFS, A* 를 넣으면 A*) 을 쓴다.
/// 이동 뒤에 공격이 오면 공격 범위는 "이동한 뒤 위치" 기준으로 예고된다.
/// </summary>
[Serializable]
public class MoveStep : PatternStep
{
    public PatternMoveMode mode = PatternMoveMode.TowardPlayer;

    [Tooltip("최대 이동 칸 수")]
    [Min(1)] public int steps = 1;

    [Tooltip("추적 전용: 플레이어와의 거리(상하좌우 칸 수)가 이 값 이하가 되면 멈춘다. 1 = 바로 옆까지")]
    [Min(1)] public int stopDistance = 1;

    [Tooltip("추적/후퇴: 이동 후 플레이어 쪽을 바라본다. 돌진: 출발 전에 플레이어 쪽으로 돈다 (끄면 바라보던 방향으로 돌진)")]
    public bool faceTarget = true;

    [Tooltip("Auto: 뒤에 공격 예고가 있으면 예고한 경로 그대로 / 없으면 적 턴의 플레이어 위치로 다시 계산")]
    public PatternMoveTiming timing = PatternMoveTiming.Auto;

    public override string Label
    {
        get
        {
            switch (mode)
            {
                case PatternMoveMode.AwayFromPlayer: return "후퇴";
                case PatternMoveMode.Charge: return "돌진";
                default: return "추적 이동";
            }
        }
    }

    public override string Describe() => $"{Label} {steps}칸";

    public override void Plan(PatternContext ctx, StepPlan plan)
    {
        plan.Origin = ctx.Cell;

        Vector2Int facing = ctx.Facing;
        BuildPath(ctx.Enemy, ctx.Cell, ref facing, plan.Path);
        ctx.Facing = facing;

        if (plan.Path.Count > 0)
        {
            Vector2Int last = plan.Path[plan.Path.Count - 1];
            Vector2Int before = plan.Path.Count > 1 ? plan.Path[plan.Path.Count - 2] : ctx.Cell;
            ctx.Cell = last;
            ctx.Facing = last - before;
            ctx.MovedDuringPlan = true;
        }

        if (faceTarget && mode != PatternMoveMode.Charge && ctx.PlayerInRoom)
            ctx.Facing = AttackArea.DirectionTowards(ctx.Cell, ctx.Player.Cell);

        plan.Facing = ctx.Facing;
    }

    public override IEnumerator Execute(PatternContext ctx, StepPlan plan)
    {
        EnemyController enemy = ctx.Enemy;

        bool recalculate = timing == PatternMoveTiming.RecalculateOnTurn ||
                           (timing == PatternMoveTiming.Auto && !ctx.HasTelegraphAfter);

        List<Vector2Int> path = plan.Path;
        if (recalculate)
        {
            path = new List<Vector2Int>();
            Vector2Int facing = enemy.Facing;
            BuildPath(enemy, enemy.Cell, ref facing, path);
            if (mode == PatternMoveMode.Charge) enemy.Face(facing);
        }

        foreach (Vector2Int cell in path)
        {
            // 한 칸씩: 옆 칸이 아니거나(앞에서 막혀 멈춘 경우) 막혔으면 거기서 멈춘다
            if (EnemyPathfinding.Manhattan(enemy.Cell, cell) != 1) break;
            if (!enemy.CanEnter(cell)) break;

            yield return enemy.StepTo(cell);
            if (!enemy.IsAlive) yield break;
        }

        if (faceTarget && mode != PatternMoveMode.Charge && enemy.IsPlayerInRoom)
            enemy.Face(AttackArea.DirectionTowards(enemy.Cell, enemy.Player.Cell));
    }

    /// <summary>from 에서 출발하는 경로를 path 에 담는다 (from 제외)</summary>
    private void BuildPath(EnemyController enemy, Vector2Int from, ref Vector2Int facing, List<Vector2Int> path)
    {
        path.Clear();
        if (enemy.Room == null) return;

        RoomGrid grid = enemy.Room.Grid;
        bool playerIn = enemy.IsPlayerInRoom;
        Vector2Int player = playerIn ? enemy.Player.Cell : new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int self = enemy.Cell; // 계획 중에는 아직 이 칸에 서 있으므로 지나갈 수 있는 칸으로 친다

        bool Free(Vector2Int c) =>
            grid.InBounds(c) && c != player && (c == self || grid.IsWalkable(c));

        switch (mode)
        {
            case PatternMoveMode.TowardPlayer:
            {
                if (!playerIn) return;

                var route = new List<Vector2Int>();
                if (!EnemyPathfinding.FindPath(grid, from, player, Free, route))
                {
                    // 길이 막혔으면 가까워지는 쪽으로 한 칸씩이라도
                    GreedySteps(from, player, Free, path, closer: true);
                    return;
                }

                Vector2Int current = from;
                foreach (Vector2Int cell in route)
                {
                    if (path.Count >= steps) break;
                    if (EnemyPathfinding.Manhattan(current, player) <= stopDistance) break;
                    if (cell == player) break;

                    path.Add(cell);
                    current = cell;
                }
                break;
            }

            case PatternMoveMode.AwayFromPlayer:
                if (!playerIn) return;
                GreedySteps(from, player, Free, path, closer: false);
                break;

            case PatternMoveMode.Charge:
            {
                Vector2Int dir = faceTarget && playerIn ? AttackArea.DirectionTowards(from, player) : facing;
                if (dir == Vector2Int.zero) dir = Vector2Int.down;
                facing = dir;

                Vector2Int current = from;
                for (int i = 0; i < steps; i++)
                {
                    Vector2Int next = current + dir;
                    if (!Free(next)) break;
                    path.Add(next);
                    current = next;
                }
                break;
            }
        }
    }

    /// <summary>매 걸음 플레이어와 가까워지는(멀어지는) 옆 칸으로 이동</summary>
    private void GreedySteps(Vector2Int from, Vector2Int player, Func<Vector2Int, bool> free,
                             List<Vector2Int> path, bool closer)
    {
        Vector2Int current = from;
        for (int i = 0; i < steps; i++)
        {
            int currentDistance = EnemyPathfinding.Manhattan(current, player);
            if (closer && currentDistance <= stopDistance) break;

            Vector2Int best = current;
            int bestDistance = currentDistance;

            foreach (Vector2Int dir in EnemyPathfinding.FourDirections)
            {
                Vector2Int next = current + dir;
                if (!free(next) || path.Contains(next)) continue;

                int d = EnemyPathfinding.Manhattan(next, player);
                if (closer ? d < bestDistance : d > bestDistance)
                {
                    best = next;
                    bestDistance = d;
                }
            }

            if (best == current) break;
            path.Add(best);
            current = best;
        }
    }
}

// ───────────────────────── 힘 ─────────────────────────

/// <summary>힘 증감. 이후 공격 피해에 더해진다 (음수면 약화)</summary>
[Serializable]
public class StrengthStep : PatternStep
{
    public int amount = 2;

    public override string Label => "힘 증감";
    public override string Describe() => amount >= 0 ? $"힘 +{amount}" : $"힘 {amount}";

    public override void Plan(PatternContext ctx, StepPlan plan)
    {
        base.Plan(ctx, plan);
        plan.Value = amount;
        ctx.PlannedStrength += amount; // 같은 패턴 뒤쪽 공격의 예고 수치에 반영
    }

    public override IEnumerator Execute(PatternContext ctx, StepPlan plan)
    {
        ctx.Enemy.AddStrength(amount);
        Debug.Log($"[Enemy] {ctx.Enemy.name} 힘 {(amount >= 0 ? "+" : "")}{amount} → {ctx.Enemy.Strength}", ctx.Enemy);
        yield break;
    }
}

// ───────────────────────── 대기 ─────────────────────────

/// <summary>아무것도 하지 않는다 (기 모으기, 숨 고르기 등 연출용 턴)</summary>
[Serializable]
public class IdleStep : PatternStep
{
    public override string Label => "대기";

    public override IEnumerator Execute(PatternContext ctx, StepPlan plan)
    {
        yield break;
    }
}
