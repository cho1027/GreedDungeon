using UnityEngine;

/// <summary>
/// 패에서 카드가 사용되면(HandUI.CardUsed) 그 카드의 효과(CardData.Effects)를 순서대로 실행한다.
/// 코스트 차감은 BattleTurnManager 가 이미 처리하므로 여기서는 효과만 담당한다.
/// 씬의 CardSystem 오브젝트에 붙어 있다.
/// </summary>
public class CardEffectRunner : MonoBehaviour
{
    [SerializeField] private BattleTurnManager _battle;
    [SerializeField] private HandUI _hand;
    [Tooltip("비워두면 Battle Turn Manager 의 Player 를 사용")]
    [SerializeField] private GridPlayer _player;

    private void OnEnable()
    {
        if (_hand != null) _hand.CardUsed += OnCardUsed;
        else Debug.LogWarning("CardEffectRunner: Hand 가 연결되지 않았습니다.", this);
    }

    private void OnDisable()
    {
        if (_hand != null) _hand.CardUsed -= OnCardUsed;
    }

    private void OnCardUsed(CardData card)
    {
        if (card == null) return;

        GridPlayer player = _player != null ? _player : (_battle != null ? _battle.Player : null);
        DungeonRoom room = _battle != null ? _battle.CurrentRoom : null;
        var context = new CardContext(card, _battle, player, room);

        foreach (CardEffect effect in card.Effects)
        {
            if (effect != null) effect.Apply(context);
        }
    }
}
