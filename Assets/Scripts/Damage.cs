using UnityEngine;

/// <summary>피해 원인. 나중에 원인별 처리(함정 면역, 상태 이상 저항 등)를 붙일 때 사용</summary>
public enum DamageType
{
    Attack = 0, // 적의 공격
    Trap,       // 함정
    Status,     // 상태 이상 (독, 화상 등)
    Other
}

/// <summary>피해 한 번의 정보. 모든 피해는 이 형태로 IDamageable.TakeDamage 에 전달한다</summary>
public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly DamageType Type;
    public readonly GameObject Source;

    public DamageInfo(int amount, DamageType type = DamageType.Attack, GameObject source = null)
    {
        Amount = Mathf.Max(0, amount);
        Type = type;
        Source = source;
    }

    public static DamageInfo Attack(int amount, GameObject source) => new DamageInfo(amount, DamageType.Attack, source);
    public static DamageInfo Trap(int amount, GameObject source = null) => new DamageInfo(amount, DamageType.Trap, source);
}

/// <summary>피해 처리 결과</summary>
public readonly struct DamageResult
{
    /// <summary>요청된 피해량</summary>
    public readonly int Requested;

    /// <summary>방어막 등이 흡수한 양</summary>
    public readonly int Absorbed;

    /// <summary>실제로 줄어든 체력</summary>
    public readonly int HealthLost;

    /// <summary>이 피해로 사망했는지</summary>
    public readonly bool Killed;

    public DamageResult(int requested, int absorbed, int healthLost, bool killed)
    {
        Requested = requested;
        Absorbed = absorbed;
        HealthLost = healthLost;
        Killed = killed;
    }

    public static DamageResult None => new DamageResult(0, 0, 0, false);
}

/// <summary>피해를 받을 수 있는 대상 (플레이어, 나중에 적도 구현 가능)</summary>
public interface IDamageable
{
    bool IsDead { get; }

    /// <summary>피해 처리 공통 함수</summary>
    DamageResult TakeDamage(DamageInfo info);
}

/// <summary>
/// 체력보다 먼저 피해를 흡수하는 것 (방어막 등).
/// PlayerHealth 와 같은 오브젝트에 이 인터페이스를 구현한 컴포넌트를 붙이거나
/// PlayerHealth.RegisterAbsorber 로 등록하면, 피해가 Order 오름차순으로 먼저 흡수되고 남은 피해만 체력에 적용된다.
/// </summary>
public interface IDamageAbsorber
{
    /// <summary>흡수 순서 (작을수록 먼저)</summary>
    int Order { get; }

    /// <summary>들어온 피해 중 흡수한 양을 반환한다 (0 ~ amount)</summary>
    int Absorb(int amount, DamageInfo info);
}
