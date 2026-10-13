using UnityEngine;

[RequireComponent(typeof(RoomGrid))]
public class DungeonRoom : MonoBehaviour
{
    [Header("출입구 오브젝트")]
    [SerializeField] private GameObject northDoor;
    [SerializeField] private GameObject southDoor;
    [SerializeField] private GameObject eastDoor;
    [SerializeField] private GameObject westDoor;

    private RoomGrid _grid;

    /// <summary>이 방의 이동 판정용 그리드</summary>
    public RoomGrid Grid
    {
        get
        {
            if (_grid == null) _grid = GetComponent<RoomGrid>();
            return _grid;
        }
    }

    public void SetDoor(bool north, bool south, bool east, bool west)
    {
        if (northDoor != null)
            northDoor.SetActive(north);

        if (southDoor != null)
            southDoor.SetActive(south);

        if (eastDoor != null)
            eastDoor.SetActive(east);

        if (westDoor != null)
            westDoor.SetActive(west);

        // 이동 판정에도 같은 정보를 적용 (열린 문만 통과 가능)
        Grid.SetDoors(north, south, east, west);
    }
}