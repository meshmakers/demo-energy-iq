namespace Meshmakers.EnergyIq.HaystackExport.Runtime;

/// <summary>
/// In-memory representation of one RT entity as defined in an OctoMesh runtime
/// model YAML file (e.g. <c>data/bim/rt-firmianstrasse.yaml</c>).
/// </summary>
public sealed class RtEntity
{
    public required string RtId { get; init; }
    public required string CkTypeId { get; init; }

    /// <summary>
    /// Attribute id (e.g. <c>EnergyIQ/Temperature</c>) → raw value.
    /// Scalar values are stored as the YAML-inferred .NET type (string / long / double / bool / DateTime).
    /// Record values are stored as <see cref="RtRecord"/>; record arrays as <c>List&lt;RtRecord&gt;</c>.
    /// </summary>
    public Dictionary<string, object?> Attributes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Outbound associations: each entry is (roleId, targetRtId, targetCkTypeId).
    /// </summary>
    public List<RtAssociation> Associations { get; } = [];
}

public sealed record RtAssociation
{
    public required string RoleId { get; init; }
    public required string TargetRtId { get; init; }
    public string? TargetCkTypeId { get; init; }
}

public sealed class RtRecord
{
    public required string CkRecordId { get; init; }
    public Dictionary<string, object?> Attributes { get; } = new(StringComparer.Ordinal);
}
