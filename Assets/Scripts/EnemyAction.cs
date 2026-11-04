using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 한 마리가 "다음에 할 행동"으로 확정한 내용. 적마다 따로 만들어지므로 여러 적의 상태가 섞이지 않는다.
/// 예고 표시(아이콘, 수치, 바닥 범위)와 실제 실행이 모두 이 데이터를 그대로 사용한다.
/// </summary>
public class EnemyIntent
{
    public EnemyIntent(EnemyAction action)
    {
        Action = action;
    }

    public EnemyAction Action { get; }

    /// <summary>행동 방향 (확정 시점의 적의 방향)</summary>
    public Vector2Int Facing;

    /// <summary>표시할 수치 (공격이면 피해량, 방어면 방어막량)</summary>
    public int Value;

    /// <summary>수치 대신 표시할 글자 (예: "5x2"). 비어 있으면 Value 를 표시한다</summary>
    public string ValueLabel;

    /// <summary>행동이 계획 단계에서 저장해 두는 추가 정보 (EnemyPattern 의 단계별 계획 등)</summary>
    public object PlanData;

    /// <summary>공격 범위 칸. 바닥 표시와 피해 판정이 이 목록 하나를 같이 쓴다</summary>
    public readonly List<Vector2Int> Cells = new List<Vector2Int>();
}

/// <summary>
/// 적 행동 하나 (공격, 방어, 이후 돌진·원거리·소환 등). 에셋으로 만들어 몬스터마다 조합한다.
/// 에셋은 여러 적이 공유하므로 실행 중 상태를 필드에 저장하지 않고, 모든 상태는 EnemyIntent / EnemyController 에 둔다.
/// 새 행동은 이 클래스를 상속해 Plan / Execute 만 구현하면 된다 (EnemyController 수정 불필요).
/// </summary>
public abstract class EnemyAction : ScriptableObject
{
    [Header("예고 표시")]
    [SerializeField] private string _displayName = "행동";
    [Tooltip("적 머리 위에 표시할 아이콘. 제공받은 이미지를 연결한다 (비워두면 아이콘 없이 수치만 표시)")]
    [SerializeField] private Sprite _icon;
    [Tooltip("아이콘 옆에 수치(피해량, 방어막량)를 표시")]
    [SerializeField] private bool _showValue = true;

    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public bool ShowValue => _showValue;

    /// <summary>
    /// 다음 행동으로 확정될 때 호출. 방향, 수치, 공격 범위를 intent 에 채운다.
    /// (이 결과가 그대로 예고 표시되고, 그대로 실행된다)
    /// </summary>
    public abstract void Plan(EnemyController enemy, EnemyIntent intent);

    /// <summary>적 턴에 실행. 끝날 때 코루틴이 끝나야 한다</summary>
    public abstract IEnumerator Execute(EnemyController enemy, EnemyIntent intent);
}
