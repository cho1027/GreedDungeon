using System.Collections;
using UnityEngine;

/// <summary>트리거가 실행될 때 넘겨받는 정보</summary>
public class RoomContext
{
    public DungeonRoom Room;
    public GridPlayer Player;
    public RoomTransitionController Controller;

    /// <summary>이 방에 처음 들어온 것인지</summary>
    public bool FirstVisit;

    public RoomContext(DungeonRoom room, GridPlayer player, RoomTransitionController controller, bool firstVisit)
    {
        Room = room;
        Player = player;
        Controller = controller;
        FirstVisit = firstVisit;
    }
}

/// <summary>
/// 방 이동 시 실행되는 이벤트의 부모 클래스.
/// 이걸 상속해서 전투 시작, 보스 연출, 대사, 문 잠금 등을 만들고 RoomData 의 트리거 목록에 넣는다.
/// 코루틴이라서 yield return 으로 연출이 끝날 때까지 플레이어 조작을 막아둘 수 있다.
/// (끝나면 컨트롤러가 조작 잠금을 풀어준다)
/// </summary>
public abstract class RoomTrigger : ScriptableObject
{
    /// <summary>방에 들어온 직후 (카메라 이동과 플레이어 등장이 끝난 뒤)</summary>
    public virtual IEnumerator OnRoomEnter(RoomContext ctx)
    {
        yield break;
    }

    /// <summary>방에서 나가기 직전 (카메라 이동 전)</summary>
    public virtual IEnumerator OnRoomExit(RoomContext ctx)
    {
        yield break;
    }
}