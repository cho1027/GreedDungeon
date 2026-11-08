using UnityEngine;

/// <summary>향후 강화 시스템이 구현할 연결 지점. 성공 시에만 방을 소모합니다.</summary>
public interface IRestRoomUpgrade
{
    bool TryUpgrade(GridPlayer player, out string message);
}

/// <summary>휴식방 규칙. 점화 후 회복과 강화 중 하나만 성공할 수 있습니다.</summary>
public static class RestRoomController
{
    public static bool Recover(RoomUsageData data, PlayerHealth health)
    {
        if (data == null || !data.activated || data.completed || health == null || health.IsDead) return false;
        // 이벤트 콜백에서도 중복 수령하지 않도록 회복 전에 사용 완료를 기록합니다.
        data.completed = true;
        int amount = Mathf.FloorToInt(health.MaxHealth * 0.3f + 0.5f);
        int healed = health.Heal(amount);
        data.result = $"체력 {healed} 회복 ({health.CurrentHealth}/{health.MaxHealth})";
        return true;
    }
    public static bool Upgrade(RoomUsageData data, GridPlayer player, IRestRoomUpgrade upgrade)
    {
        if (data == null || !data.activated || data.completed || player == null) return false;
        if (upgrade == null) { data.result = "강화 기능은 아직 구현되지 않았습니다. 회복을 선택할 수 있습니다."; return false; }
        data.completed = true;
        if (!upgrade.TryUpgrade(player, out string message))
        { data.completed = false; data.result = message; return false; }
        data.result = message;
        return true;
    }
}
