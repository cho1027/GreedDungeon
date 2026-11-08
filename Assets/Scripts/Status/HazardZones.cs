using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 바닥에 남는 효과 (방마다 하나, DungeonRoom 오브젝트에 자동으로 붙는다).
///  - 부패 지대: N턴 유지. 플레이어가 그 칸에서 턴을 끝내면 피해 (상태 이상 피해 = 방어도 무시)
///  - 지연 공격(대지 붕괴 등): 바닥에 경고를 표시하고, 다음 적 턴이 시작될 때 터진다
/// BattleTurnManager 가 플레이어 턴 종료 / 적 턴 시작 때 호출한다.
/// 표시 이미지: Resources/GreedDungeon/RotZone, DangerZone (없으면 단색 사각형)
/// </summary>
[DisallowMultipleComponent]
public class HazardZones : MonoBehaviour
{
    private class RotZone
    {
        public Vector2Int Cell;
        public int Turns;
        public int Damage;
        public SpriteRenderer View;
    }

    private class DelayedStrike
    {
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        public readonly List<SpriteRenderer> Views = new List<SpriteRenderer>();
        public int Damage;
        public GameObject Source;
        public int DecayTurns;
        public int DecayDamage;
    }

    private const int SortingOrder = 1;

    private static Sprite _rotSprite;
    private static Sprite _dangerSprite;

    private readonly List<RotZone> _rot = new List<RotZone>();
    private readonly List<DelayedStrike> _strikes = new List<DelayedStrike>();
    private DungeonRoom _room;

    public static HazardZones Get(DungeonRoom room)
    {
        if (room == null) return null;
        HazardZones z = room.GetComponent<HazardZones>();
        if (z == null)
        {
            z = room.gameObject.AddComponent<HazardZones>();
        }
        z._room = room;
        return z;
    }

    /// <summary>
    /// 이미 있을 때만 (없으면 null, 새로 만들지 않음).
    /// 에디터에서 GetComponent 는 없을 때 '가짜 null' 객체를 돌려주므로, 진짜 null 로 바꿔서 돌려준다
    /// (그래야 호출하는 쪽에서 ?. 를 써도 안전하다).
    /// </summary>
    public static HazardZones Find(DungeonRoom room)
    {
        if (room == null) return null;
        HazardZones z = room.GetComponent<HazardZones>();
        return z != null ? z : null;
    }

    private RoomGrid Grid => _room != null ? _room.Grid : null;

    // ───────────────────────── 부패 지대 ─────────────────────────

    public bool IsRotZone(Vector2Int cell)
    {
        foreach (RotZone z in _rot) if (z.Cell == cell) return true;
        return false;
    }

    public void AddRotZone(Vector2Int cell, int turns, int damage)
    {
        if (Grid == null || !Grid.InBounds(cell) || turns <= 0) return;

        foreach (RotZone z in _rot)
        {
            if (z.Cell != cell) continue;
            z.Turns = Mathf.Max(z.Turns, turns);
            z.Damage = Mathf.Max(z.Damage, damage);
            return;
        }

        _rot.Add(new RotZone
        {
            Cell = cell,
            Turns = turns,
            Damage = damage,
            View = CreateView(cell, RotSprite, new Color(1f, 1f, 1f, 0.85f), "RotZone")
        });
    }

    // ───────────────────────── 지연 공격 ─────────────────────────

    public void AddDelayedStrike(IEnumerable<Vector2Int> cells, int damage, GameObject source, int decayTurns = 0, int decayDamage = 0)
    {
        if (Grid == null) return;

        var strike = new DelayedStrike { Damage = damage, Source = source, DecayTurns = decayTurns, DecayDamage = decayDamage };
        foreach (Vector2Int c in cells)
        {
            if (!Grid.InBounds(c) || strike.Cells.Contains(c)) continue;
            strike.Cells.Add(c);
            strike.Views.Add(CreateView(c, DangerSprite, Color.white, "DelayedStrike"));
        }
        if (strike.Cells.Count > 0) _strikes.Add(strike);
    }

    public bool HasPendingStrikes => _strikes.Count > 0;

    // ───────────────────────── 턴 처리 ─────────────────────────

    /// <summary>플레이어 턴 종료: 부패 지대 위에 있으면 피해 → 지대 남은 턴 -1</summary>
    public void OnPlayerTurnEnd(GridPlayer player, PlayerHealth health)
    {
        if (player != null && health != null && Grid != null && player.CurrentRoom == Grid)
        {
            foreach (RotZone z in _rot)
            {
                if (z.Cell != player.Cell || z.Damage <= 0) continue;
                Debug.Log($"[Hazard] 부패 지대 피해 {z.Damage}");
                health.TakeDamage(new DamageInfo(z.Damage, DamageType.Status, gameObject));
                break;
            }
        }

        for (int i = _rot.Count - 1; i >= 0; i--)
        {
            _rot[i].Turns--;
            if (_rot[i].Turns > 0) continue;
            DestroyView(_rot[i].View);
            _rot.RemoveAt(i);
        }
    }

    /// <summary>적 턴 시작: 예고해 둔 지연 공격이 모두 터진다</summary>
    public void OnEnemyTurnStart(GridPlayer player, PlayerHealth health)
    {
        if (_strikes.Count == 0) return;

        var strikes = new List<DelayedStrike>(_strikes);
        _strikes.Clear();

        foreach (DelayedStrike s in strikes)
        {
            bool hit = player != null && health != null && Grid != null &&
                       player.CurrentRoom == Grid && s.Cells.Contains(player.Cell);

            Debug.Log($"[Hazard] 지연 공격 발동 ({s.Cells.Count}칸) → {(hit ? "적중" : "빗나감")}");

            if (hit && !health.IsDead)
            {
                health.TakeDamage(DamageInfo.Attack(s.Damage, s.Source != null ? s.Source : gameObject));
                if (s.DecayTurns > 0)
                    StatusEffectHolder.GetOrAdd(player.gameObject).Apply(StatusType.Decay, s.DecayTurns, s.DecayDamage);
            }

            foreach (SpriteRenderer v in s.Views) DestroyView(v);
        }
    }

    public void ClearAll()
    {
        foreach (RotZone z in _rot) DestroyView(z.View);
        _rot.Clear();
        foreach (DelayedStrike s in _strikes)
            foreach (SpriteRenderer v in s.Views) DestroyView(v);
        _strikes.Clear();
    }

    private void OnDestroy()
    {
        ClearAll();
    }

    // ───────────────────────── 표시 ─────────────────────────

    private SpriteRenderer CreateView(Vector2Int cell, Sprite sprite, Color color, string label)
    {
        var go = new GameObject($"{label} {cell}");
        go.transform.SetParent(_room.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = SortingOrder;

        Vector3 p = Grid.GridToWorld(cell);
        go.transform.position = new Vector3(p.x, p.y, _room.transform.position.z - 0.01f);
        Grid.FitToCell(go.transform, 0.95f);
        return sr;
    }

    private static void DestroyView(SpriteRenderer view)
    {
        if (view != null) Destroy(view.gameObject);
    }

    private static Sprite RotSprite
    {
        get
        {
            if (_rotSprite == null) _rotSprite = LoadOrMake("GreedDungeon/RotZone", new Color(0.35f, 0.7f, 0.2f, 0.55f));
            return _rotSprite;
        }
    }

    private static Sprite DangerSprite
    {
        get
        {
            if (_dangerSprite == null) _dangerSprite = LoadOrMake("GreedDungeon/DangerZone", new Color(1f, 0.2f, 0.15f, 0.45f));
            return _dangerSprite;
        }
    }

    /// <summary>Resources 에서 불러오고, 없으면 단색 사각형을 만든다</summary>
    public static Sprite LoadOrMake(string resourcePath, Color fallback)
    {
        Sprite s = Resources.Load<Sprite>(resourcePath);
        if (s != null) return s;

        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fallback;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
