using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 덱 / 버린 카드 더미 표시. 이미지는 Icon 의 Source Image 에 직접 넣으면 된다.
/// 장수는 DeckManager 가 SetCount 로 갱신한다.
/// 클릭하면 Clicked 가 발생한다 (CardPileViewer 가 목록 창을 연다). Image 의 Raycast Target 이 켜져 있어야 한다.
/// </summary>
public class CardPileView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("더미 이미지 (원하는 스프라이트를 Source Image 에 넣기)")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private string _format = "{0}";

    [Header("비었을 때")]
    [SerializeField] private bool _dimWhenEmpty = true;
    [Range(0f, 1f)][SerializeField] private float _emptyAlpha = 0.4f;

    [Header("마우스 올렸을 때")]
    [SerializeField] private float _hoverScale = 1.06f;

    private Color _baseColor = Color.white;
    private bool _hasBaseColor;
    private bool _hovered;

    /// <summary>더미를 클릭했을 때</summary>
    public event Action Clicked;

    public void SetCount(int count)
    {
        if (_countText != null) _countText.text = string.Format(_format, count);

        if (_icon != null)
        {
            if (!_hasBaseColor)
            {
                _baseColor = _icon.color;
                _hasBaseColor = true;
            }

            Color c = _baseColor;
            if (_dimWhenEmpty && count <= 0) c.a *= _emptyAlpha;
            _icon.color = c;
        }
    }

    private void Update()
    {
        float s = _hovered ? _hoverScale : 1f;
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(s, s, 1f), k);
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    private void OnDisable() => _hovered = false;
}
