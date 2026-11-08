using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부패한 사수
///  1. 플레이어가 같은 줄(전방) 사거리 안이면 일반 사격 / 관통 사격 중 무작위
///  2. 아니면 대각선 사격
///  3. 관통 사격은 2회 연속 사용 불가
///  (후퇴/접근은 EnemyController 의 Post Move = KeepDistance 가 처리)
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Enemy/Ch1/Archer Selector", fileName = "ArcherSelector")]
public class Ch1ArcherSelector : EnemyActionSelector
{
    [SerializeField] private EnemyAction _normalShot;
    [SerializeField] private EnemyAction _pierceShot;
    [SerializeField] private EnemyAction _diagonalShot;
    [Tooltip("사거리 (같은 줄에서 이 거리 이내면 직선 사격)")]
    [Min(1)][SerializeField] private int _range = 5;

    public override EnemyAction Choose(EnemyController enemy)
    {
        if (Ch1Select.IsAligned(enemy, _range))
        {
            bool pierceAllowed = _pierceShot != null && enemy.LastAction != _pierceShot;
            if (_normalShot == null) return pierceAllowed ? _pierceShot : _diagonalShot;
            if (pierceAllowed && Random.value < 0.5f) return _pierceShot;
            return _normalShot;
        }
        return _diagonalShot != null ? _diagonalShot : _normalShot;
    }
}
