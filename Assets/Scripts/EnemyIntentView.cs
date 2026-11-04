using TMPro;
using UnityEngine;

/// <summary>
/// 적 머리 위의 다음 행동 표시 (아이콘 + 수치).
/// 아이콘은 각 행동 에셋(EnemyAction)의 Icon 을 그대로 쓴다 → 공격 종류마다 다른 이미지를 연결하면 구분된다.
/// 몬스터 프리팹의 자식 오브젝트(머리 위 위치)에 붙인다.
/// </summary>
public class EnemyIntentView : MonoBehaviour
{
    [Tooltip("숨길 때 끌 오브젝트 (아이콘과 수치의 부모). 비워두면 아이콘·수치만 각각 끈다. 몬스터 본체를 넣지 마세요")]
    [SerializeField] private GameObject _root;
    [SerializeField] private SpriteRenderer _icon;
    [Tooltip("수치 표시. 월드 공간 텍스트(TextMeshPro) 사용")]
    [SerializeField] private TMP_Text _valueText;

    private void Awake()
    {
        Hide();
    }

    public void Show(EnemyIntent intent)
    {
        if (intent == null || intent.Action == null)
        {
            Hide();
            return;
        }

        if (_root != null) _root.SetActive(true);

        if (_icon != null)
        {
            _icon.sprite = intent.Action.Icon;
            _icon.enabled = intent.Action.Icon != null; // 이미지가 연결되지 않았으면 아이콘을 띄우지 않는다
        }

        if (_valueText != null)
        {
            bool hasLabel = !string.IsNullOrEmpty(intent.ValueLabel);
            bool show = intent.Action.ShowValue && (hasLabel || intent.Value > 0);
            _valueText.text = !show ? string.Empty : (hasLabel ? intent.ValueLabel : intent.Value.ToString());
            _valueText.enabled = show;
        }
    }

    public void Hide()
    {
        if (_root != null)
        {
            _root.SetActive(false);
            return;
        }

        if (_icon != null) _icon.enabled = false;
        if (_valueText != null) _valueText.enabled = false;
    }
}
