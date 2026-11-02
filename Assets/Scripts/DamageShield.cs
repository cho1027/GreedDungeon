using System;
using UnityEngine;

/// <summary>
/// 방어막. 체력과 별개로 관리되며, 피해가 들어오면 체력보다 먼저 차감된다 (IDamageAbsorber).
/// EnemyHealth / PlayerHealth 와 같은 오브젝트에 붙이면 자동으로 피해 처리에 연결된다.
/// 방어막을 언제 없앨지는 소유자가 정한다 (기본 적: 자기 다음 턴 시작 시 Clear).
/// </summary>
[DisallowMultipleComponent]
public class DamageShield : MonoBehaviour, IDamageAbsorber
{
    [Tooltip("흡수 순서 (작을수록 먼저). 방어막 종류가 여러 개일 때만 신경 쓰면 된다")]
    [SerializeField] private int _order = 0;

    public int Amount { get; private set; }
    public int Order => _order;

    /// <summary>방어막 수치가 바뀌었을 때 (현재 수치)</summary>
    public event Action<int> Changed;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Amount += amount;
        Changed?.Invoke(Amount);
    }

    /// <summary>남은 방어막을 모두 제거</summary>
    public void Clear()
    {
        if (Amount == 0) return;
        Amount = 0;
        Changed?.Invoke(Amount);
    }

    public int Absorb(int amount, DamageInfo info)
    {
        if (amount <= 0 || Amount <= 0) return 0;

        int taken = Mathf.Min(amount, Amount);
        Amount -= taken;
        Changed?.Invoke(Amount);
        return taken;
    }
}
