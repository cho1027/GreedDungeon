using UnityEngine;

public class DungeonRoom : MonoBehaviour
{
    [Header("출입구 오브젝트")]
    [SerializeField] private GameObject northDoor;
    [SerializeField] private GameObject southDoor;
    [SerializeField] private GameObject eastDoor;
    [SerializeField] private GameObject westDoor;

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
    }
}