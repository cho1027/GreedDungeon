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

    [SerializeField] private PlannedCard _plannedCard;
    [Header("카드 키워드")]
    [Tooltip("전투의 첫 손패에 먼저 들어갑니다.")]
    [SerializeField] private bool _innate;
    [Tooltip("사용하면 이번 플레이의 전체 덱에서 제거됩니다.")]
    [SerializeField] private bool _exhaust;
    [Tooltip("턴 종료 후에도 손패에 남습니다.")]
    [SerializeField] private bool _retain;
    [Tooltip("사용하지 않고 턴을 끝내면 소멸합니다.")]
    [SerializeField] private bool _ethereal;
    [Tooltip("이 카드의 공격으로 적을 처치했을 때 실행할 효과입니다.")]
    [SerializeField] private List<CardEffect> _lethalEffects = new List<CardEffect>();

    public PlannedCard Planned => _plannedCard;
    public bool Innate => _innate;
    public bool Exhaust => _exhaust;
    public bool Retain => _retain;
    public bool Ethereal => _ethereal;
    public IReadOnlyList<CardEffect> LethalEffects => _lethalEffects;

    public string CardName => _cardName;
    public int Cost => _cost;
    public Sprite Artwork => _artwork;
    public string Description => _description;
    public IReadOnlyList<CardEffect> Effects => _effects;
}
