using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 패에 놓이는 카드 UI 한 장.
/// - 드래그해서 옮길 수 있고, 패 영역 안에 놓으면 제자리로 돌아간다.
/// - 패 영역 밖에 놓으면 "사용": 살짝 커졌다가 사라진다. (능력은 아직 없음)
/// 카드 프리팹 조건: RectTransform(앵커/피벗 중앙) + Image(Raycast Target 켜짐) + CanvasGroup + 이 컴포넌트
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("표시 (연결 안 해도 동작함)")]
    [SerializeField] private Image _artImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private TMP_Text _descriptionText;

    [Header("패 안에서의 이동")]
    [Tooltip("목표 위치로 따라가는 속도 (클수록 빠름)")]
    [SerializeField] private float _followSpeed = 14f;

    [Header("사용 연출")]
    [SerializeField] private float _useScale = 1.25f;    // 커지는 배율
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

        if (data != null)
        {
            if (_artImage != null && data.Artwork != null) _artImage.sprite = data.Artwork;
            if (_nameText != null) _nameText.text = data.CardName;
            if (_costText != null) _costText.text = data.Cost.ToString();
            if (_descriptionText != null) _descriptionText.text = data.Description;
        }
    }

    /// <summary>패 정렬이 정해준 목표 위치/기울기 (부모 기준 로컬 좌표)</summary>
    public void SetTarget(Vector2 localPos, float rotationZ)
    {
        _targetPos = localPos;
        _targetRot = rotationZ;
    }

    private void Update()
    {
        if (IsDragging || _used) return;

        float k = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);

        Vector3 target = new Vector3(_targetPos.x, _targetPos.y, 0f);
        _rect.localPosition = Vector3.Lerp(_rect.localPosition, target, k);

        float z = Mathf.LerpAngle(_rect.localEulerAngles.z, _targetRot, k);
        _rect.localRotation = Quaternion.Euler(0f, 0f, z);
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
        _group.blocksRaycasts = true;
        _hand.OnCardDropped(this, eventData.position, eventData.pressEventCamera);
    }

    // ───────────────────────── 사용 연출 ─────────────────────────

    /// <summary>
    /// 살짝 커졌다가 사라진다. 가장 커진 순간 onUsed 가 호출된다 (나중에 카드 효과를 발동할 지점).
    /// 연출이 끝나면 오브젝트가 제거된다.
    /// </summary>
    public void PlayUse(Action onUsed)
    {
        if (_used) return;

        _used = true;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _rect.SetAsLastSibling();

        StartCoroutine(UseRoutine(onUsed));
    }

    private IEnumerator UseRoutine(Action onUsed)
    {
        Vector3 startScale = _rect.localScale;
        Vector3 peakScale = startScale * _useScale;

        // 1) 살짝 커진다
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

        // 2) 사용 (효과 발동 지점)
        onUsed?.Invoke();

        if (_holdTime > 0f)
            yield return new WaitForSeconds(_holdTime);

        // 3) 사라진다
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