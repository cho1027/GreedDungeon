using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 패에 보이는 카드 한 장.
/// - 드래그해서 옮길 수 있고, 손 영역 안에 놓으면 제자리로 돌아간다.
/// - 손 영역 밖에 놓으면 "사용": 살짝 커졌다가 사라진다 (효과는 CardEffectRunner 가 실행).
/// - 마우스를 올리면 살짝 커지며 위로 올라온다.
/// 카드 프리팹 구성: RectTransform(앵커/피벗 중앙) + Image(Raycast Target 켜기) + CanvasGroup + 이 컴포넌트
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                        IPointerEnterHandler, IPointerExitHandler
{
    [Header("표시 (연결 안 해도 동작함)")]
    [SerializeField] private Image _artImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private TMP_Text _descriptionText;

    [Header("손 안에서의 이동")]
    [Tooltip("목표 위치를 따라가는 속도 (클수록 빠름)")]
    [SerializeField] private float _followSpeed = 14f;

    [Header("마우스 올렸을 때")]
    [SerializeField] private float _hoverScale = 1.12f;
    [SerializeField] private float _hoverLift = 40f;

    [Header("사용 연출")]
    [SerializeField] private float _useScale = 1.25f;    // 커지는 정도
    [SerializeField] private float _scaleUpTime = 0.12f; // 커지는 시간
    [SerializeField] private float _holdTime = 0.05f;    // 커진 상태로 잠깐 유지
    [SerializeField] private float _fadeTime = 0.15f;    // 사라지는 시간

    private HandUI _hand;
    private RectTransform _rect;
    private RectTransform _parentRect;
    private CanvasGroup _group;

    private Vector2 _targetPos;
    private float _targetRot;
    private bool _used;
    private bool _hovered;

    public CardData Data { get; private set; }
    public bool IsDragging { get; private set; }

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
    }

    public void Init(HandUI hand, CardData data, Vector2 startLocalPos)
    {
        _hand = hand;
        Data = data;
        _parentRect = (RectTransform)transform.parent;

        _rect.localPosition = new Vector3(startLocalPos.x, startLocalPos.y, 0f);
        _targetPos = startLocalPos;

        if (data == null) return;

        if (_artImage != null && data.Artwork != null)
        {
            // 이미지가 있으면 원래 색으로 표시 (없으면 프리팹의 빈 칸 색 그대로)
            _artImage.sprite = data.Artwork;
            _artImage.color = Color.white;
            _artImage.preserveAspect = true;
        }

        if (_nameText != null) _nameText.text = data.CardName;
        if (_costText != null) _costText.text = data.Cost.ToString();
        if (_descriptionText != null) _descriptionText.text = data.Description;
    }

    /// <summary>손 정렬이 정해준 목표 위치/각도 (부모 기준 로컬 좌표)</summary>
    public void SetTarget(Vector2 localPos, float rotationZ)
    {
        _targetPos = localPos;
        _targetRot = rotationZ;
    }

    private void Update()
    {
        if (IsDragging || _used) return;

        float k = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);

        Vector2 pos = _targetPos + (_hovered ? new Vector2(0f, _hoverLift) : Vector2.zero);
        _rect.localPosition = Vector3.Lerp(_rect.localPosition, new Vector3(pos.x, pos.y, 0f), k);

        float targetRot = _hovered ? 0f : _targetRot;
        float z = Mathf.LerpAngle(_rect.localEulerAngles.z, targetRot, k);
        _rect.localRotation = Quaternion.Euler(0f, 0f, z);

        float s = _hovered ? _hoverScale : 1f;
        _rect.localScale = Vector3.Lerp(_rect.localScale, new Vector3(s, s, 1f), k);
    }

    // ───────────────────────── 마우스 올리기 ─────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_used) return;
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }

    // ───────────────────────── 드래그 ─────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_used) return;

        IsDragging = true;
        _group.blocksRaycasts = false;
        _rect.localRotation = Quaternion.identity;
        _rect.SetAsLastSibling(); // 다른 카드 위에 그려지도록
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            _rect.localPosition = new Vector3(local.x, local.y, 0f);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging) return;

        IsDragging = false;
        _hovered = false;
        _group.blocksRaycasts = true;
        _hand.OnCardDropped(this, eventData.position, eventData.pressEventCamera);
    }

    // ───────────────────────── 사용 연출 ─────────────────────────

    /// <summary>
    /// 살짝 커졌다가 사라진다. 가장 커진 순간 onUsed 가 호출된다 (카드 효과 발동 시점).
    /// 연출이 끝나면 오브젝트가 삭제된다.
    /// </summary>
    public void PlayUse(Action onUsed)
    {
        if (_used) return;

        _used = true;
        _hovered = false;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _rect.SetAsLastSibling();

        StartCoroutine(UseRoutine(onUsed));
    }

    private IEnumerator UseRoutine(Action onUsed)
    {
        Vector3 startScale = _rect.localScale;
        Vector3 peakScale = Vector3.one * _useScale;

        // 1) 살짝 커지기
        float t = 0f;
        while (t < _scaleUpTime)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / _scaleUpTime);
            float eased = 1f - (1f - x) * (1f - x); // ease-out
            _rect.localScale = Vector3.LerpUnclamped(startScale, peakScale, eased);
            yield return null;
        }
        _rect.localScale = peakScale;

        // 2) 사용 (효과 발동 시점)
        onUsed?.Invoke();

        if (_holdTime > 0f)
            yield return new WaitForSeconds(_holdTime);

        // 3) 사라지기
        t = 0f;
        while (t < _fadeTime)
        {
            t += Time.deltaTime;
            _group.alpha = 1f - Mathf.Clamp01(t / _fadeTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
