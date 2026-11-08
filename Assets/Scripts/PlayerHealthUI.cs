using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 체력 표시. PlayerHealth 의 값을 읽어서 보여주기만 하고, 체력 값을 따로 저장하지 않는다.
/// (UI 와 실제 체력이 어긋나지 않도록 항상 PlayerHealth 를 기준으로 갱신)
///
/// 방어도(DamageShield)와 상태 이상(StatusEffectHolder)도 함께 표시한다.
///  - Block Text / Status Text 를 연결하면 거기에 따로 표시
///  - 연결하지 않으면 체력 텍스트 뒤에 붙여서 표시 (예: "20 / 20  방어 5  부패 2턴")
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth _health;

    [Tooltip("체력 숫자 표시 (예: 20 / 20)")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("선택: 체력 바. Image Type 을 Filled 로 설정한 Image")]
    [SerializeField] private Image _fill;

    [Tooltip("{0} = 현재 체력, {1} = 최대 체력")]
    [SerializeField] private string _format = "{0} / {1}";

    [Header("방어도 / 상태 이상 (선택)")]
    [Tooltip("방어도 표시. 비워두면 체력 텍스트 뒤에 붙인다. 방어도가 0이면 숨김")]
    [SerializeField] private TMP_Text _blockText;
    [Tooltip("방어도가 있을 때만 켤 오브젝트 (방패 아이콘 등)")]
    [SerializeField] private GameObject _blockRoot;
    [Tooltip("상태 이상 표시 (예: 부패 2턴 · 속박 1턴). 비워두면 체력 텍스트 뒤에 붙인다")]
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private string _blockFormat = "방어 {0}";
    [SerializeField] private Color _blockColor = new Color(0.37f, 0.71f, 1f);
    [SerializeField] private Color _statusColor = new Color(0.55f, 0.85f, 0.35f);

    private DamageShield _shield;
    private StatusEffectHolder _status;

    private void OnEnable()
    {
        if (_health == null)
        {
            Debug.LogWarning("PlayerHealthUI: Health 가 연결되지 않았습니다.", this);
            return;
        }

        _health.HealthChanged += OnHealthChanged;
        BindExtras();
        Refresh();
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= OnHealthChanged;
        UnbindExtras();
    }

    private void Start()
    {
        // PlayerHealth.Awake / BattleTurnManager.Awake 보다 OnEnable 이 먼저 불린 경우를 대비해 한 번 더
        BindExtras();
        Refresh();
    }

    private void Update()
    {
        // 방어막/상태 이상 컴포넌트가 나중에 붙는 경우 대비 (한 번 찾으면 더 이상 찾지 않는다)
        if ((_shield == null || _status == null) && _health != null) BindExtras();
    }

    private void BindExtras()
    {
        if (_health == null) return;

        if (_shield == null)
        {
            _shield = _health.GetComponent<DamageShield>();
            if (_shield != null)
            {
                _shield.Changed += OnShieldChanged;
                Refresh();
            }
        }

        if (_status == null)
        {
            _status = _health.GetComponent<StatusEffectHolder>();
            if (_status != null)
            {
                _status.Changed += Refresh;
                Refresh();
            }
        }
    }

    private void UnbindExtras()
    {
        if (_shield != null) _shield.Changed -= OnShieldChanged;
        if (_status != null) _status.Changed -= Refresh;
        _shield = null;
        _status = null;
    }

    private void OnHealthChanged(int current, int max) => Refresh();
    private void OnShieldChanged(int amount) => Refresh();

    public void Refresh()
    {
        if (_health == null) return;
        Apply(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Apply(int current, int max)
    {
        int block = _shield != null ? _shield.Amount : 0;
        string status = _status != null ? _status.Describe() : string.Empty;
        string blockLabel = block > 0 ? string.Format(_blockFormat, block) : string.Empty;

        string main = string.Format(_format, current, max);
        if (_blockText == null && block > 0)
            main += $"  <color=#{ColorUtility.ToHtmlStringRGB(_blockColor)}>{blockLabel}</color>";
        if (_statusText == null && status.Length > 0)
            main += $"  <color=#{ColorUtility.ToHtmlStringRGB(_statusColor)}>{status}</color>";

        if (_text != null) _text.text = main;
        if (_fill != null) _fill.fillAmount = max > 0 ? (float)current / max : 0f;

        if (_blockText != null)
        {
            _blockText.text = blockLabel;
            _blockText.enabled = block > 0;
        }
        if (_blockRoot != null) _blockRoot.SetActive(block > 0);

        if (_statusText != null)
        {
            _statusText.text = status;
            _statusText.enabled = status.Length > 0;
        }
    }
}
