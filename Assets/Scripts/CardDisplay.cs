using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보기 전용 카드 (드래그·사용 불가). 덱 / 버린 카드 목록 창에서 쓴다.
/// 프리팹: Assets/prefab/card_preview.prefab
/// </summary>
public class CardDisplay : MonoBehaviour
{
    [SerializeField] private Image _artImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private TMP_Text _descriptionText;

    private Color _emptyArtColor;
    private bool _hasEmptyArtColor;

    public CardData Data { get; private set; }

    public void Show(CardData data)
    {
        Data = data;

        if (_artImage != null)
        {
            if (!_hasEmptyArtColor)
            {
                _emptyArtColor = _artImage.color;
                _hasEmptyArtColor = true;
            }

            bool hasArt = data != null && data.Artwork != null;
            _artImage.sprite = hasArt ? data.Artwork : null;
            _artImage.color = hasArt ? Color.white : _emptyArtColor;
            _artImage.preserveAspect = true;
        }

        if (_nameText != null) _nameText.text = data != null ? data.CardName : "";
        if (_costText != null) _costText.text = data != null ? data.Cost.ToString() : "";
        if (_descriptionText != null) _descriptionText.text = data != null ? data.Description : "";
    }
}
