using System.Collections;
using UnityEngine;

/// <summary>
/// 예시 트리거: 클리어되지 않은 방에 들어오면 문을 잠그고, 방이 클리어되면(DungeonRoom.SetCleared) 자동으로 풀린다.
/// 전투방의 RoomData > Enter Triggers 에 넣고, Cleared On Enter 는 꺼두면 된다.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Triggers/Lock Doors Until Cleared", fileName = "LockDoorsUntilCleared")]
public class LockDoorsUntilClearedTrigger : RoomTrigger
{
    public override IEnumerator OnRoomEnter(RoomContext ctx)
    {
        if (!ctx.Room.IsCleared)
            ctx.Room.Grid.DoorsLocked = true;

        yield break;
    }
}