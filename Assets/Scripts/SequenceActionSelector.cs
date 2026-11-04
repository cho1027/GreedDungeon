using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정해진 순서대로 행동한다 (예: 직선 공격 → 방어 → 가로 공격 → 반복). 패턴이 고정된 몬스터나 테스트용.
/// 만들기: Project 우클릭 → Create → GridDungeon → Enemy → Sequence Action Selector
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Sequence Action Selector", fileName = "SequenceActionSelector")]
public class SequenceActionSelector : EnemyActionSelector
{
    [SerializeField] private List<EnemyAction> _sequence = new List<EnemyAction>();

    [Tooltip("끝까지 가면 처음부터 반복. 끄면 마지막 행동을 계속한다")]
    [SerializeField] private bool _loop = true;

    public override EnemyAction Choose(EnemyController enemy)
    {
        int count = _sequence.Count;
        if (count == 0) return null;

        // 이 적이 지금까지 실행한 행동 수 = 다음 순번 (적마다 따로 센다)
        // PhaseActionSelector 안에서 쓰면 페이즈가 바뀔 때마다 처음부터 다시 센다
        int done = Mathf.Max(0, enemy.PhaseActionCount);
        int index = _loop ? done % count : Mathf.Min(done, count - 1);

        // 빈 칸은 건너뛴다
        for (int i = 0; i < count; i++)
        {
            EnemyAction action = _sequence[(index + i) % count];
            if (action != null) return action;
        }
        return null;
    }
}
