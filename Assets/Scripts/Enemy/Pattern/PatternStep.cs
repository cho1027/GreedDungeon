using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패턴을 계획/실행하는 동안 단계들이 함께 쓰는 정보.
/// 계획(Plan) 때는 Cell/Facing 이 "앞 단계까지 진행했다고 가정한" 위치와 방향이다.
/// (예: 2칸 이동 → 공격 이면, 공격 범위는 이동한 뒤 위치 기준으로 예고된다)
/// </summary>
public class PatternContext
{
    public readonly EnemyController Enemy;
    public readonly EnemyIntent Intent;
    public readonly EnemyPattern Pattern;

    /// <summary>계획: 가정한 위치 / 실행: 실제 위치</summary>
    public Vector2Int Cell;
    /// <summary>계획: 가정한 방향 / 실행: 실제 방향</summary>
    public Vector2Int Facing;
    /// <summary>계획 중 앞 단계에서 이동했는지 (이동 전에는 계획 시점에 몸을 돌려 예고를 보여준다)</summary>
    public bool MovedDuringPlan;
    /// <summary>계획 중 앞 단계의 힘 증감까지 반영한 힘</summary>
    public int PlannedStrength;

    public int StepIndex;
    /// <summary>실행 중: 이 단계 뒤에 범위 예고가 있는 단계가 남아 있는지 (이동 단계가 경로를 고정할지 판단)</summary>
    public bool HasTelegraphAfter;

    public PatternContext(EnemyController enemy, EnemyIntent intent, EnemyPattern pattern)
    {
        Enemy = enemy;
        Intent = intent;
        Pattern = pattern;
        Cell = enemy.Cell;
        Facing = enemy.Facing;
        PlannedStrength = enemy.Strength;
    }

    public RoomGrid Grid => Enemy.Room != null ? Enemy.Room.Grid : null;
    public GridPlayer Player => Enemy.Player;
    public bool PlayerInRoom => Enemy.IsPlayerInRoom;
    public bool IsPlayerDead => Enemy.Manager != null && Enemy.Manager.IsPlayerDead;

    public PlayerHealth PlayerHealth
    {
        get
        {
            if (Enemy.Manager != null && Enemy.Manager.PlayerHealth != null) return Enemy.Manager.PlayerHealth;
            return Player != null ? Player.GetComponent<PlayerHealth>() : null;
        }
    }
}

/// <summary>단계 하나의 계획 결과 (적마다, 턴마다 새로 만들어진다)</summary>
public class StepPlan
{
    /// <summary>공격 범위 (바닥 표시 + 피해 판정)</summary>
    public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    /// <summary>이동 경로 (시작 칸 제외)</summary>
    public readonly List<Vector2Int> Path = new List<Vector2Int>();

    public Vector2Int Origin;
    public Vector2Int Facing;
    public int Value;
    public int Hits = 1;
    public bool IsAttack;
    public bool IsShield;
}

/// <summary>
/// 패턴의 단계 하나 (공격, 방어막, 이동, 힘 ...). EnemyPattern 에셋 안에 직접 들어간다 (에셋을 따로 만들 필요 없음).
///
/// 새 단계 종류 추가 방법: 이 클래스를 상속하고 [Serializable] 을 붙인 뒤 Label / Plan / Execute 만 구현.
/// 인스펙터의 "+ 단계 추가" 메뉴에 자동으로 나타난다.
///  - Plan: 다음 행동으로 확정될 때. 예고할 정보(범위, 수치, 경로)를 plan 에 채운다. 여기서 실제로 무언가 바꾸면 안 된다
///  - Execute: 적 턴에 실행. plan 에 적어둔 대로 실행한다
/// 단계 객체는 에셋에 저장되어 여러 적이 공유하므로, 실행 중 상태는 필드가 아니라 plan / ctx / EnemyController 에 둔다.
/// </summary>
[Serializable]
public abstract class PatternStep
{
    [Tooltip("이 단계가 끝난 뒤 잠깐 기다리는 시간 (초)")]
    [Min(0f)] public float delayAfter = 0.15f;

    /// <summary>인스펙터 메뉴/제목에 쓰는 이름</summary>
    public abstract string Label { get; }

    /// <summary>패턴 요약에 쓰는 한 줄 설명</summary>
    public virtual string Describe() => Label;

    public virtual void Plan(PatternContext ctx, StepPlan plan)
    {
        plan.Origin = ctx.Cell;
        plan.Facing = ctx.Facing;
    }

    public abstract IEnumerator Execute(PatternContext ctx, StepPlan plan);
}
