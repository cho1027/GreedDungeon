using UnityEngine;

/// <summary>
/// 방어 카드 효과: 플레이어가 방어도를 얻는다.
/// 방어도는 적 공격 피해를 먼저 막고, 다음 플레이어 턴이 시작될 때 사라진다. (부패 등 상태 이상 피해는 막지 못한다)
///  - "방어" 카드: Block = 5
/// 만들기: Project 우클릭 → Create → GridDungeon → Card Effect → Defend
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Card Effect/Defend", fileName = "DefendCardEffect")]
public class DefendCardEffect : CardEffect
{
    [Min(0)][SerializeField] private int _block = 5;

    public int Block => _block;

    public override void Apply(CardContext context)
    {
        DamageShield shield = context.Manager != null ? context.Manager.PlayerShield : null;
        if (shield == null && context.Player != null) shield = context.Player.GetComponent<DamageShield>();

        if (shield == null)
        {
            Debug.LogWarning($"[Card] {name}: 플레이어 방어막(DamageShield)을 찾지 못했습니다.");
            return;
        }

        shield.Add(_block, 1);
        Debug.Log($"[Card] {name}: 방어도 +{_block} → {shield.Amount}");
    }
}
