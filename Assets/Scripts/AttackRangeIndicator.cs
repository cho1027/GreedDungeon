using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 범위를 바닥 타일에 표시한다. EnemyController 가 확정한 칸 목록(EnemyIntent.Cells)을 그대로 받아 그린다.
/// 적마다 자기 표시 오브젝트를 따로 가지므로 여러 적의 범위가 섞이지 않는다.
/// 몬스터 프리팹(또는 자식)에 붙인다. 표시 오브젝트는 몬스터 크기의 영향을 받지 않도록 방 아래에 생성된다.
/// </summary>
public class AttackRangeIndicator : MonoBehaviour
{
    [Tooltip("범위 한 칸에 놓을 표시 프리팹 (SpriteRenderer). 바닥보다 위, 캐릭터보다 아래 Sorting Order 로 만든다")]
    [SerializeField] private SpriteRenderer _markerPrefab;

    [Tooltip("타일 한 칸을 채우는 비율")]
    [Range(0.3f, 1f)][SerializeField] private float _fill = 0.95f;

    private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
    private bool _warned;

    public void Show(RoomGrid grid, IReadOnlyList<Vector2Int> cells)
    {
        Hide();
        if (grid == null || cells == null) return;

        if (_markerPrefab == null)
        {
            if (!_warned) Debug.LogWarning($"{name}: Marker Prefab 이 연결되지 않아 공격 범위를 표시하지 않습니다.", this);
            _warned = true;
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            SpriteRenderer marker = GetMarker(i, grid.transform);
            marker.transform.position = grid.GridToWorld(cells[i]);
            grid.FitToCell(marker.transform, _fill);
            marker.gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        foreach (SpriteRenderer marker in _markers)
        {
            if (marker != null) marker.gameObject.SetActive(false);
        }
    }

    private SpriteRenderer GetMarker(int index, Transform parent)
    {
        while (_markers.Count <= index) _markers.Add(null);

        SpriteRenderer marker = _markers[index];
        if (marker == null)
        {
            marker = Instantiate(_markerPrefab, parent);
            marker.name = $"{name} AttackRange {index}";
            _markers[index] = marker;
        }
        else if (marker.transform.parent != parent)
        {
            marker.transform.SetParent(parent, true);
        }
        return marker;
    }

    private void OnDestroy()
    {
        // 적이 사라지면 표시도 같이 제거
        foreach (SpriteRenderer marker in _markers)
        {
            if (marker != null) Destroy(marker.gameObject);
        }
        _markers.Clear();
    }
}
