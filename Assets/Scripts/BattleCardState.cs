using System.Collections.Generic;
using UnityEngine;

public enum PlannedCard
{
    None, Diagonal, Guard, Drain, Snare, Trap, Pickpocket, Hammer, Bomb,
    Intimidation, Rewind, Turret, Repair, Shuffle, Armor, EnergyDrink, Generator, Thrust, Sweep, Execute, ManaSurge, BloodPact
}

public class BattleCardState
{
    private readonly Queue<Vector2Int> _positions = new Queue<Vector2Int>();
    private readonly Dictionary<EnemyController, int> _roots = new Dictionary<EnemyController, int>();
    private readonly HashSet<Vector2Int> _traps = new HashSet<Vector2Int>();
    private readonly List<(Vector2Int cell, CardContext context)> _bombs = new List<(Vector2Int, CardContext)>();
    private int _turn;
    private int _armorTurn = -1;
    private Vector2Int _turretCell;
    private int _turretHealth;
    private GameObject _turret;

    private Vector2Int _generatorCell;
    private int _generatorHealth;
    private GameObject _generator;

    public bool HasGenerator => _generatorHealth > 0;
    public bool HasTurret => _turretHealth > 0;

    public void Reset()
    {
        _positions.Clear();
        _roots.Clear();
        _traps.Clear();
        _bombs.Clear();
        _turn = 0;
        _armorTurn = -1;
        _generatorHealth = 0;
        if (_generator != null) Object.Destroy(_generator);
        _turretHealth = 0;
        if (_turret != null) Object.Destroy(_turret);
    }

    public void BeginTurn(Vector2Int cell)
    {
        _turn++;
        if (_positions.Count == 3) _positions.Dequeue();
        _positions.Enqueue(cell);
    }

    public bool TryGetRewindCell(out Vector2Int cell)
    {
        cell = _positions.Count == 3 ? _positions.Peek() : default;
        return _positions.Count == 3;
    }

    public bool IsRooted(EnemyController enemy) =>
        _roots.TryGetValue(enemy, out int until) && _turn <= until;

    public void Root(EnemyController enemy, int turns, bool duringEnemyTurn = false)
    {
        int until = _turn + turns - (duringEnemyTurn ? 0 : 1);
        if (!_roots.TryGetValue(enemy, out int current) || until > current)
            _roots[enemy] = until;
    }

    public bool HasTrap(Vector2Int cell) => _traps.Contains(cell);
    public void PlaceTrap(Vector2Int cell) => _traps.Add(cell);

    public void TriggerTrap(EnemyController enemy)
    {
        if (!_traps.Remove(enemy.Cell)) return;
        Root(enemy, 5, enemy.Manager != null && enemy.Manager.IsEnemyTurn);
        enemy.Health?.TakeDamage(DamageInfo.Trap(3));
    }

    public void PlaceBomb(Vector2Int cell, CardContext context) => _bombs.Add((cell, context));

    public void EquipArmor() => _armorTurn = _turn + 1;
    public int ReduceDamage(int amount) => _armorTurn == _turn ? amount / 2 : amount;

    public void PlaceTurret(Vector2Int cell, RoomGrid grid)
    {
        _turretCell = cell;
        _turretHealth = 5;
        _turret = CreateDevice("Turret", cell, grid);
    }

    public void PlaceGenerator(Vector2Int cell, RoomGrid grid)
    {
        _generatorCell = cell;
        _generatorHealth = 5;
        _generator = CreateDevice("Cost Generator", cell, grid);
    }

    private static GameObject CreateDevice(string name, Vector2Int cell, RoomGrid grid)
    {
        GameObject device = GameObject.CreatePrimitive(PrimitiveType.Cube);
        device.name = name;
        Object.Destroy(device.GetComponent<Collider>());
        device.transform.SetParent(grid.transform, true);
        device.transform.position = grid.GridToWorld(cell);
        device.transform.localScale = Vector3.one * (grid.CellSize * 0.55f);
        return device;
    }

    public void RepairTurret() => _turretHealth = Mathf.Min(5, _turretHealth + 3);

    public bool Occupies(Vector2Int cell) =>
        (HasTurret && _turretCell == cell) || (HasGenerator && _generatorCell == cell);

    public void HitDevices(IReadOnlyList<Vector2Int> cells, int damage)
    {
        foreach (Vector2Int cell in cells)
        {
            if (HasTurret && cell == _turretCell)
            {
                _turretHealth = Mathf.Max(0, _turretHealth - damage);
                if (!HasTurret) Object.Destroy(_turret);
            }

            if (HasGenerator && cell == _generatorCell)
            {
                _generatorHealth = Mathf.Max(0, _generatorHealth - damage);
                if (!HasGenerator) Object.Destroy(_generator);
            }
        }
    }

    public static List<EnemyController> Enemies(DungeonRoom room)
    {
        var enemies = new List<EnemyController>();
        foreach (SpawnedEntity entity in room.Spawned)
        {
            if (entity == null || entity.Object == null || !entity.Object.activeInHierarchy) continue;
            var enemy = entity.Object.GetComponentInChildren<EnemyController>();
            if (enemy != null && enemy.IsAlive && enemy.Health != null) enemies.Add(enemy);
        }
        return enemies;
    }

    public void EndTurn(BattleTurnManager battle)
    {
        foreach (var bomb in _bombs)
        {
            bomb.context.BeginAttack();
            foreach (EnemyController enemy in Enemies(battle.CurrentRoom))
            {
                Vector2Int offset = enemy.Cell - bomb.cell;
                if (Mathf.Abs(offset.x) <= 1 && Mathf.Abs(offset.y) <= 1)
                    bomb.context.Attack(enemy.Health, 15);
            }
            Vector2Int playerOffset = battle.Player.Cell - bomb.cell;
            if (Mathf.Abs(playerOffset.x) <= 1 && Mathf.Abs(playerOffset.y) <= 1)
                battle.PlayerHealth?.TakeDamage(DamageInfo.Trap(15));
            bomb.context.ApplyLethalEffects();
            if (battle.IsPlayerDead) break;
        }
        _bombs.Clear();

        if (!HasTurret || battle.IsPlayerDead) return;
        EnemyController nearest = null;
        int distance = int.MaxValue;
        foreach (EnemyController enemy in Enemies(battle.CurrentRoom))
        {
            Vector2Int offset = enemy.Cell - _turretCell;
            int steps = Mathf.Abs(offset.x) + Mathf.Abs(offset.y);
            if (steps >= distance) continue;
            distance = steps;
            nearest = enemy;
        }
        if (nearest != null) nearest.Health.TakeDamage(DamageInfo.Attack(5, _turret));
    }
}
