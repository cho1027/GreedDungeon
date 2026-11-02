using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 체력 표시. PlayerHealth 의 값을 읽어서 보여주기만 하고, 체력 값을 따로 저장하지 않는다.
/// (UI 와 실제 체력이 어긋나지 않도록 항상 PlayerHealth 를 기준으로 갱신)
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

    private void OnEnable()
    {
        if (_health == null)
        {
            Debug.LogWarning("PlayerHealthUI: Health 가 연결되지 않았습니다.", this);
            return;
        }

        _health.HealthChanged += OnHealthChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= OnHealthChanged;
    }

    private void Start()
    {
        // PlayerHealth.Awake 보다 OnEnable 이 먼저 불린 경우를 대비해 한 번 더 갱신
        Refresh();
    }

    private void OnHealthChanged(int current, int max)
    {
        Apply(current, max);
    }

    public void Refresh()
    {
        if (_health == null) return;
        Apply(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Apply(int current, int max)
    {
        if (_text != null) _text.text = string.Format(_format, current, max);
        if (_fill != null) _fill.fillAmount = max > 0 ? (float)current / max : 0f;
    }
}
