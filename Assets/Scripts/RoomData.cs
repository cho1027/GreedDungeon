using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Normal = 0,
    Elite,
    Treasure,
    Rest,
    Shop,
    Boss
}

/// <summary>방에 생성되는 물체의 종류 (에디터 표시/검색용)</summary>
public enum RoomSpawnKind
{
    Monster = 0,
    Chest,
    Obstacle,
    Prop
}

/// <summary>방 안의 "어느 칸에 무엇을 생성할지" 한 줄</summary>
[Serializable]
public class RoomSpawnEntry
{
    public RoomSpawnKind kind = RoomSpawnKind.Monster;
    public GameObject prefab;

    [Tooltip("바닥 그리드 좌표. 왼쪽 아래가 (0,0), 범위는 (0~10, 0~6)")]
    public Vector2Int cell;

    [Tooltip("켜면 이 칸으로는 플레이어가 이동할 수 없다 (몬스터, 상자, 바위 등)")]
    public bool blocksMovement = true;

    [Tooltip("켜면 스프라이트 크기를 타일 1칸에 맞춘다")]
    public bool fitToCell = true;

    [Range(0.3f, 1f)]
    public float fillRatio = 0.9f;
}

/// <summary>
/// 방 한 개의 내용 데이터.
/// DungeonRoom 프리팹의 Data 칸에 넣으면, 플레이어가 처음 들어갈 때 spawns 대로 물체가 생성된다.
/// 만들기: Project 우클릭 → Create → GridDungeon → Room Data
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Room Data", fileName = "RoomData")]
public class RoomData : ScriptableObject
{
    [Header("기본")]
    [SerializeField] private RoomType _roomType = RoomType.Normal;

    [Tooltip("켜면 처음 들어갈 때 바로 '클리어' 상태가 된다 (보물방/휴식방 등). 전투방은 끄고 직접 SetCleared() 를 호출한다")]
    [SerializeField] private bool _clearedOnEnter = true;

    [Header("생성할 물체 (몬스터, 상자, 장애물 ...)")]
    [SerializeField] private List<RoomSpawnEntry> _spawns = new List<RoomSpawnEntry>();

    [Header("방 이동 트리거 (위에서부터 순서대로 실행)")]
    [Tooltip("방에 들어온 직후 실행 (전투 시작, 보스 연출 등)")]
    [SerializeField] private List<RoomTrigger> _enterTriggers = new List<RoomTrigger>();

    [Tooltip("방에서 나가기 직전에 실행")]
    [SerializeField] private List<RoomTrigger> _exitTriggers = new List<RoomTrigger>();

    public RoomType RoomType => _roomType;
    public bool ClearedOnEnter => _clearedOnEnter;
    public IReadOnlyList<RoomSpawnEntry> Spawns => _spawns;
    public IReadOnlyList<RoomTrigger> EnterTriggers => _enterTriggers;
    public IReadOnlyList<RoomTrigger> ExitTriggers => _exitTriggers;
}