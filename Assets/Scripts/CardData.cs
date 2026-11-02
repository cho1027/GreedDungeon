using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 한 장의 데이터 (이름, 코스트, 이미지, 설명, 효과).
/// 만들기: Project 우클릭 → Create → GridDungeon → Card Data
/// 효과는 CardEffect 에셋(예: AttackCardEffect)을 Effects 목록에 넣으면 사용 시 순서대로 실행된다.
/// </summary>
[CreateAssetMenu(menuName = "GridDungeon/Card Data", fileName = "CardData")]
public class CardData : ScriptableObject
{
    [SerializeField] private string _cardName = "카드";
    [SerializeField] private int _cost = 1;
    [SerializeField] private Sprite _artwork;
    [TextArea]
    [SerializeField] private string _description;

    [Header("효과 (위에서부터 순서대로 실행)")]
    [SerializeField] private List<CardEffect> _effects = new List<CardEffect>();

    public string CardName => _cardName;
    public int Cost => _cost;
    public Sprite Artwork => _artwork;
    public string Description => _description;
    public IReadOnlyList<CardEffect> Effects => _effects;
}
