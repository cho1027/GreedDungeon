using UnityEngine;

/// <summary>유물 정의만 보관합니다. 획득 여부와 실제 효과 적용은 별도 시스템의 책임입니다.</summary>
[CreateAssetMenu(menuName = "GridDungeon/Relic")]
public class RelicData : ScriptableObject
{
    public string relicId;
    public string relicName;
    [TextArea] public string description;
    public Sprite icon;
    public string rarity;
    public string effectType;
    public float effectValue;
    public bool developmentOnly;
}
