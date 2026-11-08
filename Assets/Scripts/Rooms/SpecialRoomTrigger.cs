using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>기존 RoomData 입장 트리거에 연결합니다. 화면을 닫을 때까지 기존 이동 잠금을 유지합니다.</summary>
[CreateAssetMenu(menuName = "GridDungeon/Special Room Screen")]
public class SpecialRoomTrigger : RoomTrigger
{
    public bool treasure;
    public Texture2D campfireLit;
    public Texture2D campfireUnlit;
    public Texture2D chestClosed;
    public Texture2D chestOpen;
    public TMP_FontAsset font;
    public RelicData[] relicPool;
    public bool developmentMode;

    public override IEnumerator OnRoomEnter(RoomContext ctx)
    {
        if (ctx?.Room == null || ctx.Player == null) yield break;
        var go = new GameObject("Special Room Screen");
        SpecialRoomUI screen = go.AddComponent<SpecialRoomUI>();
        screen.Initialize(this, ctx, RoomRunData.Get(RoomRunData.GetRoomId(ctx.Room)));
        try { while (screen != null && !screen.Closed) yield return null; }
        finally { if (go != null) Destroy(go); }
    }
}
