using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1챕터 보스 시체왕.
///
/// 1페이즈 – 거대한 육체
///  - 보스 본체는 1칸, 이동하지 않는다.
///  - 전투가 시작되면 방 바닥 곳곳(무작위, 흩어진 위치)이 "시체 타일"로 오염된다. 시체 타일은 그냥 밟을 수 있다.
///  - 보스를 때리면 깎인 체력만큼 "무작위 시체 타일"의 내구도가 깎이고, 0 이 되면 그 타일이 파괴(복구 X)되어 원래 바닥으로 돌아온다.
///  - 1페이즈에서는 체력이 (최대 체력 - 시체 타일 내구도 합) 아래로 내려가지 않는다 (2페이즈 몫).
///  - 대지 붕괴는 플레이어와 가장 가까운 시체 타일을 기준으로 한다 (Ch1.NearestCorpseTile).
///
/// 페이즈 전환 – 육체 붕괴
///  - 시체 타일이 모두 파괴되면: 화면 흔들림 + 바닥 균열 → 암전(추락) → 아래층(바닥 색이 바뀐 방)에서 2페이즈
///
/// 2페이즈 – 드러난 본체
///  - 자유롭게 이동 (행동 뒤 이동 = Chase)
///  - 행동은 Ch1CorpseKingSelector 가 Phase 값을 보고 2페이즈 선택기로 바꾼다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyHealth))]
public class CorpseKingBoss : MonoBehaviour, IDamageAbsorber, IRoomSpawned
{
    [Header("1페이즈 시체 타일")]
    [Tooltip("방 바닥에 흩어 놓을 시체 타일 수")]
    [Min(1)][SerializeField] private int _corpseTileCount = 10;
    [Tooltip("최대 체력 중 1페이즈(시체 타일 내구도 합)에 쓰는 비율. 나머지가 2페이즈 체력")]
    [Range(0.1f, 0.9f)][SerializeField] private float _phase1HealthRatio = 0.5f;
    [Tooltip("시체 타일 그림 (바닥 위에 깔린다)")]
    [SerializeField] private Sprite _bodyTileSprite;
    [Tooltip("1페이즈 보스 본체 그림")]
    [SerializeField] private Sprite _coreSprite;
    [Range(0.5f, 1f)][SerializeField] private float _tileFill = 1f;
    [Tooltip("시체 타일을 보스 바로 옆(상하좌우)에는 두지 않는다")]
    [SerializeField] private bool _keepAwayFromBoss = true;

    [Header("2페이즈")]
    [SerializeField] private Sprite _revealedSprite;
    [Tooltip("2페이즈 시작 칸 (-1 = 방 중앙 위쪽)")]
    [SerializeField] private Vector2Int _phase2BossCell = new Vector2Int(-1, -1);
    [Tooltip("2페이즈 플레이어 시작 칸 (-1 = 방 중앙 아래쪽)")]
    [SerializeField] private Vector2Int _phase2PlayerCell = new Vector2Int(-1, -1);
    [Tooltip("추락한 아래층 바닥 색 (방의 바닥/벽 스프라이트에 곱해진다)")]
    [SerializeField] private Color _lowerFloorTint = new Color(0.55f, 0.42f, 0.5f, 1f);

    [Header("페이즈 전환 연출")]
    [Min(0f)][SerializeField] private float _collapseTime = 1.4f;
    [Min(0f)][SerializeField] private float _shakeStrength = 0.25f;
    [Min(0)][SerializeField] private int _crackCount = 14;
    [Min(0f)][SerializeField] private float _fadeTime = 0.6f;
    [Min(0f)][SerializeField] private float _blackHoldTime = 0.5f;

    private class CorpseTile
    {
        public Vector2Int Cell;
        public int Durability;
        public int MaxDurability;
        public SpriteRenderer View;
    }

    private const int TileSortingOrder = 1; // 바닥 위, 몬스터/플레이어 아래

    private readonly List<CorpseTile> _tiles = new List<CorpseTile>();
    private readonly List<Vector2Int> _cellBuffer = new List<Vector2Int>();
    private EnemyController _controller;
    private EnemyHealth _health;
    private DungeonRoom _room;
    private int _phase2Health;
    private bool _tilesBuilt;

    /// <summary>1 = 거대한 육체, 2 = 드러난 본체</summary>
    public int Phase { get; private set; } = 1;
    public bool IsTransitioning { get; private set; }
    public int RemainingTiles => _tiles.Count;

    /// <summary>남아 있는 시체 타일 칸 (1페이즈에서만)</summary>
    public IReadOnlyList<Vector2Int> CorpseTileCells
    {
        get
        {
            _cellBuffer.Clear();
            if (Phase == 1) foreach (CorpseTile t in _tiles) _cellBuffer.Add(t.Cell);
            return _cellBuffer;
        }
    }

    public int Order => 1000; // 방어막보다 나중에

    private void Awake()
    {
        _controller = GetComponent<EnemyController>();
        _health = GetComponent<EnemyHealth>();
        _health.RegisterAbsorber(this);
    }

    private void OnEnable()
    {
        if (_health != null) _health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (_health != null) _health.Damaged -= OnDamaged;
    }

    /// <summary>방의 다른 몬스터/장애물이 모두 배치된 뒤(첫 프레임)에 시체 타일을 깐다</summary>
    private void Start()
    {
        EnsureTiles();
    }

    private void OnDestroy()
    {
        foreach (CorpseTile t in _tiles)
            if (t.View != null) Destroy(t.View.gameObject);
        _tiles.Clear();
    }

    // ───────────────────────── 배치 ─────────────────────────

    public void OnRoomSpawned(DungeonRoom room, Vector2Int cell, RoomSpawnEntry entry)
    {
        _room = room;
        _controller.SetPostMove(EnemyPostMove.None); // 1페이즈는 이동하지 않는다
        if (_controller.BodySprite != null && _coreSprite != null) _controller.BodySprite.sprite = _coreSprite;
    }

    private void EnsureTiles()
    {
        if (_tilesBuilt || _room == null || Phase != 1) return;
        _tilesBuilt = true;

        RoomGrid grid = _room.Grid;
        Vector2Int boss = _controller.Cell;

        // 다른 몬스터/상자/장애물이 있는 칸은 제외
        var taken = new HashSet<Vector2Int> { boss };
        if (_room.Spawned != null)
            foreach (SpawnedEntity s in _room.Spawned)
                if (s != null && s.Object != null) taken.Add(s.Cell);
        BattleTurnManager battle = _controller.Manager != null ? _controller.Manager : FindAnyObjectByType<BattleTurnManager>();
        GridPlayer player = battle != null ? battle.Player : null;
        if (player != null && player.CurrentRoom == grid) taken.Add(player.Cell);

        var candidates = new List<Vector2Int>();
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var c = new Vector2Int(x, y);
                if (taken.Contains(c) || !grid.IsWalkable(c)) continue;
                if (_keepAwayFromBoss && Mathf.Abs(c.x - boss.x) + Mathf.Abs(c.y - boss.y) <= 1) continue;
                candidates.Add(c);
            }
        }

        int count = Mathf.Min(_corpseTileCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Vector2Int c = candidates[index];
            candidates.RemoveAt(index);
            _tiles.Add(new CorpseTile { Cell = c, View = CreateTileView(c) });
        }

        if (_tiles.Count == 0)
        {
            // 깔 곳이 없으면 1페이즈 없이 바로 2페이즈 체력으로 시작
            _phase2Health = 0;
            Debug.LogWarning("[Boss] 시체 타일을 깔 칸이 없습니다.", this);
            return;
        }

        // 내구도 = 1페이즈 체력을 타일 수로 나눈 값 (나머지는 앞 타일부터 1씩)
        int pool = Mathf.Clamp(Mathf.RoundToInt(_health.MaxHealth * _phase1HealthRatio), _tiles.Count, _health.MaxHealth - 1);
        int each = pool / _tiles.Count;
        int extra = pool % _tiles.Count;
        for (int i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].MaxDurability = each + (i < extra ? 1 : 0);
            _tiles[i].Durability = _tiles[i].MaxDurability;
        }

        _phase2Health = _health.MaxHealth - pool;
        Debug.Log($"[Boss] 시체 타일 {_tiles.Count}칸 (타일당 내구도 약 {each}, 합 {pool}), 2페이즈 체력 {_phase2Health}", this);
    }

    private SpriteRenderer CreateTileView(Vector2Int cell)
    {
        var go = new GameObject($"CorpseTile {cell}");
        go.transform.SetParent(_room.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _bodyTileSprite != null ? _bodyTileSprite : HazardZones.LoadOrMake("GreedDungeon/CorpseTile", new Color(0.45f, 0.32f, 0.3f, 0.9f));
        sr.sortingOrder = TileSortingOrder;

        Vector3 p = _room.Grid.GridToWorld(cell);
        go.transform.position = new Vector3(p.x, p.y, _room.transform.position.z - 0.005f);
        _room.Grid.FitToCell(go.transform, _tileFill);
        return sr;
    }

    /// <summary>from 에서 가장 가까운 시체 타일 (없으면 false)</summary>
    public bool TryGetNearestCorpseTile(Vector2Int from, out Vector2Int cell)
    {
        cell = from;
        if (Phase != 1 || _tiles.Count == 0) return false;

        int best = int.MaxValue;
        foreach (CorpseTile t in _tiles)
        {
            int d = Mathf.Abs(t.Cell.x - from.x) + Mathf.Abs(t.Cell.y - from.y);
            if (d >= best) continue;
            best = d;
            cell = t.Cell;
        }
        return true;
    }

    // ───────────────────────── 피해 ─────────────────────────

    /// <summary>1페이즈: 체력이 2페이즈 몫 아래로 내려가지 않게 넘치는 피해를 흡수한다</summary>
    public int Absorb(int amount, DamageInfo info)
    {
        EnsureTiles();
        if (Phase != 1 || amount <= 0 || _tiles.Count == 0) return 0;
        if (IsTransitioning) return amount; // 연출 중에는 무적

        int allowed = Mathf.Max(0, _health.CurrentHealth - _phase2Health);
        return Mathf.Max(0, amount - allowed);
    }

    /// <summary>보스가 맞은 만큼 무작위 시체 타일의 내구도가 깎인다</summary>
    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (Phase != 1 || IsTransitioning || result.HealthLost <= 0 || _tiles.Count == 0) return;

        int remaining = result.HealthLost;
        while (remaining > 0 && _tiles.Count > 0)
        {
            CorpseTile tile = _tiles[Random.Range(0, _tiles.Count)];
            int taken = Mathf.Min(remaining, tile.Durability);
            tile.Durability -= taken;
            remaining -= taken;

            if (tile.Durability <= 0) BreakTile(tile);
            else Tint(tile);
        }

        if (_tiles.Count == 0) StartCoroutine(PhaseTransition());
    }

    private void Tint(CorpseTile tile)
    {
        if (tile.View == null || tile.MaxDurability <= 0) return;
        float t = (float)tile.Durability / tile.MaxDurability;
        tile.View.color = Color.Lerp(new Color(0.45f, 0.3f, 0.3f), Color.white, t);
    }

    private void BreakTile(CorpseTile tile)
    {
        _tiles.Remove(tile);
        Debug.Log($"[Boss] 시체 타일 {tile.Cell} 파괴 (남은 {_tiles.Count})", this);
        if (tile.View != null) StartCoroutine(Crumble(tile.View));
    }

    private static IEnumerator Crumble(SpriteRenderer view)
    {
        Transform tr = view.transform;
        Vector3 scale = tr.localScale;
        float t = 0f;
        while (t < 0.3f && view != null)
        {
            t += Time.deltaTime;
            float k = 1f - t / 0.3f;
            tr.localScale = scale * Mathf.Max(0.05f, k);
            view.color = new Color(0.4f, 0.25f, 0.25f, k);
            yield return null;
        }
        if (view != null) Destroy(view.gameObject);
    }

    // ───────────────────────── 페이즈 전환 ─────────────────────────

    private IEnumerator PhaseTransition()
    {
        IsTransitioning = true;
        BattleTurnManager battle = _controller.Manager != null ? _controller.Manager : FindAnyObjectByType<BattleTurnManager>();
        GridPlayer player = battle != null ? battle.Player : null;
        if (battle != null) battle.IsCutscene = true;
        _controller.ClearIntent();

        Debug.Log("[Boss] 육체 붕괴 → 2페이즈", this);

        // 1) 화면 흔들림 + 바닥 균열
        Camera cam = player != null && player.Camera != null ? player.Camera : Camera.main;
        Vector3 camOrigin = cam != null ? cam.transform.position : Vector3.zero;
        var cracks = new List<GameObject>();
        Sprite crack = HazardZones.LoadOrMake("GreedDungeon/Crack", new Color(0f, 0f, 0f, 0.6f));
        RoomGrid grid = _room.Grid;
        float interval = _crackCount > 0 ? _collapseTime / _crackCount : _collapseTime;
        float nextCrack = 0f;

        float time = 0f;
        while (time < _collapseTime)
        {
            time += Time.deltaTime;
            if (cam != null)
            {
                float k = _shakeStrength * (0.4f + 0.6f * time / Mathf.Max(0.01f, _collapseTime));
                cam.transform.position = camOrigin + (Vector3)(Random.insideUnitCircle * k);
            }
            if (cracks.Count < _crackCount && time >= nextCrack)
            {
                nextCrack += interval;
                var c = new Vector2Int(Random.Range(0, grid.Width), Random.Range(0, grid.Height));
                cracks.Add(CreateCrack(c, crack));
            }
            if (_controller.BodySprite != null)
                _controller.BodySprite.color = Color.Lerp(Color.white, new Color(0.5f, 0.3f, 0.3f), time / Mathf.Max(0.01f, _collapseTime));
            yield return null;
        }
        if (cam != null) cam.transform.position = camOrigin;

        // 2) 암전 (추락)
        Image fade = CreateFadeOverlay();
        yield return Fade(fade, 0f, 1f, _fadeTime);

        foreach (GameObject c in cracks) if (c != null) Destroy(c);
        HazardZones.Find(_room)?.ClearAll();

        // 3) 아래층: 바닥 색 변경, 위치 재배치, 본체 등장
        TintRoom(_lowerFloorTint);

        Phase = 2;
        if (_controller.BodySprite != null)
        {
            if (_revealedSprite != null) _controller.BodySprite.sprite = _revealedSprite;
            _controller.BodySprite.color = Color.white;
        }

        Vector2Int bossCell = _phase2BossCell.x >= 0 ? _phase2BossCell : new Vector2Int(grid.Width / 2, grid.Height - 2);
        Vector2Int playerCell = _phase2PlayerCell.x >= 0 ? _phase2PlayerCell : new Vector2Int(grid.Width / 2, 1);
        bossCell = FindFreeCell(bossCell, player, true);
        _controller.PlaceAt(bossCell);
        if (player != null && player.CurrentRoom == grid)
        {
            playerCell = FindFreeCell(playerCell, player, false);
            player.EnterRoom(grid, playerCell, false);
            player.Face(Vector2Int.up);
        }
        _controller.Face(Vector2Int.down);
        _controller.SetPostMove(EnemyPostMove.Chase, 1);

        if (_blackHoldTime > 0f) yield return new WaitForSeconds(_blackHoldTime);
        yield return Fade(fade, 1f, 0f, _fadeTime);
        if (fade != null) Destroy(fade.canvas.gameObject);

        IsTransitioning = false;
        if (battle != null) battle.IsCutscene = false;

        _controller.PlanNextAction();
        Debug.Log($"[Boss] 2페이즈 시작 (체력 {_health.CurrentHealth}/{_health.MaxHealth})", this);
    }

    private Vector2Int FindFreeCell(Vector2Int wanted, GridPlayer player, bool forBoss)
    {
        RoomGrid grid = _room.Grid;
        for (int r = 0; r < Mathf.Max(grid.Width, grid.Height); r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r) continue;
                    Vector2Int c = wanted + new Vector2Int(x, y);
                    if (!grid.InBounds(c)) continue;
                    bool self = forBoss && c == _controller.Cell;
                    if (!self && !grid.IsWalkable(c)) continue;
                    if (player != null && player.CurrentRoom == grid)
                    {
                        if (forBoss && c == player.Cell) continue;
                        if (!forBoss && c == _controller.Cell) continue;
                    }
                    return c;
                }
            }
        }
        return wanted;
    }

    private GameObject CreateCrack(Vector2Int cell, Sprite sprite)
    {
        var go = new GameObject($"Crack {cell}");
        go.transform.SetParent(_room.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;
        Vector3 p = _room.Grid.GridToWorld(cell);
        go.transform.position = new Vector3(p.x, p.y, _room.transform.position.z - 0.01f);
        _room.Grid.FitToCell(go.transform, 1f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
        return go;
    }

    /// <summary>방의 바닥/벽 그림에만 색을 곱한다 (몬스터, 범위 표시 등은 제외)</summary>
    private void TintRoom(Color tint)
    {
        var skip = new HashSet<Transform>();
        foreach (SpawnedEntity s in _room.Spawned)
            if (s != null && s.Object != null) skip.Add(s.Object.transform);

        foreach (SpriteRenderer sr in _room.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.transform == transform) continue;
            if (IsUnder(sr.transform, skip)) continue;
            string n = sr.gameObject.name;
            if (n.Contains("AttackRange") || n.Contains("RotZone") || n.Contains("DelayedStrike") || n.Contains("Crack")) continue;
            sr.color = sr.color * tint;
        }
    }

    private static bool IsUnder(Transform t, HashSet<Transform> roots)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (roots.Contains(p)) return true;
        return false;
    }

    private static Image CreateFadeOverlay()
    {
        var canvasGo = new GameObject("CorpseKing Fade");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var imageGo = new GameObject("Black");
        imageGo.transform.SetParent(canvasGo.transform, false);
        var image = imageGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true; // 연출 중 클릭 막기

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return image;
    }

    private static IEnumerator Fade(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        image.color = new Color(0f, 0f, 0f, to);
    }
}
