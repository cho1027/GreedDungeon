using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스용: 체력 % 에 따라 페이즈를 나누고, 페이즈마다 다른 선택 규칙(Sequence / Weighted)을 쓴다.
///  - 페이즈는 위에서부터 체력 % 가 높은 순서로 적는다 (첫 페이즈 = 100)
///  - 체력이 그 % 이하로 떨어진 다음 행동부터 다음 페이즈가 된다 (되돌아가지 않음)
///  - Enter Action 을 넣으면 그 페이즈에 들어갈 때 한 번만 쓴다 (예: 2페이즈 진입 포효)
///  - 안쪽 Sequence 선택기는 페이즈가 바뀔 때마다 처음부터 다시 센다
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Phase Action Selector
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Phase Action Selector", fileName = "PhaseActionSelector")]
public class PhaseActionSelector : EnemyActionSelector
{
    [Serializable]
    public class Phase
    {
        public string name = "1페이즈";

        [Tooltip("체력이 이 % 이하일 때 이 페이즈 (첫 페이즈는 100)")]
        [Range(1, 100)] public int healthPercent = 100;

        [Tooltip("이 페이즈에서 행동을 고르는 규칙 (Sequence / Weighted 선택기 에셋)")]
        public EnemyActionSelector selector;

        [Tooltip("이 페이즈에 처음 들어갈 때 한 번만 쓰는 행동 (선택)")]
        public EnemyAction enterAction;
    }

    [Tooltip("체력 % 가 높은 페이즈부터 위에서 아래로")]
    [SerializeField] private List<Phase> _phases = new List<Phase>();

    public override EnemyAction Choose(EnemyController enemy)
    {
        if (_phases.Count == 0) return null;

        int target = Mathf.Max(FindPhase(enemy), enemy.SelectorPhase); // 되돌아가지 않음
        target = Mathf.Clamp(target, 0, _phases.Count - 1);
        Phase phase = _phases[target];

        if (target != enemy.SelectorPhase)
        {
            bool hasEnter = phase.enterAction != null;
            enemy.EnterSelectorPhase(target, hasEnter);
            Debug.Log($"[Enemy] {enemy.name} → {phase.name}", enemy);

            if (hasEnter) return phase.enterAction;
        }

        if (phase.selector == null || phase.selector == this) return null;
        return phase.selector.Choose(enemy);
    }

    /// <summary>현재 체력에 맞는 가장 깊은 페이즈</summary>
    private int FindPhase(EnemyController enemy)
    {
        EnemyHealth health = enemy.Health;
        float percent = health != null && health.MaxHealth > 0
            ? health.CurrentHealth * 100f / health.MaxHealth
            : 100f;

        int result = 0;
        for (int i = 0; i < _phases.Count; i++)
        {
            if (_phases[i] != null && percent <= _phases[i].healthPercent) result = i;
        }
        return result;
    }
}
