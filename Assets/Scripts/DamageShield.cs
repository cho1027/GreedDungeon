using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방어막(방어도). 체력과 별개로 관리되며, 피해가 들어오면 체력보다 먼저 차감된다 (IDamageAbsorber).
/// EnemyHealth / PlayerHealth 와 같은 오브젝트에 붙이면 자동으로 피해 처리에 연결된다.
///
/// 방어막은 "층" 단위로 쌓이고, 층마다 유지 턴이 있다.
///  - Add(양, 턴): 턴 = TickTurn() 이 몇 번 불리면 사라지는지. 0 이하 = 사라지지 않음(계속 쌓임)
///  - 피해는 곧 사라질 층부터 깎는다 (영구 방어막은 마지막에)
///  - 소유자가 자기 턴 시작에 TickTurn() 을 부른다 (적: 자기 턴 시작 / 플레이어: 플레이어 턴 시작)
/// </summary>
[DisallowMultipleComponent]
public class DamageShield : MonoBehaviour, IDamageAbsorber
{
    [Tooltip("흡수 순서 (작을수록 먼저). 방어막 종류가 여러 개일 때만 신경 쓰면 된다")]
    [SerializeField] private int _order = 0;

    [Tooltip("상태 이상(부패, 부패 지대 등) 피해도 막는다. 끄면 상태 이상 피해는 방어도를 무시하고 체력에 바로 들어간다")]
    [SerializeField] private bool _blocksStatusDamage = false;

    private class Layer
    {
        public int Amount;
        public int Turns; // 0 이하 = 영구
    }

    private readonly List<Layer> _layers = new List<Layer>();

    public int Amount { get; private set; }
    public int Order => _order;

    /// <summary>방어막 수치가 바뀌었을 때 (현재 수치)</summary>
    public event Action<int> Changed;

    /// <summary>방어막 추가. turns = 유지 턴 수 (기본 1 = 다음 TickTurn 에 사라짐), 0 이하 = 영구</summary>
    public void Add(int amount, int turns = 1)
    {
        if (amount <= 0) return;
        _layers.Add(new Layer { Amount = amount, Turns = turns });
        Recount();
    }

    /// <summary>남은 방어막을 모두 제거</summary>
    public void Clear()
    {
        if (_layers.Count == 0 && Amount == 0) return;
        _layers.Clear();
        Recount();
    }

    /// <summary>턴 경과: 기한이 있는 층의 남은 턴을 줄이고 끝난 층을 지운다</summary>
    public void TickTurn()
    {
        bool changed = false;
        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            Layer layer = _layers[i];
            if (layer.Turns <= 0) continue; // 영구

            layer.Turns--;
            if (layer.Turns <= 0)
            {
                _layers.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) Recount();
    }

    public int Absorb(int amount, DamageInfo info)
    {
        if (amount <= 0 || Amount <= 0) return 0;
        if (!_blocksStatusDamage && info.Type == DamageType.Status) return 0;

        // 곧 사라질 층부터 (영구 층은 맨 뒤)
        _layers.Sort((a, b) => SortKey(a).CompareTo(SortKey(b)));

        int remaining = amount;
        for (int i = 0; i < _layers.Count && remaining > 0; i++)
        {
            int taken = Mathf.Min(remaining, _layers[i].Amount);
            _layers[i].Amount -= taken;
            remaining -= taken;
        }
        _layers.RemoveAll(l => l.Amount <= 0);

        Recount();
        return amount - remaining;
    }

    private static int SortKey(Layer l) => l.Turns <= 0 ? int.MaxValue : l.Turns;

    private void Recount()
    {
        int total = 0;
        foreach (Layer l in _layers) total += l.Amount;
        if (total == Amount) return;
        Amount = total;
        Changed?.Invoke(Amount);
    }
}
