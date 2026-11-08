using System.Collections.Generic;
using UnityEngine;

public class RoomEconomy : MonoBehaviour
{
    [SerializeField] private BattleTurnManager _battle;
    [SerializeField] private List<CardData> _pool = new List<CardData>();
    [Min(0)][SerializeField] private int _rewardGold = 25;
    [Min(0)][SerializeField] private int _cardPrice = 30;
    [Min(0)][SerializeField] private int _removePrice = 20;

    private sealed class Shop
    {
        public List<CardData> Cards;
        public bool RemovedCard;
    }

    private readonly Dictionary<DungeonRoom, Shop> _shops = new Dictionary<DungeonRoom, Shop>();
    private readonly HashSet<DungeonRoom> _rewarded = new HashSet<DungeonRoom>();
    private List<CardData> _reward;
    private bool _bossReward;
    private bool _shopOpen;
    private bool _removing;
    private Vector2 _scroll;
    private Font _font;

    public bool BlocksAction => _reward != null || _shopOpen;

    private void Awake()
    {
        _battle.Economy = this;
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 16);
    }

    private void OnEnable() => _battle.CombatEnded += OnCombatEnded;
    private void OnDisable()
    {
        _battle.CombatEnded -= OnCombatEnded;
        _shopOpen = false;
        _removing = false;
    }

    private void OnDestroy()
    {
        if (_font != null) Destroy(_font);
    }

    private List<CardData> RollCards()
    {
        var candidates = new List<CardData>();
        foreach (CardData card in _pool)
            if (card != null && !candidates.Contains(card)) candidates.Add(card);
        var result = new List<CardData>();
        while (candidates.Count > 0 && result.Count < 3)
        {
            int i = Random.Range(0, candidates.Count);
            result.Add(candidates[i]);
            candidates.RemoveAt(i);
        }
        return result;
    }

    private void OnCombatEnded(DungeonRoom room)
    {
        if (!_rewarded.Add(room)) return;
        GameManager.Instance.AddGold(_rewardGold);
        _reward = RollCards();
        _bossReward = room.Data != null && room.Data.RoomType == RoomType.Boss;
    }

    private void ClaimReward(CardData card)
    {
        _reward = null;
        if (card != null) GameManager.Instance.AddCard(card);
        if (_bossReward) GameManager.Instance.NotifyBossDefeated();
    }

    private void OnGUI()
    {
        if (GameManager.Instance.IsRunOver) return;
        Font previous = GUI.skin.font;
        if (_font != null) GUI.skin.font = _font;
        try
        {
            if (_reward != null) DrawReward();
            else if (_battle.CurrentRoom != null && _battle.CurrentRoom.Data != null &&
                _battle.CurrentRoom.Data.RoomType == RoomType.Shop)
                DrawShop(_battle.CurrentRoom);
        }
        finally
        {
            GUI.skin.font = previous;
        }
    }

    private void DrawReward()
    {
        BeginPanel();
        GUILayout.Label($"전투 보상   +{_rewardGold} 골드");
        GUILayout.Label("카드 한 장을 선택하세요.");
        foreach (CardData card in _reward)
        {
            if (GUILayout.Button(CardLabel(card))) { ClaimReward(card); break; }
        }

        GUILayout.EndArea();
    }

    private void DrawShop(DungeonRoom room)
    {
        if (!_shopOpen)
        {
            if (GUI.Button(new Rect(16, 70, 150, 36), "상점 열기")) _shopOpen = true;
            return;
        }

        if (!_shops.TryGetValue(room, out Shop shop))
        {
            shop = new Shop { Cards = RollCards() };
            _shops.Add(room, shop);
        }

        GameManager game = GameManager.Instance;
        BeginPanel();
        GUILayout.Label($"상점   보유 골드 {game.Gold}");
        if (_removing)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(245));
            var deck = new List<CardData>(game.Deck);
            foreach (CardData card in deck)
            {
                if (!GUILayout.Button(card.CardName)) continue;
                if (game.TrySpendGold(_removePrice))
                {
                    game.RemoveCard(card);
                    shop.RemovedCard = true;
                }
                _removing = false;
                break;
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("뒤로")) _removing = false;
        }
        else
        {
            for (int i = 0; i < shop.Cards.Count; i++)
            {
                CardData card = shop.Cards[i];
                GUI.enabled = game.CanAfford(_cardPrice);
                if (GUILayout.Button($"{_cardPrice} 골드  |  {CardLabel(card)}") && game.TrySpendGold(_cardPrice))
                {
                    shop.Cards.RemoveAt(i);
                    game.AddCard(card);
                    break;
                }
            }
            GUI.enabled = !shop.RemovedCard && game.Deck.Count > 0 && game.CanAfford(_removePrice);
            if (GUILayout.Button($"카드 제거 ({_removePrice} 골드)"))
            {
                _scroll = Vector2.zero;
                _removing = true;
            }
            GUI.enabled = true;
            if (GUILayout.Button("닫기")) _shopOpen = false;
        }
        GUILayout.EndArea();
    }

    private static string CardLabel(CardData card) => $"{card.CardName} ({card.Cost})\n{card.Description}";

    private static void BeginPanel()
    {
        float width = Mathf.Min(600, Screen.width - 24);
        GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 30, width, Screen.height - 60), GUI.skin.box);
    }
}
