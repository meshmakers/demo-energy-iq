namespace Meshmakers.EnergyIq.HaystackExport.Runtime;

/// <summary>
/// In-memory snapshot of an OctoMesh runtime model. Indexed by rtId and by ckTypeId.
/// </summary>
public sealed class RtModel
{
    public List<string> Dependencies { get; init; } = [];
    private readonly Dictionary<string, RtEntity> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RtEntity>> _byType = new(StringComparer.Ordinal);

    public void Add(RtEntity entity)
    {
        if (!_byId.TryAdd(entity.RtId, entity))
        {
            throw new InvalidDataException($"Duplicate rtId: {entity.RtId}");
        }

        if (!_byType.TryGetValue(entity.CkTypeId, out var list))
        {
            list = [];
            _byType[entity.CkTypeId] = list;
        }
        list.Add(entity);
    }

    public RtEntity? FindById(string rtId) =>
        _byId.TryGetValue(rtId, out var e) ? e : null;

    public IReadOnlyList<RtEntity> FindByType(string ckTypeId) =>
        _byType.TryGetValue(ckTypeId, out var list) ? list : Array.Empty<RtEntity>();

    public IEnumerable<RtEntity> All => _byId.Values;

    /// <summary>
    /// Follow an outbound association role from <paramref name="entity"/> and return the
    /// first matching target entity (or null). When <paramref name="targetCkTypeId"/> is
    /// given the result is filtered to that type.
    /// </summary>
    public RtEntity? FollowAssociation(RtEntity entity, string roleId, string? targetCkTypeId = null)
    {
        foreach (var assoc in entity.Associations)
        {
            if (assoc.RoleId != roleId) continue;
            if (targetCkTypeId is not null && assoc.TargetCkTypeId != targetCkTypeId) continue;
            return FindById(assoc.TargetRtId);
        }
        return null;
    }

    /// <summary>
    /// Walk parent chain via <c>System/ParentChild</c> until an entity of
    /// <paramref name="targetCkTypeId"/> is found (or null).
    /// </summary>
    public RtEntity? FindAncestor(RtEntity entity, string targetCkTypeId)
    {
        var current = entity;
        for (var depth = 0; depth < 32 && current is not null; depth++)
        {
            var parent = FollowAssociation(current, "System/ParentChild");
            if (parent is null) return null;
            if (parent.CkTypeId == targetCkTypeId) return parent;
            current = parent;
        }
        return null;
    }
}
