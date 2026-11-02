using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오른쪽 위 미니맵.
/// - 던전이 생성되면(DungeonGenerator2D.Generated) Room Template 을 복제해 방마다 한 칸씩 만든다.
///   (던전이 매번 랜덤이라 방 칸만 실행 중에 만들고, 틀·템플릿·색은 전부 씬에서 직접 수정할 수 있다)
/// - 가 본 방은 밝게, 가 본 방과 붙어 있는 방은 흐리게, 나머지는 숨긴다.
/// - 보스·보물 등 특별한 방은 아이콘(없으면 색 점)으로 표시한다.
/// - 현재 방이 항상 가운데 오도록 따라간다.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Serializable]
    public class RoomIcon
    {
        public RoomType type;
        [Tooltip("비워두면 색 점으로 표시")]
        public Sprite sprite;
        public Color color = Color.white;
    }

    [Header("연결")]
    [SerializeField] private DungeonGenerator2D _generator;
    [SerializeField] private RoomTransitionController _roomController;

    [Header("UI")]
    [Tooltip("방 칸이 보이는 영역. 밖으로 나간 칸은 잘린다")]
    [SerializeField] private RectTransform _viewport;
    [Tooltip("방 칸들이 들어갈 부모. 현재 방이 가운데 오도록 이 오브젝트를 움직인다")]
    [SerializeField] private RectTransform _content;
    [Tooltip("방 한 칸 템플릿 (꺼 두면 복제해서 쓴다). 자식에 'Icon' 이라는 Image 가 있으면 방 종류 아이콘으로 쓴다")]
    [SerializeField] private Image _roomTemplate;

    [Header("배치")]
    [SerializeField] private Vector2 _cellSize = new Vector2(46f, 34f);
    [SerializeField] private Vector2 _gap = new Vector2(6f, 6f);
    [SerializeField] private bool _centerOnCurrent = true;
    [Min(0f)][SerializeField] private float _followSpeed = 10f;
    [Tooltip("가 본 방과 붙어 있는, 아직 안 가 본 방도 흐리게 보여준다")]
    [SerializeField] private bool _showAdjacentUnvisited = true;

    [Header("색")]
    [SerializeField] private Color _currentColor = new Color(0.98f, 0.9f, 0.68f, 1f);
    [SerializeField] private Color _clearedColor = new Color(0.46f, 0.43f, 0.56f, 1f);
    [Tooltip("들어갔지만 아직 클리어하지 못한 방 (전투 중 등)")]
    [SerializeField] private Color _unclearedColor = new Color(0.62f, 0.3f, 0.32f, 1f);
    [SerializeField] private Color _unknownColor = new Color(0.22f, 0.2f, 0.28f, 1f);
    [Range(0f, 1f)][SerializeField] private float _unknownIconAlpha = 0.6f;

    [Header("방 종류 아이콘")]
    [SerializeField]
    private List<RoomIcon> _icons = new List<RoomIcon>
    {
        new RoomIcon { type = RoomType.Boss, color = new Color(0.95f, 0.25f, 0.25f, 1f) },
        new RoomIcon { type = RoomType.Treasure, color = new Color(1f, 0.82f, 0.25f, 1f) },
        new RoomIcon { type = RoomType.Shop, color = new Color(0.35f, 0.85f, 0.45f, 1f) },
        new RoomIcon { type = RoomType.Rest, color = new Color(0.35f, 0.65f, 1f, 1f) },
        new RoomIcon { type = RoomType.Elite, color = new Color(0.75f, 0.4f, 0.95f, 1f) },
    };

    private class Tile
    {
        public DungeonRoom Room;
        public Image Image;
        public Image Icon;
    }

    private readonly Dictionary<Vector2Int, Tile> _tiles = new Dictionary<Vector2Int, Tile>();
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left };

    // ───────────────────────── 연결 ─────────────────────────

    private void Awake()
    {
        if (_roomTemplate != null) _roomTemplate.gameObject.SetActive(false);

        // 칸이 영역 밖으로 삐져나오지 않도록 (씬에 이미 있으면 그대로 사용)
        if (_viewport != null && _viewport.GetComponent<RectMask2D>() == null)
            _viewport.gameObject.AddComponent<RectMask2D>();
    }

    private void OnEnable()
    {
        if (_generator != null) _generator.Generated += OnGenerated;
        else Debug.LogWarning("MinimapUI: Generator 가 연결되지 않았습니다.", this);

        if (_roomController != null) _roomController.RoomEntered += OnRoomEntered;
        else Debug.LogWarning("MinimapUI: Room Controller 가 연결되지 않았습니다.", this);
    }

    private void OnDisable()
    {
        if (_generator != null) _generator.Generated -= OnGenerated;
        if (_roomController != null) _roomController.RoomEntered -= OnRoomEntered;
        ClearTiles();
    }

    private void OnGenerated(DungeonRoom startRoom) => Rebuild();
    private void OnRoomEntered(DungeonRoom room) => Refresh();
    private void OnRoomCleared(DungeonRoom room) => Refresh();

    private void Update()
    {
        if (!_centerOnCurrent || _content == null) return;

        float k = _followSpeed <= 0f ? 1f : 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
        _content.anchoredPosition = Vector2.Lerp(_content.anchoredPosition, GetTargetOffset(), k);
    }

    // ───────────────────────── 생성 ─────────────────────────

    /// <summary>현재 던전 기준으로 방 칸을 전부 다시 만든다</summary>
    public void Rebuild()
    {
        ClearTiles();
        if (_generator == null || _content == null || _roomTemplate == null) return;

        foreach (DungeonRoom room in _generator.GetComponentsInChildren<DungeonRoom>(true))
        {
            // 재생성 직후에는 지워질 예정인 이전 방도 남아 있으므로, 생성기에 등록된 방만 사용
            if (room == null || _generator.GetRoom(room.MapCell) != room) continue;
            if (_tiles.ContainsKey(room.MapCell)) continue;

            Image image = Instantiate(_roomTemplate, _content);
            image.name = $"Room {room.MapCell.x},{room.MapCell.y}";

            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = _cellSize;
            rt.anchoredPosition = CellToPosition(room.MapCell);

            Transform iconTr = image.transform.Find("Icon");
            var tile = new Tile
            {
                Room = room,
                Image = image,
                Icon = iconTr != null ? iconTr.GetComponent<Image>() : null
            };

            _tiles[room.MapCell] = tile;
            room.Cleared += OnRoomCleared;
        }

        Refresh();
        _content.anchoredPosition = GetTargetOffset(); // 처음에는 바로 맞춘다
    }

    private void ClearTiles()
    {
        foreach (Tile tile in _tiles.Values)
        {
            if (tile.Room != null) tile.Room.Cleared -= OnRoomCleared;
            if (tile.Image != null) Destroy(tile.Image.gameObject);
        }
        _tiles.Clear();
    }

    // ───────────────────────── 표시 ─────────────────────────

    /// <summary>방문/클리어/현재 방 상태에 맞춰 색과 표시 여부를 갱신한다</summary>
    public void Refresh()
    {
        DungeonRoom current = _roomController != null ? _roomController.CurrentRoom : null;

        foreach (Tile tile in _tiles.Values)
        {
            DungeonRoom room = tile.Room;
            if (room == null || tile.Image == null) continue;

            bool visited = room.IsVisited;
            bool known = visited || (_showAdjacentUnvisited && HasVisitedNeighbor(room.MapCell));
            tile.Image.gameObject.SetActive(known);
            if (!known) continue;

            if (room == current) tile.Image.color = _currentColor;
            else if (!visited) tile.Image.color = _unknownColor;
            else tile.Image.color = room.IsCleared ? _clearedColor : _unclearedColor;

            UpdateIcon(tile, visited);
        }
    }

    private void UpdateIcon(Tile tile, bool visited)
    {
        if (tile.Icon == null) return;

        RoomType type = tile.Room.Data != null ? tile.Room.Data.RoomType : RoomType.Normal;
        RoomIcon icon = FindIcon(type);

        if (type == RoomType.Normal || icon == null)
        {
            tile.Icon.gameObject.SetActive(false);
            return;
        }

        tile.Icon.gameObject.SetActive(true);
        if (icon.sprite != null) tile.Icon.sprite = icon.sprite;

        Color c = icon.sprite != null ? Color.white : icon.color;
        if (!visited) c.a *= _unknownIconAlpha;
        tile.Icon.color = c;
    }

    private RoomIcon FindIcon(RoomType type)
    {
        foreach (RoomIcon icon in _icons)
        {
            if (icon != null && icon.type == type) return icon;
        }
        return null;
    }

    private bool HasVisitedNeighbor(Vector2Int cell)
    {
        foreach (Vector2Int dir in Directions)
        {
            if (_tiles.TryGetValue(cell + dir, out Tile n) && n.Room != null && n.Room.IsVisited)
                return true;
        }
        return false;
    }

    private Vector2 CellToPosition(Vector2Int cell)
    {
        return new Vector2(cell.x * (_cellSize.x + _gap.x), cell.y * (_cellSize.y + _gap.y));
    }

    /// <summary>현재 방이 미니맵 가운데 오도록 하는 Content 위치</summary>
    private Vector2 GetTargetOffset()
    {
        DungeonRoom current = _roomController != null ? _roomController.CurrentRoom : null;
        if (current != null && _tiles.ContainsKey(current.MapCell))
            return -CellToPosition(current.MapCell);

        return _content != null ? _content.anchoredPosition : Vector2.zero;
    }
}
