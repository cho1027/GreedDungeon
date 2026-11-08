using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 전투 도중 방에 몬스터를 추가로 만든다 (사령술사 소환 등).
/// RoomData 로 만든 몬스터와 똑같이 DungeonRoom.Spawned 에 등록되므로 턴 진행, 카드 공격, 방 클리어 판정이 그대로 동작한다.
/// (DungeonRoom 에 런타임 추가 함수가 없어 목록에 직접 넣는다)
/// </summary>
public static class RoomSpawnUtil
{
    private static readonly FieldInfo SpawnedField =
        typeof(DungeonRoom).GetField("_spawned", BindingFlags.NonPublic | BindingFlags.Instance);

    public static GameObject SpawnMonster(DungeonRoom room, GameObject prefab, Vector2Int cell, float fill = 0.9f)
    {
        if (room == null || prefab == null) return null;

        var list = SpawnedField != null ? SpawnedField.GetValue(room) as List<SpawnedEntity> : null;
        if (list == null)
        {
            Debug.LogWarning("RoomSpawnUtil: DungeonRoom 의 생성 목록(_spawned)을 찾지 못해 소환하지 못했습니다.", room);
            return null;
        }

        RoomGrid grid = room.Grid;
        GameObject go = Object.Instantiate(prefab, grid.GridToWorld(cell), Quaternion.identity, room.transform);
        grid.FitToCell(go.transform, fill);
        grid.SetBlocked(cell, true);

        var entry = new RoomSpawnEntry
        {
            kind = RoomSpawnKind.Monster,
            prefab = prefab,
            cell = cell,
            blocksMovement = true,
            fitToCell = true,
            fillRatio = fill
        };
        list.Add(new SpawnedEntity { Object = go, Cell = cell, Entry = entry });

        foreach (IRoomSpawned callback in go.GetComponentsInChildren<IRoomSpawned>())
            callback.OnRoomSpawned(room, cell, entry);

        return go;
    }
}
