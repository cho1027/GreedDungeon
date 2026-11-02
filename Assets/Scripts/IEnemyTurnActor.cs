using System.Collections;

/// <summary>
/// 적의 턴 행동. 몬스터 프리팹(또는 자식)에 이 인터페이스를 구현한 컴포넌트를 붙이면
/// 적 턴마다 BattleTurnManager 가 TakeTurn 을 차례대로 실행하고, 끝날 때까지 기다린다.
/// 구현한 컴포넌트가 없는 몬스터는 잠깐 대기하는 임시 행동으로 처리된다.
/// (기본 적은 EnemyController 가 구현한다)
/// </summary>
public interface IEnemyTurnActor
{
    /// <summary>false 면 살아 있는 적으로 치지 않는다 (전투 상태 판정, 턴 행동 모두 제외)</summary>
    bool IsAlive { get; }

    /// <summary>
    /// 이번 턴의 행동 (이동, 공격 등). 행동이 끝날 때 코루틴이 끝나야 한다.
    /// manager.Player, manager.CurrentRoom 으로 플레이어와 방에 접근할 수 있다.
    /// </summary>
    IEnumerator TakeTurn(BattleTurnManager manager);
}

/// <summary>
/// 전투 시작을 알림받는 적 (선택). 전투가 시작되면 첫 플레이어 턴 전에 호출된다.
/// 첫 행동 예고(아이콘, 공격 범위 표시)를 여기서 정한다.
/// </summary>
public interface IEnemyCombatListener
{
    void OnCombatStarted(BattleTurnManager manager);
}
