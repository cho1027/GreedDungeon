using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 적 패턴 하나 = 에셋 하나. 안에 단계(공격, 방어막, 이동, 힘, 대기)를 순서대로 쌓아서 만든다.
/// 예)  "돌진 3칸 → 정면 3줄 공격 8"  /  "방어막 10 + 힘 +2"  /  "추적 2칸 → 주변 1칸 공격 4x2"
///
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Pattern
/// 인스펙터의 "+ 단계 추가" 로 단계를 넣고, ▲▼ 로 순서를 바꾼다. 맨 아래에 패턴 요약이 나온다.
///
/// 기존 AttackAction / DefendAction 처럼 EnemyAction 이므로 Sequence / Weighted / Phase 선택기에 그대로 넣으면 된다.
/// 머리 위 수치: 공격이 1개면 "피해" 또는 "피해x타수", 여러 개면 총 피해, 공격이 없으면 방어막 합.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Pattern", fileName = "Pattern")]
public class EnemyPattern : EnemyAction
{
    [SerializeReference] private List<PatternStep> _steps = new List<PatternStep>();

    public IReadOnlyList<PatternStep> Steps => _steps;

    // ───────────────────────── 계획 (예고) ─────────────────────────

    public override void Plan(EnemyController enemy, EnemyIntent intent)
    {
        var ctx = new PatternContext(enemy, intent, this);
        var plans = new List<StepPlan>(_steps.Count);

        for (int i = 0; i < _steps.Count; i++)
        {
            var plan = new StepPlan();
            PatternStep step = _steps[i];

            if (step != null)
            {
                ctx.StepIndex = i;
                step.Plan(ctx, plan);
            }

            plans.Add(plan);

            // 모든 단계의 공격 범위를 합쳐서 바닥에 표시
            foreach (Vector2Int cell in plan.Cells)
                if (!intent.Cells.Contains(cell)) intent.Cells.Add(cell);
        }

        intent.PlanData = plans;
        intent.Facing = ctx.Facing;
        SetDisplayValue(intent, plans);
    }

    private static void SetDisplayValue(EnemyIntent intent, List<StepPlan> plans)
    {
        int attackCount = 0, totalDamage = 0, firstDamage = 0, firstHits = 1, shield = 0;

        foreach (StepPlan p in plans)
        {
            if (p.IsAttack)
            {
                attackCount++;
                totalDamage += p.Value * p.Hits;
                if (attackCount == 1)
                {
                    firstDamage = p.Value;
                    firstHits = p.Hits;
                }
            }
            else if (p.IsShield)
            {
                shield += p.Value;
            }
        }

        intent.ValueLabel = null;
        if (attackCount == 1)
        {
            intent.Value = firstDamage;
            if (firstHits > 1) intent.ValueLabel = $"{firstDamage}x{firstHits}";
        }
        else if (attackCount > 1)
        {
            intent.Value = totalDamage;
        }
        else
        {
            intent.Value = shield;
        }
    }

    // ───────────────────────── 실행 ─────────────────────────

    public override IEnumerator Execute(EnemyController enemy, EnemyIntent intent)
    {
        if (enemy.ActionDelay > 0f)
            yield return new WaitForSeconds(enemy.ActionDelay);

        var plans = intent.PlanData as List<StepPlan>;
        if (plans == null || plans.Count != _steps.Count)
        {
            // 예고 없이 실행되는 경우 대비 (보통은 생기지 않음)
            intent.Cells.Clear();
            Plan(enemy, intent);
            plans = (List<StepPlan>)intent.PlanData;
        }

        var ctx = new PatternContext(enemy, intent, this);

        for (int i = 0; i < _steps.Count; i++)
        {
            PatternStep step = _steps[i];
            if (step == null) continue;

            ctx.StepIndex = i;
            ctx.Cell = enemy.Cell;
            ctx.Facing = enemy.Facing;
            ctx.HasTelegraphAfter = HasTelegraphAfter(plans, i);

            IEnumerator routine = step.Execute(ctx, plans[i]);
            if (routine != null) yield return routine;

            if (!enemy.IsAlive || ctx.IsPlayerDead) yield break;

            if (step.delayAfter > 0f && i < _steps.Count - 1)
                yield return new WaitForSeconds(step.delayAfter);
        }
    }

    private static bool HasTelegraphAfter(List<StepPlan> plans, int index)
    {
        for (int j = index + 1; j < plans.Count; j++)
            if (plans[j].Cells.Count > 0) return true;
        return false;
    }

    // ───────────────────────── 요약 ─────────────────────────

    /// <summary>"추적 이동 2칸 → 공격 5x2 (직선 3칸)" 같은 한 줄 요약</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        foreach (PatternStep step in _steps)
        {
            if (step == null) continue;
            if (sb.Length > 0) sb.Append(" → ");
            sb.Append(step.Describe());
        }
        return sb.Length > 0 ? sb.ToString() : "(단계 없음)";
    }

#if UNITY_EDITOR
    /// <summary>에디터 전용: 단계 추가 (인스펙터 버튼에서 사용)</summary>
    public void EditorAddStep(PatternStep step)
    {
        if (step != null) _steps.Add(step);
    }
#endif
}
