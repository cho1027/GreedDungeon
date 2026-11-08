using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 손패 UI. 카드를 추가하고, 부채꼴로 정렬하고, 손 영역 밖으로 끌어낸 카드를 "사용" 처리한다.
/// 이 컴포넌트는 화면 아래의 "손 영역" 오브젝트(RectTransform, 피벗 중앙)에 붙인다.
/// 카드는 이 오브젝트의 자식으로 생성된다.
/// </summary>
public class HandUI : MonoBehaviour
{
    [SerializeField] private CardView _cardPrefab;

    [Tooltip("이 영역 안에 카드를 놓으면 패로 돌아가고, 밖에 놓으면 사용된다. 비워두면 이 오브젝트를 사용")]
    [SerializeField] private RectTransform _handArea;

    [SerializeField] private int _maxCards = 10;

    [Header("정렬")]
    [SerializeField] private float _cardSpacing = 140f;   // 카드 사이 간격 (카드가 많으면 자동으로 좁혀짐)
    [SerializeField] private float _arcHeight = 30f;      // 가운데 카드가 양끝보다 높은 정도
    [SerializeField] private float _maxFanAngle = 8f;     // 양끝 카드의 기울기
    [SerializeField] private float _yOffset = 0f;         // 전체를 위/아래로 이동
    [Tooltip("새 카드가 날아오기 시작하는 위치 (손 영역 중심 기준)")]
    [SerializeField] private Vector2 _spawnOffset = new Vector2(700f, -150f);

    [Header("테스트")]
    [SerializeField] private bool _debugKeyEnabled = true; // C 키로 카드 추가
    [SerializeField] private List<CardData> _testCards = new List<CardData>();

    private readonly List<CardView> _cards = new List<CardView>();
    private RectTransform _container;

    public int Count => _cards.Count;

    /// <summary>카드가 패에 추가됐을 때</summary>
    public event Action<CardData> CardAdded;

    /// <summary>
    /// 카드 사용이 확정된 순간 (사용 연출 시작 직전, 같은 프레임).
    /// 코스트 차감처럼 즉시 반영해야 하는 처리는 여기서 한다.
    /// </summary>
    public event Action<CardData> CardUseStarted;

    /// <summary>코스트 차감 후 카드 효과를 실행한다</summary>
    public event Action<CardData> CardUsed;

    /// <summary>
    /// 카드 사용 가능 여부 (코스트, 턴 확인 등). null 이면 항상 사용 가능.
    /// false 를 반환하면 카드는 사용되지 않고 패로 돌아간다.
    /// </summary>
    public Func<CardData, bool> CanUseCard;

    public Func<CardData, Vector2, bool> CanTargetCard;
    public Vector2 DropPosition { get; private set; }

    private void Awake()
    {
        _container = (RectTransform)transform;
        if (_handArea == null) _handArea = _container;
    }

    private void Update()
    {
        if (!_debugKeyEnabled) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            AddRandomCard();
#else
        if (Input.GetKeyDown(KeyCode.C))
            AddRandomCard();
#endif
    }

    // ───────────────────────── 추가 ─────────────────────────

    /// <summary>테스트 카드 목록에서 한 장 추가 (UI 버튼 OnClick 에 연결해도 됨)</summary>
    [ContextMenu("Add Test Card")]
    public void AddRandomCard()
    {
        CardData data = null;
        if (_testCards != null && _testCards.Count > 0)
            data = _testCards[UnityEngine.Random.Range(0, _testCards.Count)];

        AddCard(data);
    }

    /// <summary>카드 한 장을 패에 추가한다. data 가 null 이어도 빈 카드로 추가된다</summary>
    public CardView AddCard(CardData data)
    {
        if (_cardPrefab == null)
        {
            Debug.LogWarning("HandUI: Card Prefab 이 연결되지 않았습니다.", this);
            return null;
        }

        if (_cards.Count >= _maxCards) return null;

        CardView card = Instantiate(_cardPrefab, _container);
        card.Init(this, data, _spawnOffset);
        _cards.Add(card);

        Relayout();
        CardAdded?.Invoke(data);
        return card;
    }

    // ───────────────────────── 드롭 처리 (CardView 가 호출) ─────────────────────────

    public void OnCardDropped(CardView card, Vector2 screenPos, Camera eventCamera)
    {
        bool insideHand = RectTransformUtility.RectangleContainsScreenPoint(_handArea, screenPos, eventCamera);

        // 손 안에 놓았거나 사용 조건이 안 되면 제자리로
        if (insideHand || !_cards.Contains(card) ||
            (CanUseCard != null && !CanUseCard(card.Data)) ||
            (CanTargetCard != null && !CanTargetCard(card.Data, screenPos)))
        {
            Relayout();
            return;
        }

        DropPosition = screenPos;
        UseCard(card);
    }

    private void UseCard(CardView card)
    {
        _cards.Remove(card);
        Relayout(); // 남은 카드들이 빈자리를 메운다

        CardData data = card.Data;

        // 사용 확정: 코스트 차감 등은 연출을 기다리지 않고 즉시 처리
        CardUseStarted?.Invoke(data);

        CardUsed?.Invoke(data);
        card.PlayUse(null);
    }

    // ───────────────────────── 정렬 ─────────────────────────

    private void Relayout()
    {
        int n = _cards.Count;
        if (n == 0) return;

        float cardWidth = ((RectTransform)_cardPrefab.transform).rect.width;
        float available = _container.rect.width - cardWidth;
        float spacing = n > 1 ? Mathf.Min(_cardSpacing, available / (n - 1)) : 0f;

        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f; // -1(왼쪽 끝) ~ 1(오른쪽 끝)

            float x = (i - (n - 1) * 0.5f) * spacing;
            float y = _yOffset + _arcHeight * (1f - t * t);
            float rot = -t * _maxFanAngle;

            _cards[i].SetTarget(new Vector2(x, y), rot);
            _cards[i].transform.SetSiblingIndex(i);
        }

        // 드래그 중인 카드는 항상 맨 위
        for (int i = 0; i < n; i++)
        {
            if (_cards[i].IsDragging) _cards[i].transform.SetAsLastSibling();
        }
    }

    /// <summary>패를 전부 비운다 (사용 효과 없이 제거)</summary>
    public void RemoveUnretainedCards()
    {
        for (int i = _cards.Count - 1; i >= 0; i--)
        {
            CardData data = _cards[i].Data;
            if (data != null && data.Retain && !data.Ethereal) continue;
            Destroy(_cards[i].gameObject);
            _cards.RemoveAt(i);
        }
        Relayout();
    }

    public void ClearHand()
    {
        foreach (CardView card in _cards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        _cards.Clear();
    }
}
