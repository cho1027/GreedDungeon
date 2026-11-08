using System.Collections.Generic;

/// <summary>런의 보유 유물 ID 목록. 기본 정책은 같은 ID 중복 획득 금지이며 효과는 적용하지 않습니다.</summary>
public class RelicManager
{
    private readonly HashSet<string> owned = new HashSet<string>();
    public IReadOnlyCollection<string> OwnedIds => owned;
    public bool CanAcquire(RelicData relic) => relic != null &&
        !string.IsNullOrWhiteSpace(relic.relicId) && !owned.Contains(relic.relicId);
    public bool TryAcquire(RelicData relic) => CanAcquire(relic) && owned.Add(relic.relicId);
}
