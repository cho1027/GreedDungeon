using TMPro;
using UnityEngine;

/// <summary>
/// (선택) 적의 체력과 방어막 수치 표시. EnemyHealth / DamageShield 값을 읽어서 보여주기만 한다.
/// 몬스터 프리팹의 자식(발밑 등)에 월드 공간 텍스트(TextMeshPro)로 붙인다.
/// </summary>
public class EnemyStatusView : MonoBehaviour
{
    [SerializeField] private EnemyHealth _health;
    [SerializeField] private DamageShield _shield;

    [Tooltip("체력 표시 (예: 20/20)")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private string _healthFormat = "{0}/{1}";

    [Tooltip("방어막 수치 표시. 방어막이 0이면 숨긴다")]
    [SerializeField] private TMP_Text _shieldText;
    [Tooltip("방어막 아이콘 등, 방어막이 있을 때만 켤 오브젝트 (선택)")]
    [SerializeField] private GameObject _shieldRoot;

    private void Awake()
    {
        if (_health == null) _health = GetComponentInParent<EnemyHealth>();
        if (_shield == null) _shield = GetComponentInParent<DamageShield>();
    }

    private void OnEnable()
    {
        if (_health != null) _health.HealthChanged += OnHealthChanged;
        if (_shield != null) _shield.Changed += OnShieldChanged;
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= OnHealthChanged;
        if (_shield != null) _shield.Changed -= OnShieldChanged;
    }

    private void Start()
    {
        if (_health != null) OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        OnShieldChanged(_shield != null ? _shield.Amount : 0);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (_healthText != null) _healthText.text = string.Format(_healthFormat, current, max);
    }

    private void OnShieldChanged(int amount)
    {
        bool show = amount > 0;
        if (_shieldText != null)
        {
            _shieldText.text = show ? amount.ToString() : string.Empty;
            _shieldText.enabled = show;
        }
        if (_shieldRoot != null) _shieldRoot.SetActive(show);
    }
}
