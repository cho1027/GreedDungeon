using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GridDungeon/Card Effect/Configurable")]
public class ConfiguredCardEffect : CardEffect
{
    private enum Operation { Shield, Draw, Move, Attack, Knockback, Strength, Dexterity, NextAttackStrength }

    [SerializeField] private Operation _operation;
    [SerializeField] private int _amount = 1;
    [SerializeField] private AttackShape _shape;
    [Min(1)][SerializeField] private int _range = 1;
    [Min(1)][SerializeField] private int _hits = 1;
    [SerializeField] private bool _backward;

    public override bool CanApply(CardContext context)
    {
        if (_operation != Operation.Move) return true;
        return MoveDistance(context) == _amount;
    }

    public override void Apply(CardContext context)
    {
        switch (_operation)
        {
            case Operation.Shield:
                context.Player.GetComponent<DamageShield>()?.Add(
                    Mathf.Max(0, _amount + context.Manager.CardDexterity));
                break;
            case Operation.Draw:
                context.Deck?.Draw(_amount);
                break;
            case Operation.Move:
                int distance = MoveDistance(context);
                if (distance > 0)
                    context.Player.TryTeleport(context.Player.Cell + MoveDirection(context) * distance);
                break;
            case Operation.Strength:
                context.Manager.AddCardStrength(_amount);
                break;
            case Operation.NextAttackStrength:
                context.Manager.AddNextAttackStrength(_amount);
                break;
            case Operation.Dexterity:
                context.Manager.AddCardDexterity(_amount);
                break;
            case Operation.Attack:
            case Operation.Knockback:
                ApplyToEnemies(context);
                break;
        }
    }

    private Vector2Int MoveDirection(CardContext context) =>
        _backward ? -context.Player.Facing : context.Player.Facing;

    private int MoveDistance(CardContext context)
    {
        if (context.Player.IsMoving) return 0;
        int distance = 0;
        for (int step = 1; step <= _amount; step++)
        {
            Vector2Int cell = context.Player.Cell + MoveDirection(context) * step;
            if (!context.Room.Grid.InBounds(cell) || !context.Room.Grid.IsWalkable(cell) ||
                context.Manager.CardState.Occupies(cell)) break;
            distance = step;
        }
        return distance;
    }

    private void ApplyToEnemies(CardContext context)
    {
        if (_operation == Operation.Attack) context.BeginAttack();
        var cells = new List<Vector2Int>();
        AttackArea.Calculate(context.Room.Grid, _shape, context.Player.Cell, context.Player.Facing, _range, false, cells);
        foreach (EnemyController enemy in BattleCardState.Enemies(context.Room))
        {
            if (!cells.Contains(enemy.Cell)) continue;
            if (_operation == Operation.Attack)
            {
                for (int hit = 0; hit < _hits && enemy.IsAlive; hit++)
                    context.Attack(enemy.Health, _amount);
            }
            else
            {
                Vector2Int direction = AttackArea.DirectionTowards(context.Player.Cell, enemy.Cell);
                for (int step = 0; step < _amount && enemy.IsAlive; step++)
                    if (!enemy.TryPush(direction)) break;
            }
        }
    }
}
