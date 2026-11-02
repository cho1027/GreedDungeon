using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 덱 / 버린 카드 목록 창.
/// - 오른쪽 아래 덱 더미 또는 버린 카드 더미를 클릭하면 열린다. (닫기 버튼, ESC 로 닫기)
/// - 카드 목록은 힙 정렬(HeapSort)로 정렬해서 보여준다. 정렬 버튼으로 코스트순 / 이름순을 바꾼다.
///   (덱은 정렬해서 보여주므로 실제로 뽑힐 순서는 드러나지 않는다)
/// - 창이 열려 있는 동안 더미 장수가 바뀌면 바로 다시 그린다.
/// </summary>
public class CardPileViewer : MonoBehaviour
{
    public enum PileType { Draw, Discard }
    public enum SortMode { Cost, Name }

    [Header("연결")]
    [SerializeField] private DeckManager _deck;
    [SerializeField] private CardPileView _drawPileButton;
    [SerializeField] private CardPileView _discardPileButton;

    [Header("창")]
    [Tooltip("열고 닫을 창 (평소에는 꺼 둔다)")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _sortButton;
    [SerializeField] private TMP_Text _sortButtonText;
    [SerializeField] private ScrollRect _scroll;
    [Tooltip("카드가 배치될 부모 (스크롤 내용)")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private CardDisplay _cardPrefab;

    [Header("배치")]
    [Min(1)][SerializeField] private int _columns = 5;
    [SerializeField] private Vector2 _cellSize = new Vector2(170f, 240f);
    [SerializeField] private Vector2 _spacing = new Vector2(24f, 24f);
    [SerializeField] private float _padding = 20f;

    [Header("문구")]
    [SerializeField] private string _drawTitle = "덱";
    [SerializeField] private string _discardTitle = "버린 카드";
    [Tooltip("{0} = 더미 이름, {1} = 장수")]
    [SerializeField] private string _titleFormat = "{0} · {1}장";
    [SerializeField] private string _sortCostText = "정렬: 코스트";
    [SerializeField] private string _sortNameText = "정렬: 이름";

    [Header("정렬")]
    [SerializeField] private SortMode _sortMode = SortMode.Cost;

    private readonly List<CardDisplay> _pool = new List<CardDisplay>();
    private readonly List<CardData> _cards = new List<CardData>();
    private PileType _current;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    // ───────────────────────── 연결 ─────────────────────────

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_scroll == null && _panel != null) _scroll = _panel.GetComponentInChildren<ScrollRect>(true);
    }

    private void OnEnable()
    {
        if (_drawPileButton != null) _drawPileButton.Clicked += OpenDrawPile;
        if (_discardPileButton != null) _discardPileButton.Clicked += OpenDiscardPile;
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_sortButton != null) _sortButton.onClick.AddListener(ToggleSort);
        if (_deck != null) _deck.PilesChanged += OnPilesChanged;
    }

    private void OnDisable()
    {
        if (_drawPileButton != null) _drawPileButton.Clicked -= OpenDrawPile;
        if (_discardPileButton != null) _discardPileButton.Clicked -= OpenDiscardPile;
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_sortButton != null) _sortButton.onClick.RemoveListener(ToggleSort);
        if (_deck != null) _deck.PilesChanged -= OnPilesChanged;
    }

    private void Update()
    {
        if (!IsOpen) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
    }

    // ───────────────────────── 열기 / 닫기 ─────────────────────────

    public void OpenDrawPile() => Open(PileType.Draw);
    public void OpenDiscardPile() => Open(PileType.Discard);

    public void Open(PileType pile)
    {
        if (_panel == null) return;

        // 같은 더미를 다시 누르면 닫기
        if (IsOpen && _current == pile)
        {
            Close();
            return;
        }

        _current = pile;
        _panel.SetActive(true);
        Rebuild();

        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f; // 맨 위부터
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    public void ToggleSort()
    {
        _sortMode = _sortMode == SortMode.Cost ? SortMode.Name : SortMode.Cost;
        if (IsOpen) Rebuild();
    }

    private void OnPilesChanged()
    {
        if (IsOpen) Rebuild();
    }

    // ───────────────────────── 그리기 ─────────────────────────

    private void Rebuild()
    {
        _cards.Clear();
        if (_deck != null)
        {
            if (_current == PileType.Draw) _deck.CopyDrawPile(_cards);
            else _deck.CopyDiscardPile(_cards);
        }

        // 힙 정렬
        HeapSort.Sort(_cards, _sortMode == SortMode.Cost ? (Comparison<CardData>)CompareByCost : CompareByName);

        if (_titleText != null)
        {
            string pileName = _current == PileType.Draw ? _drawTitle : _discardTitle;
            _titleText.text = string.Format(_titleFormat, pileName, _cards.Count);
        }

        if (_sortButtonText != null)
            _sortButtonText.text = _sortMode == SortMode.Cost ? _sortCostText : _sortNameText;

        Layout();
    }

    private void Layout()
    {
        if (_content == null || _cardPrefab == null) return;

        int columns = Mathf.Max(1, _columns);
        int rows = Mathf.CeilToInt(_cards.Count / (float)columns);

        // 내용 크기 (위쪽 가운데 기준)
        _content.anchorMin = new Vector2(0.5f, 1f);
        _content.anchorMax = new Vector2(0.5f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        float width = columns * _cellSize.x + (columns - 1) * _spacing.x + _padding * 2f;
        float height = rows > 0 ? rows * _cellSize.y + (rows - 1) * _spacing.y + _padding * 2f : 0f;
        _content.sizeDelta = new Vector2(width, height);
        _content.anchoredPosition = new Vector2(0f, _content.anchoredPosition.y);

        // 필요한 만큼만 만들고 남는 것은 숨긴다
        while (_pool.Count < _cards.Count)
            _pool.Add(Instantiate(_cardPrefab, _content));

        for (int i = 0; i < _pool.Count; i++)
        {
            CardDisplay card = _pool[i];
            bool used = i < _cards.Count;
            card.gameObject.SetActive(used);
            if (!used) continue;

            card.Show(_cards[i]);

            int col = i % columns;
            int row = i / columns;

            RectTransform rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(
                (col - (columns - 1) * 0.5f) * (_cellSize.x + _spacing.x),
                -_padding - row * (_cellSize.y + _spacing.y) - _cellSize.y * 0.5f);
        }
    }

    // ───────────────────────── 비교 (정렬 기준) ─────────────────────────

    /// <summary>코스트 오름차순 → 같으면 이름순 (빈 카드는 맨 뒤)</summary>
    private static int CompareByCost(CardData a, CardData b)
    {
        int nulls = CompareNulls(a, b);
        if (nulls != 0 || a == null) return nulls;

        int c = a.Cost.CompareTo(b.Cost);
        return c != 0 ? c : string.Compare(a.CardName, b.CardName, StringComparison.CurrentCulture);
    }

    /// <summary>이름 가나다순 → 같으면 코스트순 (빈 카드는 맨 뒤)</summary>
    private static int CompareByName(CardData a, CardData b)
    {
        int nulls = CompareNulls(a, b);
        if (nulls != 0 || a == null) return nulls;

        int c = string.Compare(a.CardName, b.CardName, StringComparison.CurrentCulture);
        return c != 0 ? c : a.Cost.CompareTo(b.Cost);
    }

    private static int CompareNulls(CardData a, CardData b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return 0;
    }
}
