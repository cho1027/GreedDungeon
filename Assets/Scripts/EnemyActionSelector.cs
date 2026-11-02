using UnityEngine;

/// <summary>
/// 적의 다음 행동을 고르는 규칙 (행동 실행과 분리). 몬스터마다 다른 선택 규칙 에셋을 연결한다.
/// 선택에 필요한 기록(마지막 행동, 연속 횟수, 행동 횟수)은 EnemyController 에 있으므로
/// 에셋 하나를 여러 적이 공유해도 상태가 섞이지 않는다.
/// </summary>
public abstract class EnemyActionSelector : ScriptableObject
{
    /// <summary>다음 행동을 고른다. 고를 수 없으면 null</summary>
    public abstract EnemyAction Choose(EnemyController enemy);
}
