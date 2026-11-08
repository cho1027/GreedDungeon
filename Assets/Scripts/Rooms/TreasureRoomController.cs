using System.Collections.Generic;

/// <summary>상자 선택 규칙. 서로 다른 유물 3개가 준비되어야 선택을 허용합니다.</summary>
public static class TreasureRoomController
{
    public static bool Prepare(RoomUsageData data, RelicData[] pool, RelicManager inventory, bool developmentMode = true)
    {
        if (data.offers.Count == 3) return true;
        var candidates = new List<RelicData>();
        var ids = new HashSet<string>();
        if (pool != null) foreach (RelicData relic in pool)
            if (inventory.CanAcquire(relic) && (!relic.developmentOnly || developmentMode) && ids.Add(relic.relicId)) candidates.Add(relic);
        if (candidates.Count < 3) { data.result = "선택 가능한 서로 다른 유물 데이터가 3개 필요합니다."; return false; }
        // 최초 개봉에만 후보를 확정합니다. 재방문해도 다시 추첨하지 않습니다.
        for (int i = 0; i < 3; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            data.offers.Add(candidates[index]); candidates.RemoveAt(index);
        }
        return true;
    }
    public static bool Choose(RoomUsageData data, int index, RelicManager inventory)
    {
        if (!data.activated || data.completed || data.offers.Count != 3 || index < 0 || index >= 3) return false;
        RelicData chosen = data.offers[index];
        if (!inventory.TryAcquire(chosen)) { data.result = "이미 보유했거나 유효하지 않은 유물입니다."; return false; }
        data.selectedRelicId = chosen.relicId;
        data.completed = true;
        data.result = $"{chosen.relicName} 획득";
        return true;
    }
}
