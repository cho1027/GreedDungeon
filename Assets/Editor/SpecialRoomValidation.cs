using System;
using UnityEditor;
using UnityEngine;

/// <summary>씬을 바꾸지 않고 회복, 상호 배제, 재방문, 유물 선택과 새 런 초기화를 검사합니다.</summary>
public static class SpecialRoomValidation
{
    [MenuItem("Tools/Grid Dungeon/Validate Special Rooms")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Play를 종료한 뒤 검증을 실행하세요. 현재 런 데이터를 보호하기 위해 중단합니다.");
            return;
        }
        var playerObject = new GameObject("Room validation player");
        var relics = new RelicData[4];
        try
        {
            var health = playerObject.AddComponent<PlayerHealth>();
            health.TakeDamage(new DamageInfo(10));
            var data = new RoomUsageData();
            Expect(!RestRoomController.Recover(data,health),"점화 전 회복 차단");
            data.activated=true;
            Expect(RestRoomController.Recover(data,health) && health.CurrentHealth==16,"30% 회복");
            Expect(!RestRoomController.Recover(data,health) && health.CurrentHealth==16,"중복 회복 차단");
            var player=playerObject.AddComponent<GridPlayer>();
            var upgradeData=new RoomUsageData {activated=true};
            Expect(!RestRoomController.Upgrade(upgradeData,player,null) && !upgradeData.completed,"미구현 강화는 방 미소모");
            Expect(RestRoomController.Upgrade(upgradeData,player,new FakeUpgrade()) && upgradeData.completed,"강화 연결");
            Expect(!RestRoomController.Recover(upgradeData,health),"강화 후 회복 차단");
            health.Heal(100);
            data=new RoomUsageData {activated=true};
            Expect(RestRoomController.Recover(data,health) && health.CurrentHealth==20 && data.result.Contains("0 회복"),"최대 체력 회복 0");
            health.TakeDamage(new DamageInfo(100));
            Expect(!RestRoomController.Recover(new RoomUsageData {activated=true},health),"사망 회복 차단");
            var inventory=new RelicManager();
            for(int i=0;i<4;i++) { relics[i]=ScriptableObject.CreateInstance<RelicData>();relics[i].relicId="test."+i;relics[i].relicName="Test "+i; }
            var treasure=new RoomUsageData();
            Expect(!TreasureRoomController.Prepare(treasure,new[]{relics[0],relics[0],relics[1]},inventory),"중복 후보 차단");
            Expect(TreasureRoomController.Prepare(treasure,relics,inventory),"서로 다른 후보 세 개");
            var first=treasure.offers[0];
            Expect(TreasureRoomController.Prepare(treasure,relics,inventory) && treasure.offers[0]==first,"후보 재추첨 방지");
            Expect(!TreasureRoomController.Choose(treasure,0,inventory),"개봉 전 선택 차단");
            treasure.activated=true;
            Expect(!TreasureRoomController.Choose(treasure,3,inventory),"잘못된 선택 차단");
            Expect(TreasureRoomController.Choose(treasure,0,inventory) && inventory.OwnedIds.Count==1,"유물 하나 획득");
            Expect(!TreasureRoomController.Choose(treasure,1,inventory) && inventory.OwnedIds.Count==1,"두 번째 보상 차단");
            Expect(!inventory.TryAcquire(first),"중복 소유 차단");
            RoomRunData.BeginNewRun();
            var persisted=RoomRunData.Get("room-a"); persisted.activated=true;
            Expect(RoomRunData.Get("room-a").activated && !RoomRunData.Get("room-b").activated,"방별 상태 유지");
            RoomRunData.Relics.TryAcquire(relics[0]);
            RoomRunData.BeginNewRun();
            Expect(!RoomRunData.Get("room-a").activated && RoomRunData.Relics.OwnedIds.Count==0,"새 런 초기화");
            Debug.Log("SPECIAL ROOM VALIDATION PASSED: recovery, upgrade, relic selection, revisit state, new run reset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
            foreach(var relic in relics) if(relic!=null) UnityEngine.Object.DestroyImmediate(relic);
        }
    }
    private sealed class FakeUpgrade : IRestRoomUpgrade
    { public bool TryUpgrade(GridPlayer player,out string message) {message="테스트 연결 성공";return true;} }
    private static void Expect(bool condition,string name) { if(!condition) throw new Exception("검증 실패: "+name); }
}
