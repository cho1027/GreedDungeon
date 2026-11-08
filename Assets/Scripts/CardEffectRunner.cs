using UnityEngine;

public class CardEffectRunner : MonoBehaviour
{
    [SerializeField] private BattleTurnManager _battle;
    [SerializeField] private HandUI _hand;
    [SerializeField] private GridPlayer _player;
    [SerializeField] private DeckManager _deck;
    [SerializeField] private CardData _repairCard;

    private GridPlayer Player => _player != null ? _player : _battle.Player;
    private BattleCardState State => _battle.CardState;
    private static int BloodPactHealthCost(PlayerHealth health) =>
        Mathf.Max(1, Mathf.CeilToInt(health.MaxHealth * 0.15f));

    private void Awake()
    {
        if (_deck == null) _deck = GetComponent<DeckManager>();
    }

    private void OnEnable()
    {
        if (_hand == null) return;
        _hand.CardUsed += OnCardUsed;
        _hand.CanTargetCard = CanTarget;
    }

    private void OnDisable()
    {
        if (_hand == null) return;
        _hand.CardUsed -= OnCardUsed;
        _hand.CanTargetCard = null;
    }

    private Vector2Int TargetCell(Vector2 screen)
    {
        Ray ray = Player.Camera.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.forward, _battle.CurrentRoom.Grid.WorldCenter);
        return plane.Raycast(ray, out float distance)
            ? _battle.CurrentRoom.Grid.WorldToGrid(ray.GetPoint(distance))
            : new Vector2Int(-1, -1);
    }

    private bool CanTarget(CardData card, Vector2 screen)
    {
        if (card == null || _battle == null || Player == null || _battle.CurrentRoom == null) return false;
        if (!_battle.IsPlayerTurn) return false;
        var context = new CardContext(card, _battle, Player, _battle.CurrentRoom, _deck);
        foreach (CardEffect effect in card.Effects)
            if (effect != null && !effect.CanApply(context)) return false;
        if (card.Planned == PlannedCard.Rewind)
            return !Player.IsMoving && State.TryGetRewindCell(out Vector2Int previous) &&
                Player.CurrentRoom.IsWalkable(previous) && !State.Occupies(previous);
        if (card.Planned == PlannedCard.Thrust)
            return Player.Camera != null && TargetCell(screen) != Player.Cell &&
                _battle.CurrentRoom.Grid.InBounds(TargetCell(screen));
        if (card.Planned == PlannedCard.Repair) return State.HasTurret;
        if (card.Planned != PlannedCard.Snare && card.Planned != PlannedCard.Trap &&
            card.Planned != PlannedCard.Bomb && card.Planned != PlannedCard.Turret &&
            card.Planned != PlannedCard.Generator && card.Planned != PlannedCard.Execute &&
            card.Planned != PlannedCard.BloodPact) return true;

        if (Player.Camera == null) return false;
        Vector2Int cell = TargetCell(screen);
        RoomGrid grid = _battle.CurrentRoom.Grid;
        if (!grid.InBounds(cell)) return false;
        if (card.Planned == PlannedCard.Snare)
            return BattleCardState.Enemies(_battle.CurrentRoom).Exists(enemy => enemy.Cell == cell);
        if (card.Planned == PlannedCard.BloodPact)
        {
            PlayerHealth health = _battle.PlayerHealth;
            return health != null && health.CurrentHealth > BloodPactHealthCost(health) &&
                BattleCardState.Enemies(_battle.CurrentRoom).Exists(enemy => enemy.Cell == cell);
        }
        if (card.Planned == PlannedCard.Execute)
            return BattleCardState.Enemies(_battle.CurrentRoom).Exists(enemy =>
                enemy.Cell == cell && enemy.Health.CurrentHealth <= enemy.Health.MaxHealth / 2);
        if (card.Planned == PlannedCard.Bomb) return true;
        if (!grid.IsWalkable(cell) || cell == Player.Cell || State.Occupies(cell)) return false;
        return card.Planned switch
        {
            PlannedCard.Trap => !State.HasTrap(cell),
            PlannedCard.Generator => !State.HasGenerator,
            _ => !State.HasTurret
        };
    }

    private void OnCardUsed(CardData card)
    {
        var context = new CardContext(card, _battle, Player, _battle.CurrentRoom, _deck);
        foreach (CardEffect effect in card.Effects)
        {
            if (effect != null) effect.Apply(context);
        }
        ApplyPlannedCard(context);
        context.ApplyLethalEffects();
        _battle.RefreshCombatState();
    }

    private void ApplyPlannedCard(CardContext context)
    {
        switch (context.Card.Planned)
        {
            case PlannedCard.Thrust:
                ApplyThrust(context);
                break;
            case PlannedCard.BloodPact:
            case PlannedCard.Execute:
            case PlannedCard.Sweep:
            case PlannedCard.Diagonal:
            case PlannedCard.Drain:
            case PlannedCard.Hammer:
            case PlannedCard.Intimidation:
            case PlannedCard.Snare:
            case PlannedCard.Pickpocket:
                ApplyToEnemies(context);
                break;
            case PlannedCard.Guard:
                Player.GetComponent<DamageShield>()?.Add(Mathf.Max(0, 5 + _battle.CardDexterity));
                break;
            case PlannedCard.Trap:
                State.PlaceTrap(TargetCell(_hand.DropPosition));
                break;
            case PlannedCard.Bomb:
                State.PlaceBomb(TargetCell(_hand.DropPosition), context);
                break;
            case PlannedCard.Rewind:
                if (State.TryGetRewindCell(out Vector2Int previous)) Player.TryTeleport(previous);
                break;
            case PlannedCard.Turret:
                State.PlaceTurret(TargetCell(_hand.DropPosition), context.Room.Grid);
                _deck.GainToHand(_repairCard);
                break;
            case PlannedCard.Repair:
                State.RepairTurret();
                break;
            case PlannedCard.Shuffle:
                _deck.ShuffleHandIntoDraw();
                break;
            case PlannedCard.ManaSurge:
                _battle.ApplyManaSurge();
                break;
            case PlannedCard.EnergyDrink:
                _battle.GainCost(1);
                break;
            case PlannedCard.Generator:
                State.PlaceGenerator(TargetCell(_hand.DropPosition), context.Room.Grid);
                break;
            case PlannedCard.Armor:
                State.EquipArmor();
                break;
        }
    }

    private void ApplyThrust(CardContext context)
    {
        context.BeginAttack();
        Vector2Int offset = TargetCell(_hand.DropPosition) - Player.Cell;
        Vector2Int direction = Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
            ? new Vector2Int(offset.x > 0 ? 1 : -1, 0)
            : new Vector2Int(0, offset.y > 0 ? 1 : -1);

        var enemies = BattleCardState.Enemies(context.Room);
        for (int distance = 1; distance <= 2; distance++)
        {
            Vector2Int cell = Player.Cell + direction * distance;
            var enemy = enemies.Find(candidate => candidate.Cell == cell);
            if (enemy != null)
            {
                context.Attack(enemy.Health, 6);
                return;
            }
            if (!context.Room.Grid.IsWalkable(cell) || State.Occupies(cell)) return;
        }
    }

    private void ApplyToEnemies(CardContext context)
    {
        if (context.Card.Planned != PlannedCard.Snare && context.Card.Planned != PlannedCard.Pickpocket)
            context.BeginAttack();
        Vector2Int facing = Player.Facing;
        Vector2Int side = new Vector2Int(-facing.y, facing.x);
        int draws = 0;
        foreach (EnemyController enemy in BattleCardState.Enemies(context.Room))
        {
            Vector2Int offset = enemy.Cell - Player.Cell;
            int forward = offset.x * facing.x + offset.y * facing.y;
            int across = Mathf.Abs(offset.x * side.x + offset.y * side.y);
            switch (context.Card.Planned)
            {
                case PlannedCard.Diagonal when Mathf.Abs(offset.x) == 1 && Mathf.Abs(offset.y) == 1:
                    context.Attack(enemy.Health, 5);
                    break;
                case PlannedCard.Drain when forward == 1 && across == 0:
                    _battle.PlayerHealth?.Heal(context.Attack(enemy.Health, 5).HealthLost);
                    break;
                case PlannedCard.Hammer when forward >= 1 && forward <= 3 && across <= 1:
                    context.Attack(enemy.Health, 9);
                    break;
                case PlannedCard.Intimidation:
                    context.Attack(enemy.Health, 11);
                    break;
                case PlannedCard.Snare when enemy.Cell == TargetCell(_hand.DropPosition):
                    State.Root(enemy, 3);
                    break;
                case PlannedCard.BloodPact when enemy.Cell == TargetCell(_hand.DropPosition):
                    PlayerHealth health = _battle.PlayerHealth;
                    if (health != null && health.TrySpendHealth(BloodPactHealthCost(health)))
                        context.Attack(enemy.Health, 15);
                    return;
                case PlannedCard.Execute when enemy.Cell == TargetCell(_hand.DropPosition) &&
                    enemy.Health.CurrentHealth <= enemy.Health.MaxHealth / 2:
                    context.Attack(enemy.Health, 20);
                    break;
                case PlannedCard.Sweep when forward == 1 && across <= 1:
                    context.Attack(enemy.Health, 5);
                    break;
                case PlannedCard.Pickpocket when forward == 1 && across <= 1:
                    draws++;
                    break;
            }
        }
        if (draws > 0) _deck.Draw(draws);
    }
}
