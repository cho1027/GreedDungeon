using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>방별 실제 진행 데이터. UI를 닫아도 점화·상자 개봉·선택 결과를 유지합니다.</summary>
[Serializable]
public class RoomUsageData
{
    public bool activated;
    public bool completed;
    public string result;
    public string selectedRelicId;
    public List<RelicData> offers = new List<RelicData>();
}

/// <summary>씬을 넘어 유지되는 현재 런 데이터. 새 던전 생성 시 BeginNewRun을 호출합니다.
/// 디스크 세이브는 아직 없으므로 앱 종료 후 복원은 하지 않습니다.</summary>
public static class RoomRunData
{
    private static readonly Dictionary<string, RoomUsageData> rooms = new Dictionary<string, RoomUsageData>();
    public static event Action RunReset;
    public static string RunId { get; private set; } = Guid.NewGuid().ToString("N");
    public static RelicManager Relics { get; private set; } = new RelicManager();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void BeginNewRun()
    {
        RunReset?.Invoke();
        RunReset = null;
        rooms.Clear();
        Relics = new RelicManager();
        RunId = Guid.NewGuid().ToString("N");
    }
    public static string GetRoomId(DungeonRoom room) =>
        $"{RunId}:{room.gameObject.scene.path}:{room.MapCell.x},{room.MapCell.y}";
    public static RoomUsageData Get(string id)
    {
        if (!rooms.TryGetValue(id, out RoomUsageData data)) rooms.Add(id, data = new RoomUsageData());
        return data;
    }
}
