using UnityEngine;

/// <summary>
/// 카드 한 장의 표시용 데이터. (능력은 아직 없음 — 나중에 이 클래스를 확장하면 된다)
/// 만들기: Project 우클릭  Create  GridDungeon  Card Data
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Card Data", fileName = "CardData")]
public class CardData : ScriptableObject
{
    [SerializeField] private string _cardName = "카드";
    [SerializeField] private int _cost = 1;
    [SerializeField] private Sprite _artwork;
    [TextArea]
    [SerializeField] private string _description;

    public string CardName => _cardName;
    public int Cost => _cost;
    public Sprite Artwork => _artwork;
    public string Description => _description;
}