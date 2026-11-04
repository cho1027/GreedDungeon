using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>덱 구성 한 줄: 어떤 카드를 몇 장</summary>
[Serializable]
public class DeckEntry
{
    public CardData card;
    [Min(1)] public int count = 1;
}

/// <summary>
/// 덱(뽑을 카드 더미)과 버린 카드 더미를 관리한다. 두 더미 모두 Stack 으로 구현한다.
/// - 덱: 섞은 뒤 Push 로 쌓고, 뽑을 때는 맨 위에서 Pop (후입선출)
/// - 버린 카드: 버릴 때 Push, 다시 섞을 때 전부 꺼내 섞은 뒤 덱에 Push
/// - 플레이어 턴 시작: 덱에서 Draw Per Turn 장 뽑아 패에 추가 (덱이 비면 버린 카드를 섞어 덱으로)
/// - 카드 사용: 버린 카드 더미로
/// - 턴 종료: 남은 패를 전부 버린 카드 더미로
/// - 전투 시작: GameManager 의 덱(판 전체에서 유지되는 덱)으로 다시 만들고 섞음
///   → 보상/상점에서 GameManager.AddCard / RemoveCard 한 결과가 다음 전투부터 반영된다
/// - 전투 종료: 패와 버린 카드를 모두 덱으로 되돌리고 섞음
/// 씬의 CardSystem 오브젝트에 붙어 있다.
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private BattleTurnManager _battle;
    [SerializeField] private HandUI _hand;

    [Header("덱")]
    [Tooltip("시작 덱 구성 (카드 + 장수)")]
    [SerializeField] private List<DeckEntry> _startingDeck = new List<DeckEntry>();
    [Min(0)][SerializeField] private int _drawPerTurn = 5;

    [Header("UI (오른쪽 덱 / 버린 카드)")]
    [SerializeField] private CardPileView _drawPileView;
    [SerializeField] private CardPileView _discardPileView;

    private readonly Stack<CardData> _drawPile = new Stack<CardData>();
    private readonly Stack<CardData> _discardPile = new Stack<CardData>();
    private readonly List<CardData> _handCards = new List<CardData>();

    public int DrawPileCount => _drawPile.Count;
    public int DiscardPileCount => _discardPile.Count;

    /// <summary>덱 맨 위 카드 (비었으면 null). 뽑지 않고 확인만 한다</summary>
    public CardData PeekDrawPile() => _drawPile.Count > 0 ? _drawPile.Peek() : null;

    /// <summary>버린 카드 더미 맨 위 카드 (마지막으로 버린 카드, 비었으면 null)</summary>
    public CardData PeekDiscardPile() => _discardPile.Count > 0 ? _discardPile.Peek() : null;

    /// <summary>덱의 카드를 result 에 복사한다 (덱은 그대로, 목록 창에서 쓴다)</summary>
    public void CopyDrawPile(List<CardData> result)
    {
        result.AddRange(_drawPile);
    }

    /// <summary>버린 카드를 result 에 복사한다 (더미는 그대로)</summary>
    public void CopyDiscardPile(List<CardData> result)
    {
        result.AddRange(_discardPile);
    }

    /// <summary>덱 / 버린 카드 장수가 바뀌었을 때</summary>
    public event Action PilesChanged;

    private void Awake()
    {
        BuildStartingDeck();
    }

    private void OnEnable()
    {
        if (_battle != null)
        {
            _battle.CombatStarted += OnCombatStarted;
            _battle.PlayerTurnStarted += OnPlayerTurnStarted;
            _battle.PlayerTurnEnded += OnPlayerTurnEnded;
            _battle.CombatEnded += OnCombatEnded;
        }
        else Debug.LogWarning("DeckManager: Battle Turn Manager 가 연결되지 않았습니다.", this);

        if (_hand != null)
        {
            _hand.CardAdded += OnCardAdded;
            _hand.CardUseStarted += OnCardUseStarted;
        }
        else Debug.LogWarning("DeckManager: Hand 가 연결되지 않았습니다.", this);

        RefreshUI();
    }

    private void OnDisable()
    {
        if (_battle != null)
        {
            _battle.CombatStarted -= OnCombatStarted;
            _battle.PlayerTurnStarted -= OnPlayerTurnStarted;
            _battle.PlayerTurnEnded -= OnPlayerTurnEnded;
            _battle.CombatEnded -= OnCombatEnded;
        }

        if (_hand != null)
        {
            _hand.CardAdded -= OnCardAdded;
            _hand.CardUseStarted -= OnCardUseStarted;
        }
    }

    // ───────────────────────── 덱 구성 ─────────────────────────

    private void BuildStartingDeck()
    {
        // GameManager 에 덱이 없으면 여기 시작 덱을 넣어 둔다 (처음 시작할 때만)
        GameManager.Instance.SeedDeckIfEmpty(_startingDeck);
        RebuildFromMasterDeck();
    }

    /// <summary>패, 덱, 버린 카드를 비우고 GameManager 의 덱으로 다시 만든다</summary>
    public void RebuildFromMasterDeck()
    {
        _drawPile.Clear();
        _discardPile.Clear();
        _handCards.Clear();
        if (_hand != null) _hand.ClearHand();

        var cards = new List<CardData>(GameManager.Instance.Deck);
        PushShuffled(cards);
        RefreshUI();
    }

    private void OnCombatStarted(DungeonRoom room)
    {
        // 전투마다 판 전체 덱에서 새로 시작 (보상으로 얻은 카드 반영, 소멸된 카드 복구)
        RebuildFromMasterDeck();
    }

    // ───────────────────────── 턴 / 전투 ─────────────────────────

    private void OnPlayerTurnStarted()
    {
        Draw(_drawPerTurn);
    }

    private void OnPlayerTurnEnded()
    {
        DiscardHand();
    }

    private void OnCombatEnded(DungeonRoom room)
    {
        // 남은 패, 버린 카드를 전부 덱으로 되돌려 다음 전투 준비
        DiscardHand();

        var cards = new List<CardData>(_drawPile);
        cards.AddRange(_discardPile);
        _drawPile.Clear();
        _discardPile.Clear();

        PushShuffled(cards);
        RefreshUI();
    }

    // ───────────────────────── 뽑기 / 버리기 ─────────────────────────

    /// <summary>덱에서 count 장 뽑아 패에 추가한다. 덱이 비면 버린 카드를 섞어 덱으로 만든다</summary>
    public void Draw(int count)
    {
        if (_hand == null) return;

        for (int i = 0; i < count; i++)
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) break; // 뽑을 카드가 전혀 없음
                ReshuffleDiscardIntoDraw();
            }

            CardData card = _drawPile.Pop(); // 맨 위에서 한 장

            // 패가 가득 차서 못 받으면 바로 버린 카드로
            if (_hand.AddCard(card) == null)
                _discardPile.Push(card);
        }

        RefreshUI();
    }

    /// <summary>남은 패를 전부 버린 카드 더미로 보낸다</summary>
    public void DiscardHand()
    {
        foreach (CardData card in _handCards) _discardPile.Push(card);
        _handCards.Clear();
        if (_hand != null) _hand.ClearHand();
        RefreshUI();
    }

    private void OnCardAdded(CardData card)
    {
        if (card != null) _handCards.Add(card);
    }

    private void OnCardUseStarted(CardData card)
    {
        if (card == null) return;
        _handCards.Remove(card);
        _discardPile.Push(card);
        RefreshUI();
    }

    // ───────────────────────── 기타 ─────────────────────────

    /// <summary>버린 카드 더미를 전부 꺼내 섞은 뒤 덱에 쌓는다</summary>
    private void ReshuffleDiscardIntoDraw()
    {
        var cards = new List<CardData>(_discardPile.Count);
        while (_discardPile.Count > 0) cards.Add(_discardPile.Pop());
        PushShuffled(cards);
    }

    /// <summary>카드 목록을 섞어서 덱(Stack)에 차례로 Push 한다</summary>
    private void PushShuffled(List<CardData> cards)
    {
        Shuffle(cards);
        foreach (CardData card in cards) _drawPile.Push(card);
    }

    private static void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void RefreshUI()
    {
        if (_drawPileView != null) _drawPileView.SetCount(_drawPile.Count);
        if (_discardPileView != null) _discardPileView.SetCount(_discardPile.Count);
        PilesChanged?.Invoke();
    }
}
